namespace GrantMatcher.Shared.Models;

/// <summary>
/// Per-Grant analytics and performance metrics
/// </summary>
public class GrantMetrics
{
    public Guid GrantId { get; set; }
    public string GrantName { get; set; } = string.Empty;
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    // View metrics
    public int TotalViews { get; set; }
    public int UniqueViews { get; set; }
    public double AverageViewDurationSeconds { get; set; }

    // Engagement metrics
    public int TotalSaves { get; set; }
    public int TotalShares { get; set; }
    public int ApplicationLinkClicks { get; set; }

    // Conversion metrics
    public double ViewToSaveRate => TotalViews > 0 ? (double)TotalSaves / TotalViews * 100 : 0;
    public double ViewToClickRate => TotalViews > 0 ? (double)ApplicationLinkClicks / TotalViews * 100 : 0;

    // Search performance
    public int TimesInSearchResults { get; set; }
    public double AverageSearchPosition { get; set; }
    public double AverageMatchScore { get; set; }

    // Time-based metrics (last 7 days)
    public int ViewsLast7Days { get; set; }
    public int SavesLast7Days { get; set; }
    public int ClicksLast7Days { get; set; }

    // Trending
    public double TrendingScore { get; set; }
    public bool IsTrending { get; set; }

    // User demographics (aggregated)
    public Dictionary<string, int> ViewsByMajor { get; set; } = new();
    public Dictionary<string, int> ViewsByState { get; set; } = new();
    public Dictionary<string, int> ViewsByGpaRange { get; set; } = new();

    // For Cosmos DB partitioning
    public string PartitionKey => $"Grant_{GrantId}";
}

// TopGrants and GrantRanking classes moved to AnalyticsDTOs.cs to avoid duplication

/// <summary>
/// Overall application analytics
/// </summary>
public class ApplicationMetrics
{
    public DateTime Date { get; set; }
    public string Period { get; set; } = "daily"; // daily, weekly, monthly

    // User metrics
    public int TotalUsers { get; set; }
    public int NewUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int ReturningUsers { get; set; }

    // Profile metrics
    public int ProfilesCreated { get; set; }
    public int ProfilesUpdated { get; set; }
    public double ProfileCompletionRate { get; set; }

    // Conversation metrics
    public int ConversationsStarted { get; set; }
    public int ConversationsCompleted { get; set; }
    public double ConversationCompletionRate => ConversationsStarted > 0
        ? (double)ConversationsCompleted / ConversationsStarted * 100 : 0;
    public double AverageMessagesPerConversation { get; set; }

    // Search metrics
    public int SearchesPerformed { get; set; }
    public double AverageResultsPerSearch { get; set; }
    public double AverageSearchDurationMs { get; set; }

    // Grant metrics
    public int GrantsViewed { get; set; }
    public int GrantsSaved { get; set; }
    public int ApplicationLinksClicked { get; set; }
    public double SaveRate => GrantsViewed > 0
        ? (double)GrantsSaved / GrantsViewed * 100 : 0;
    public double ClickThroughRate => GrantsViewed > 0
        ? (double)ApplicationLinksClicked / GrantsViewed * 100 : 0;

    // Performance metrics
    public double AverageApiResponseTimeMs { get; set; }
    public int TotalApiCalls { get; set; }
    public int FailedApiCalls { get; set; }
    public double ApiErrorRate => TotalApiCalls > 0
        ? (double)FailedApiCalls / TotalApiCalls * 100 : 0;

    // For Cosmos DB partitioning
    public string PartitionKey => $"metrics_{Period}_{Date:yyyy-MM}";
}
