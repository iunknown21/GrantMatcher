using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GrantMatcher.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace GrantMatcher.Core.Services;

public class GroqService : IGroqService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GroqService>? _logger;
    private const string DefaultModel = "llama-3.3-70b-versatile"; // Fast and high-quality

    public GroqService(HttpClient httpClient, string apiKey, ILogger<GroqService>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public async Task<string> GenerateGrantSummaryAsync(
        string title,
        string agency,
        string description,
        decimal? fundingFloor,
        decimal? fundingCeiling,
        string? closeDate,
        List<string> eligibleApplicants,
        CancellationToken cancellationToken = default)
    {
        var fundingRange = "";
        if (fundingFloor.HasValue && fundingCeiling.HasValue)
        {
            fundingRange = $"${fundingFloor.Value:N0} - ${fundingCeiling.Value:N0}";
        }
        else if (fundingCeiling.HasValue)
        {
            fundingRange = $"up to ${fundingCeiling.Value:N0}";
        }

        var eligibleList = eligibleApplicants.Any()
            ? string.Join(", ", eligibleApplicants)
            : "Various eligible applicants";

        var prompt = $@"Create a 200-word summary of this federal grant opportunity for nonprofit organizations:

Title: {title}
Agency: {agency}
Funding: {fundingRange}
Deadline: {closeDate ?? "Not specified"}
Eligible: {eligibleList}

Description:
{description.Substring(0, Math.Min(description.Length, 2000))}

Focus on:
- Who should apply (be specific about the types of organizations)
- What activities are funded
- Key requirements or priorities
- Why this grant matters to the nonprofit sector

Write in a clear, actionable style. Start with the most important information.";

        return await GenerateCompletionAsync(prompt, "You are a grant advisor helping nonprofits find relevant funding opportunities. Write concise, actionable summaries.", cancellationToken);
    }

    public async Task<string> GenerateCompletionAsync(
        string prompt,
        string systemMessage = "",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GroqChatRequest
            {
                Model = DefaultModel,
                Messages = new List<GroqMessage>
                {
                    new() { Role = "system", Content = systemMessage.IsNullOrEmpty() ? "You are a helpful assistant." : systemMessage },
                    new() { Role = "user", Content = prompt }
                },
                Temperature = 0.7,
                MaxTokens = 400 // Enough for 200-word summary
            };

            _logger?.LogInformation("Calling Groq AI with model {Model}", DefaultModel);

            var response = await _httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GroqChatResponse>(cancellationToken);

            if (result?.Choices == null || !result.Choices.Any())
            {
                throw new InvalidOperationException("No response from Groq AI");
            }

            var summary = result.Choices[0].Message.Content;
            _logger?.LogInformation("Generated summary: {Length} characters", summary.Length);

            return summary;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error calling Groq AI");
            throw;
        }
    }

    // DTOs for Groq API
    private class GroqChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<GroqMessage> Messages { get; set; } = new();

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.7;

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; } = 400;
    }

    private class GroqMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private class GroqChatResponse
    {
        [JsonPropertyName("choices")]
        public List<GroqChoice> Choices { get; set; } = new();
    }

    private class GroqChoice
    {
        [JsonPropertyName("message")]
        public GroqMessage Message { get; set; } = new();
    }
}

public static class StringExtensions
{
    public static bool IsNullOrEmpty(this string? value) => string.IsNullOrEmpty(value);
}
