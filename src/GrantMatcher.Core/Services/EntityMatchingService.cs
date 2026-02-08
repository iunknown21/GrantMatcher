using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using GrantMatcher.Core.Interfaces;
using GrantMatcher.Shared.Constants;
using GrantMatcher.Shared.DTOs;
using GrantMatcher.Shared.Models;

namespace GrantMatcher.Core.Services;

public class EntityMatchingService : IEntityMatchingService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<EntityMatchingService>? _logger;

    public EntityMatchingService(HttpClient httpClient, string apiKey, ILogger<EntityMatchingService>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _logger = logger;

        // Set default headers
        _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);

        _logger?.LogInformation("EntityMatchingService initialized with base URL: {BaseUrl}", _httpClient.BaseAddress);
    }

    public async Task<string> StoreGrantEntityAsync(GrantEntity Grant, CancellationToken cancellationToken = default)
    {
        if (Grant == null)
            throw new ArgumentNullException(nameof(Grant));

        if (string.IsNullOrWhiteSpace(Grant.Name))
            throw new ArgumentException("Grant name is required", nameof(Grant));

        try
        {
            _logger?.LogInformation("Storing Grant entity: {Name}", Grant.Name);

            var entityRequest = new EntityRequest
            {
                EntityType = AppConstants.EntityTypes.Grant,
                Name = Grant.Name,
                Description = Grant.NaturalLanguageSummary,
                Attributes = BuildGrantAttributes(Grant)
            };

            var response = await _httpClient.PostAsJsonAsync("/profiles", entityRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger?.LogError("Failed to store Grant entity. Status: {Status}, Error: {Error}",
                    response.StatusCode, error);
                response.EnsureSuccessStatusCode();
            }

            var entityResponse = await response.Content.ReadFromJsonAsync<EntityResponse>(cancellationToken);

            if (entityResponse?.Id == null)
            {
                _logger?.LogError("Entity response did not contain an ID");
                throw new InvalidOperationException("Failed to get entity ID from response");
            }

            _logger?.LogInformation("Successfully stored Grant entity with ID: {EntityId}", entityResponse.Id);
            return entityResponse.Id;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP error while storing Grant entity: {Name}", Grant.Name);
            throw new InvalidOperationException($"Failed to store Grant entity: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error while storing Grant entity: {Name}", Grant.Name);
            throw;
        }
    }

    public async Task UploadEmbeddingAsync(string entityId, float[] embedding, CancellationToken cancellationToken = default)
    {
        var request = new EmbeddingUploadRequest
        {
            Embedding = embedding,
            EmbeddingModel = "text-embedding-3-small"
        };

        var response = await _httpClient.PostAsJsonAsync($"/profiles/{entityId}/embeddings/upload", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<VectorSearchResponse> SearchGrantsAsync(NonprofitProfile Nonprofit, VectorSearchRequest searchRequest, CancellationToken cancellationToken = default)
    {
        // Build filters based on Nonprofit eligibility
        searchRequest.AttributeFilters = BuildEligibilityFilters(Nonprofit);

        var response = await _httpClient.PostAsJsonAsync("/profiles/search", searchRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var searchResponse = await response.Content.ReadFromJsonAsync<VectorSearchResponse>(cancellationToken);
        return searchResponse ?? new VectorSearchResponse();
    }

    public async Task<EntityResponse?> GetEntityAsync(string entityId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/profiles/{entityId}", cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<EntityResponse>(cancellationToken);
    }

    public async Task DeleteEntityAsync(string entityId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"/profiles/{entityId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ConversationResponse> SendConversationMessageAsync(string entityId, string message, string? systemPrompt = null, CancellationToken cancellationToken = default)
    {
        var request = new
        {
            Message = message,
            SystemPrompt = systemPrompt ?? BuildProfileExtractionSystemPrompt()
        };

        var response = await _httpClient.PostAsJsonAsync($"/entities/{entityId}/conversation", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EntityMatchingConversationResponse>(cancellationToken);

        if (result == null)
            throw new InvalidOperationException("Failed to get conversation response");

        // Map EntityMatchingAI response to our ConversationResponse format
        return new ConversationResponse
        {
            Reply = result.AiResponse ?? "",
            UpdatedHistory = new List<ConversationMessage>(), // EntityMatchingAI manages history internally
            ExtractedData = MapInsightsToExtractedData(result.NewInsights),
            ProfileComplete = result.NewInsights?.Count >= 5 // Simple heuristic: need at least 5 insights
        };
    }

    public async Task<List<ConversationMessage>> GetConversationHistoryAsync(string entityId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/entities/{entityId}/conversation", cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new List<ConversationMessage>();

        var result = await response.Content.ReadFromJsonAsync<EntityMatchingConversationContext>(cancellationToken);

        if (result?.ConversationChunks == null)
            return new List<ConversationMessage>();

        // Convert EntityMatchingAI conversation chunks to our format
        var messages = new List<ConversationMessage>();
        foreach (var chunk in result.ConversationChunks)
        {
            messages.Add(new ConversationMessage
            {
                Role = chunk.Role ?? "user",
                Content = chunk.Content ?? "",
                Timestamp = chunk.Timestamp ?? DateTime.UtcNow
            });
        }

        return messages;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        // EntityMatchingAI doesn't expose a direct embedding endpoint
        // Instead, we'll use OpenAI directly (simpler for now)
        // TODO: Consider creating a temporary entity and extracting its embedding
        throw new NotImplementedException("Embedding generation should be handled when storing entities. EntityMatchingAI generates embeddings internally.");
    }

    public async Task<string> CreateNonprofitEntityAsync(string userId, string name, CancellationToken cancellationToken = default)
    {
        var entityRequest = new EntityRequest
        {
            EntityType = 1, // Person/Nonprofit type
            Name = name,
            Description = "Nonprofit profile for Grant matching",
            Attributes = new Dictionary<string, object>
            {
                { "userId", userId },
                { "entityPurpose", "Grant-matching" }
            }
        };

        var response = await _httpClient.PostAsJsonAsync("/profiles", entityRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var entityResponse = await response.Content.ReadFromJsonAsync<EntityResponse>(cancellationToken);
        return entityResponse?.Id ?? throw new InvalidOperationException("Failed to create Nonprofit entity");
    }

    private string BuildProfileExtractionSystemPrompt()
    {
        return @"You are a helpful assistant helping Nonprofits build their Grant profile through conversation.

Your job is to:
1. Ask friendly questions to extract: GPA, major, extracurricular activities, interests, career goals
2. Extract structured insights from their responses
3. Determine when you have enough information

Extract insights about:
- Academic information (GPA, major, school)
- Extracurricular activities
- Interests and hobbies
- Career goals and aspirations
- Personal background (first-generation, ethnicity if volunteered)

Be conversational and encouraging. Ask follow-up questions to get specific details.";
    }

    private ExtractedProfileData? MapInsightsToExtractedData(List<EntityMatchingInsight>? insights)
    {
        if (insights == null || !insights.Any())
            return null;

        var data = new ExtractedProfileData();

        foreach (var insight in insights)
        {
            var category = insight.Category?.ToLower() ?? "";
            var insightText = insight.Insight ?? "";

            // Simple mapping - in production, you'd want more sophisticated parsing
            switch (category)
            {
                case "organization":
                case "nonprofit":
                    if (insightText.Contains("mission", StringComparison.OrdinalIgnoreCase))
                        data.MissionStatement = insightText;
                    if (insightText.Contains("EIN", StringComparison.OrdinalIgnoreCase))
                        data.EIN = insightText;
                    if (insightText.Contains("type", StringComparison.OrdinalIgnoreCase))
                        data.OrganizationType = insightText;
                    break;

                case "service":
                case "area":
                    if (data.ServiceAreas == null)
                        data.ServiceAreas = new List<string>();
                    data.ServiceAreas.Add(insightText);
                    break;

                case "funding":
                case "category":
                    if (data.FundingCategories == null)
                        data.FundingCategories = new List<string>();
                    data.FundingCategories.Add(insightText);
                    break;

                case "budget":
                case "financial":
                    // Try to extract budget amount
                    var budgetMatch = System.Text.RegularExpressions.Regex.Match(insightText, @"\$?[\d,]+");
                    if (budgetMatch.Success)
                    {
                        var budgetStr = budgetMatch.Value.Replace("$", "").Replace(",", "");
                        if (decimal.TryParse(budgetStr, out var budget))
                            data.AnnualBudget = budget;
                    }
                    break;
            }
        }

        return data;
    }

    private Dictionary<string, object> BuildGrantAttributes(GrantEntity Grant)
    {
        var attributes = new Dictionary<string, object>
        {
            { "grantId", Grant.id },
            { "opportunityNumber", Grant.OpportunityNumber },
            { "agency", Grant.Agency },
            { "agencyCode", Grant.AgencyCode },
            { "applicationUrl", Grant.ApplicationUrl }
        };

        if (Grant.AwardCeiling.HasValue)
            attributes["awardCeiling"] = Grant.AwardCeiling.Value;

        if (Grant.AwardFloor.HasValue)
            attributes["awardFloor"] = Grant.AwardFloor.Value;

        if (Grant.CloseDate.HasValue)
            attributes["closeDate"] = Grant.CloseDate.Value.ToString("O");

        if (Grant.ExpectedNumberOfAwards.HasValue)
            attributes["expectedNumberOfAwards"] = Grant.ExpectedNumberOfAwards.Value;

        // Add grant-specific eligibility criteria
        if (Grant.ApplicantTypes.Any())
            attributes["applicantTypes"] = Grant.ApplicantTypes;

        if (Grant.FundingCategories.Any())
            attributes["fundingCategories"] = Grant.FundingCategories;

        if (Grant.EligibleStates.Any())
            attributes["eligibleStates"] = Grant.EligibleStates;

        if (Grant.AwardCeiling.HasValue)
            attributes["awardCeiling"] = Grant.AwardCeiling.Value;

        if (Grant.AwardFloor.HasValue)
            attributes["awardFloor"] = Grant.AwardFloor.Value;

        if (!string.IsNullOrEmpty(Grant.Agency))
            attributes["agency"] = Grant.Agency;

        if (!string.IsNullOrEmpty(Grant.OpportunityNumber))
            attributes["opportunityNumber"] = Grant.OpportunityNumber;

        if (Grant.CloseDate.HasValue)
            attributes["closeDate"] = Grant.CloseDate.Value;

        return attributes;
    }

    private FilterGroup BuildEligibilityFilters(NonprofitProfile Nonprofit)
    {
        var filters = new List<AttributeFilter>();

        // State filter: if Grant has eligible states, Nonprofit must match
        if (!string.IsNullOrEmpty(Nonprofit.State))
        {
            filters.Add(new AttributeFilter
            {
                FieldPath = "attributes.eligibleStates",
                Operator = "ContainsOrEmpty",
                Value = Nonprofit.State
            });
        }

        // Applicant type filter: if Grant has applicant types, Nonprofit must match at least one
        if (Nonprofit.ApplicantTypes.Any())
        {
            filters.Add(new AttributeFilter
            {
                FieldPath = "attributes.applicantTypes",
                Operator = "ContainsOrEmpty",
                Value = Nonprofit.ApplicantTypes.First() // Simplified - in real implementation handle multiple
            });
        }

        // Funding category filter: if Grant has funding categories, Nonprofit must match at least one
        if (Nonprofit.FundingCategories.Any())
        {
            filters.Add(new AttributeFilter
            {
                FieldPath = "attributes.fundingCategories",
                Operator = "ContainsOrEmpty",
                Value = Nonprofit.FundingCategories.First() // Simplified
            });
        }

        return new FilterGroup
        {
            LogicalOperator = "And",
            Filters = filters
        };
    }
}
