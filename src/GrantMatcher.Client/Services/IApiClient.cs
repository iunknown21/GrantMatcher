using GrantMatcher.Shared.DTOs;
using GrantMatcher.Shared.Models;

namespace GrantMatcher.Client.Services;

public interface IApiClient
{
    // Profile operations
    Task<NonprofitProfile> CreateProfileAsync(NonprofitProfile profile);
    Task<NonprofitProfile?> GetProfileAsync(Guid id);
    Task<NonprofitProfile> UpdateProfileAsync(Guid id, NonprofitProfile profile);
    Task DeleteProfileAsync(Guid id);

    // Grant operations
    Task<GrantEntity?> GetGrantAsync(Guid id);
    Task<List<GrantEntity>> ListGrantsAsync();

    // Matching operations
    Task<SearchResponse> SearchGrantsAsync(SearchRequest request);

    // Conversation operations
    Task<ConversationResponse> ProcessConversationAsync(ConversationRequest request);
    Task<float[]> GenerateEmbeddingAsync(string text);

    // Admin operations - commented out for now, can be added later when needed
    // Task<GrantEntity> CreateGrantAsync(GrantEntity Grant);
    // Task<GrantEntity> UpdateGrantAsync(Guid id, GrantEntity Grant);
    // Task DeleteGrantAsync(Guid id);
    // Task<BulkImportResult> BulkImportGrantsAsync(List<GrantEntity> Grants);
    // Task<AdminStats> GetAdminStatsAsync();
    // Task<List<NonprofitProfile>> ListNonprofitProfilesAsync();
}

public class BulkImportResult
{
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class AdminStats
{
    public int TotalGrants { get; set; }
    public int GrantsAddedThisMonth { get; set; }
    public int TotalNonprofits { get; set; }
    public int NonprofitsJoinedThisMonth { get; set; }
    public int TotalMatches { get; set; }
    public double AverageMatchesPerNonprofit { get; set; }
    public string SystemHealth { get; set; } = "Healthy";
}
