using GrantMatcher.Shared.Models;

namespace GrantMatcher.Shared.DTOs;

/// <summary>
/// Request to track an analytics event
/// </summary>
public class TrackEventRequest
{
    public string UserId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string EventCategory { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
    public string? PageUrl { get; set; }
    public string? PageTitle { get; set; }
    public string? Referrer { get; set; }
    public string? UserAgent { get; set; }
    public double? DurationMs { get; set; }
}

/// <summary>
/// Response after tracking an event
/// </summary>
public class TrackEventResponse
{
    public bool Success { get; set; }
    public string EventId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Request to get analytics data
/// </summary>
public class GetAnalyticsRequest
{
    public DateTime StartDate { get; set; } = DateTime.UtcNow.AddDays(-7);
    public DateTime EndDate { get; set; } = DateTime.UtcNow;
    public string? UserId { get; set; }
    public string? EventCategory { get; set; }
    public string? EventType { get; set; }
    public string? MetricType { get; set; } // "overview", "Grant", "user", "session"
    public int Limit { get; set; } = 100;
    public int Offset { get; set; } = 0;
}

/// <summary>
/// Response containing analytics data
/// </summary>
public class GetAnalyticsResponse
{
    public ApplicationMetrics? Overview { get; set; }
    public SessionMetrics? SessionData { get; set; }
    public List<GrantMetrics> GrantData { get; set; } = new();
    public TopGrants? TopPerformers { get; set; }
    public List<AnalyticsEvent> Events { get; set; } = new();
    public int TotalCount { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Request to generate an analytics report
/// </summary>
public class GenerateReportRequest
{
    public string ReportType { get; set; } = "daily"; // daily, weekly, monthly, custom
    public DateTime StartDate { get; set; } = DateTime.UtcNow.AddDays(-7);
    public DateTime EndDate { get; set; } = DateTime.UtcNow;
    public List<string> Metrics { get; set; } = new(); // Which metrics to include
    public string Format { get; set; } = "json"; // json, csv, pdf
}

/// <summary>
/// Response containing generated report
/// </summary>
public class GenerateReportResponse
{
    public bool Success { get; set; }
    public string ReportId { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string? DownloadUrl { get; set; }
    public object? Data { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Real-time analytics summary
/// </summary>
public class RealTimeAnalytics
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int ActiveUsers { get; set; }
    public int ActiveSessions { get; set; }
    public int EventsLastMinute { get; set; }
    public int EventsLastHour { get; set; }
    public List<RecentEvent> RecentEvents { get; set; } = new();
}

public class RecentEvent
{
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? UserId { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Funnel analysis for conversion tracking
/// </summary>
public class FunnelAnalysis
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<FunnelStep> Steps { get; set; } = new();
    public double OverallConversionRate { get; set; }
}

public class FunnelStep
{
    public string StepName { get; set; } = string.Empty;
    public int StepNumber { get; set; }
    public int UserCount { get; set; }
    public double DropoffRate { get; set; }
    public double ConversionRate { get; set; }
}

/// <summary>
/// Cohort analysis for user retention
/// </summary>
public class CohortAnalysis
{
    public string CohortName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public int InitialUsers { get; set; }
    public List<RetentionData> RetentionByWeek { get; set; } = new();
}

public class RetentionData
{
    public int WeekNumber { get; set; }
    public DateTime WeekStart { get; set; }
    public int ActiveUsers { get; set; }
    public double RetentionRate { get; set; }
}

/// <summary>
/// Top performing grants based on various metrics
/// </summary>
public class TopGrants
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<TopGrantItem> Grants { get; set; } = new();
}

public class TopGrantItem
{
    public Guid GrantId { get; set; }
    public string GrantName { get; set; } = string.Empty;
    public string Agency { get; set; } = string.Empty;
    public int ViewCount { get; set; }
    public int SaveCount { get; set; }
    public int ApplicationLinkClicks { get; set; }
    public double EngagementScore { get; set; }
}
