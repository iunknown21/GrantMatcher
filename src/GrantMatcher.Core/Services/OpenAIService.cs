using System.Net.Http.Json;
using System.Text.Json;
using GrantMatcher.Core.Interfaces;
using GrantMatcher.Shared.DTOs;

namespace GrantMatcher.Core.Services;

public class OpenAIService : IOpenAIService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _embeddingModel;
    private readonly string _chatModel;
    private readonly ICachingService? _cachingService;
    private readonly IPerformanceMonitor? _performanceMonitor;

    public OpenAIService(
        HttpClient httpClient,
        string apiKey,
        string embeddingModel = "text-embedding-3-small",
        string chatModel = "gpt-4o-mini",
        ICachingService? cachingService = null,
        IPerformanceMonitor? performanceMonitor = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _embeddingModel = embeddingModel;
        _chatModel = chatModel;
        _cachingService = cachingService;
        _performanceMonitor = performanceMonitor;

        _httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_performanceMonitor != null)
        {
            return await _performanceMonitor.TrackAsync(
                "GenerateEmbedding",
                async () => await GenerateEmbeddingInternalAsync(text, cancellationToken),
                TimeSpan.FromSeconds(2),
                new Dictionary<string, object> { ["TextLength"] = text.Length },
                cancellationToken);
        }

        return await GenerateEmbeddingInternalAsync(text, cancellationToken);
    }

    private async Task<float[]> GenerateEmbeddingInternalAsync(string text, CancellationToken cancellationToken = default)
    {
        // Try to get from cache first
        if (_cachingService != null)
        {
            var cacheKey = CacheKeys.Embedding(text);
            var cached = await _cachingService.GetOrCreateAsync(
                cacheKey,
                async () =>
                {
                    var embedding = await GenerateEmbeddingFromAPIAsync(text, cancellationToken);
                    return new EmbeddingWrapper { Values = embedding };
                },
                absoluteExpiration: TimeSpan.FromHours(24), // Embeddings are deterministic, cache for 24 hours
                slidingExpiration: TimeSpan.FromHours(12),
                cancellationToken);

            if (cached?.Values != null)
                return cached.Values;
        }

        return await GenerateEmbeddingFromAPIAsync(text, cancellationToken);
    }

    private async Task<float[]> GenerateEmbeddingFromAPIAsync(string text, CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = _embeddingModel,
            input = text
        };

        var response = await _httpClient.PostAsJsonAsync("embeddings", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        var embeddingArray = result?.RootElement
            .GetProperty("data")[0]
            .GetProperty("embedding");

        if (!embeddingArray.HasValue)
            throw new InvalidOperationException("Failed to extract embedding from OpenAI response");

        var floatList = new List<float>();
        foreach (var element in embeddingArray.Value.EnumerateArray())
        {
            floatList.Add(element.GetSingle());
        }

        return floatList.ToArray();
    }

    // Wrapper class for caching embeddings
    private class EmbeddingWrapper
    {
        public float[] Values { get; set; } = Array.Empty<float>();
    }

    public async Task<ConversationResponse> ProcessConversationAsync(ConversationRequest request, CancellationToken cancellationToken = default)
    {
        if (_performanceMonitor != null)
        {
            return await _performanceMonitor.TrackAsync(
                "ProcessConversation",
                async () => await ProcessConversationInternalAsync(request, cancellationToken),
                TimeSpan.FromSeconds(5),
                new Dictionary<string, object>
                {
                    ["MessageLength"] = request.Message?.Length ?? 0,
                    ["HistoryCount"] = request.History?.Count ?? 0
                },
                cancellationToken);
        }

        return await ProcessConversationInternalAsync(request, cancellationToken);
    }

    private async Task<ConversationResponse> ProcessConversationInternalAsync(ConversationRequest request, CancellationToken cancellationToken = default)
    {
        var systemMessage = BuildProfileExtractionSystemMessage();
        var messages = new List<object> { systemMessage };

        // Add conversation history
        foreach (var msg in request.History)
        {
            messages.Add(new { role = msg.Role, content = msg.Content });
        }

        // Add current user message
        messages.Add(new { role = "user", content = request.Message });

        var chatRequest = new
        {
            model = _chatModel,
            messages = messages,
            temperature = 0.7,
            response_format = new { type = "json_object" }
        };

        var response = await _httpClient.PostAsJsonAsync("chat/completions", chatRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        var assistantMessage = result?.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrEmpty(assistantMessage))
            throw new InvalidOperationException("Failed to get response from OpenAI");

        // Parse the JSON response
        var parsedResponse = JsonSerializer.Deserialize<ConversationAIResponse>(assistantMessage);

        var updatedHistory = request.History.ToList();
        updatedHistory.Add(new ConversationMessage { Role = "user", Content = request.Message, Timestamp = DateTime.UtcNow });
        updatedHistory.Add(new ConversationMessage { Role = "assistant", Content = parsedResponse?.Reply ?? "", Timestamp = DateTime.UtcNow });

        return new ConversationResponse
        {
            Reply = parsedResponse?.Reply ?? "",
            UpdatedHistory = updatedHistory,
            ExtractedData = parsedResponse?.ExtractedData,
            ProfileComplete = parsedResponse?.ProfileComplete ?? false
        };
    }

    public async Task<string> GenerateProfileSummaryAsync(Dictionary<string, object> profileData, CancellationToken cancellationToken = default)
    {
        var prompt = $@"Generate a natural language summary (2-3 sentences) for a Nonprofit profile based on this data:
{JsonSerializer.Serialize(profileData, new JsonSerializerOptions { WriteIndented = true })}

The summary should highlight:
- Academic achievements and major
- Interests and activities
- Career goals
- Any unique characteristics (first-gen, ethnicity, etc.)

Write in third person. Make it concise and focused on matching Grant criteria.";

        var messages = new[]
        {
            new { role = "system", content = "You are a helpful assistant that creates concise profile summaries for Grant matching." },
            new { role = "user", content = prompt }
        };

        var request = new
        {
            model = _chatModel,
            messages = messages,
            temperature = 0.5
        };

        var response = await _httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        var summary = result?.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return summary ?? string.Empty;
    }

    private object BuildProfileExtractionSystemMessage()
    {
        return new
        {
            role = "system",
            content = @"You are a helpful assistant helping Nonprofits build their Grant profile through conversation.

Your job is to:
1. Ask friendly questions to extract: GPA, major, extracurricular activities, interests, career goals
2. Extract structured data from their responses
3. Determine when you have enough information

Always respond with valid JSON in this format:
{
  ""reply"": ""Your friendly response or question"",
  ""extractedData"": {
    ""gpa"": 3.8,
    ""major"": ""Computer Science"",
    ""extracurricularActivities"": [""Debate Team"", ""Volunteer at food bank""],
    ""interests"": [""AI"", ""Social justice""],
    ""careerGoals"": [""Software engineer at tech company""],
    ""profileSummary"": ""A natural language summary...""
  },
  ""profileComplete"": false
}

Only include fields in extractedData that you've learned about. Set profileComplete to true when you have at least: major, some activities/interests, and career goals."
        };
    }

    private class ConversationAIResponse
    {
        public string Reply { get; set; } = string.Empty;
        public ExtractedProfileData? ExtractedData { get; set; }
        public bool ProfileComplete { get; set; }
    }
}
