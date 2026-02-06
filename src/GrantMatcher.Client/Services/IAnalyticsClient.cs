using GrantMatcher.Shared.DTOs;

namespace GrantMatcher.Client.Services;

/// <summary>
/// Client-side analytics tracking service
/// </summary>
public interface IAnalyticsClient
{
    // Session management
    string SessionId { get; }
    Task InitializeAsync();

    // Event tracking (fire-and-forget, non-blocking)
    Task TrackPageViewAsync(string pageUrl, string pageTitle);
    Task TrackEventAsync(string eventType, string eventCategory, Dictionary<string, object>? properties = null);
    Task TrackConversationStartedAsync();
    Task TrackConversationMessageAsync(int messageNumber);
    Task TrackConversationCompletedAsync();
    Task TrackProfileCreatedAsync(Guid profileId);
    Task TrackProfileUpdatedAsync(Guid profileId);
    Task TrackSearchPerformedAsync(string query, int resultCount);
    Task TrackGrantViewedAsync(Guid GrantId, string GrantName);
    Task TrackGrantSavedAsync(Guid GrantId, string GrantName);
    Task TrackApplicationLinkClickedAsync(Guid GrantId, string GrantName);
    Task TrackErrorAsync(string errorMessage, string? stackTrace = null);

    // Time tracking
    void StartTimeTracking(string pageUrl);
    Task EndTimeTrackingAsync(string pageUrl);

    // Analytics retrieval
    Task<GetAnalyticsResponse?> GetAnalyticsAsync(GetAnalyticsRequest request);
    Task<GenerateReportResponse?> GenerateReportAsync(GenerateReportRequest request);
    Task<RealTimeAnalytics?> GetRealTimeAnalyticsAsync();
    Task<FunnelAnalysis?> GetFunnelAnalysisAsync(int daysBack = 30);
    Task<TopGrants?> GetTopGrantsAsync(int limit = 10);
}
