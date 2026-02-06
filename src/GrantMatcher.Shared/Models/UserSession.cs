namespace GrantMatcher.Shared.Models;

/// <summary>
/// Tracks a user session for analytics and behavior analysis
/// </summary>
public class UserSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public double DurationMinutes => EndTime.HasValue
        ? (EndTime.Value - StartTime).TotalMinutes
        : (DateTime.UtcNow - StartTime).TotalMinutes;

    // Session metrics
    public int PageViews { get; set; }
    public int EventCount { get; set; }
    public List<string> PagesVisited { get; set; } = new();

    // User actions during session
    public bool ProfileCreated { get; set; }
    public bool ProfileUpdated { get; set; }
    public int SearchesPerformed { get; set; }
    public int GrantsViewed { get; set; }
    public int GrantsSaved { get; set; }
    public int ApplicationLinksClicked { get; set; }
    public bool ConversationStarted { get; set; }
    public bool ConversationCompleted { get; set; }

    // Entry and exit
    public string? EntryPage { get; set; }
    public string? ExitPage { get; set; }
    public string? Referrer { get; set; }

    // Device context
    public string? DeviceType { get; set; }
    public string? Browser { get; set; }
    public string? OS { get; set; }

    // Geographic context
    public string? Country { get; set; }
    public string? Region { get; set; }

    // For Cosmos DB partitioning
    public string PartitionKey => $"session_{StartTime:yyyy-MM-dd}";
}

/// <summary>
/// Aggregated session metrics for reporting
/// </summary>
public class SessionMetrics
{
    public DateTime Date { get; set; }
    public int TotalSessions { get; set; }
    public int UniqueSessions { get; set; }
    public double AverageDurationMinutes { get; set; }
    public double AveragePageViews { get; set; }
    public int BounceCount { get; set; }
    public double BounceRate => TotalSessions > 0 ? (double)BounceCount / TotalSessions * 100 : 0;

    // Conversion metrics
    public int SessionsWithProfileCreation { get; set; }
    public int SessionsWithSearch { get; set; }
    public int SessionsWithGrantView { get; set; }
    public int SessionsWithApplicationClick { get; set; }

    public double ProfileCreationRate => TotalSessions > 0 ? (double)SessionsWithProfileCreation / TotalSessions * 100 : 0;
    public double SearchRate => TotalSessions > 0 ? (double)SessionsWithSearch / TotalSessions * 100 : 0;
    public double ViewToClickRate => SessionsWithGrantView > 0 ? (double)SessionsWithApplicationClick / SessionsWithGrantView * 100 : 0;
}
