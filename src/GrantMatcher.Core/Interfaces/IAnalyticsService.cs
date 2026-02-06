using GrantMatcher.Shared.DTOs;
using GrantMatcher.Shared.Models;

namespace GrantMatcher.Core.Interfaces;

/// <summary>
/// Service for tracking and analyzing user behavior and application metrics
/// </summary>
public interface IAnalyticsService
{
    // Event tracking
    Task<TrackEventResponse> TrackEventAsync(TrackEventRequest request, CancellationToken cancellationToken = default);
    Task TrackEventAsync(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken = default);

    // Session management
    Task<UserSession> CreateSessionAsync(string userId, string? referrer = null, CancellationToken cancellationToken = default);
    Task UpdateSessionAsync(UserSession session, CancellationToken cancellationToken = default);
    Task<UserSession?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    Task EndSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    // Metrics retrieval
    Task<ApplicationMetrics> GetApplicationMetricsAsync(DateTime startDate, DateTime endDate, string period = "daily", CancellationToken cancellationToken = default);
    Task<SessionMetrics> GetSessionMetricsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<List<GrantMetrics>> GetGrantMetricsAsync(DateTime startDate, DateTime endDate, int limit = 100, CancellationToken cancellationToken = default);
    Task<GrantMetrics?> GetGrantMetricsByIdAsync(Guid GrantId, CancellationToken cancellationToken = default);

    // Top performers
    Task<TopGrants> GetTopGrantsAsync(int limit = 10, CancellationToken cancellationToken = default);

    // Event querying
    Task<List<AnalyticsEvent>> GetEventsAsync(GetAnalyticsRequest request, CancellationToken cancellationToken = default);
    Task<int> GetEventCountAsync(string eventType, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    // Analytics calculations
    Task CalculateGrantMetricsAsync(Guid GrantId, CancellationToken cancellationToken = default);
    Task CalculateApplicationMetricsAsync(DateTime date, string period, CancellationToken cancellationToken = default);

    // Funnel and cohort analysis
    Task<FunnelAnalysis> GetFunnelAnalysisAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<List<CohortAnalysis>> GetCohortAnalysisAsync(DateTime startDate, int numberOfCohorts = 6, CancellationToken cancellationToken = default);

    // Real-time analytics
    Task<RealTimeAnalytics> GetRealTimeAnalyticsAsync(CancellationToken cancellationToken = default);
}
