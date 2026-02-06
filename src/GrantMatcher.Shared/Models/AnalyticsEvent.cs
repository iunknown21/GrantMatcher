namespace GrantMatcher.Shared.Models;

/// <summary>
/// Base model for tracking user events in the application
/// </summary>
public class AnalyticsEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string EventCategory { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Event details
    public Dictionary<string, object> Properties { get; set; } = new();

    // Context
    public string? PageUrl { get; set; }
    public string? PageTitle { get; set; }
    public string? Referrer { get; set; }
    public string? UserAgent { get; set; }

    // Device info
    public string? DeviceType { get; set; }
    public string? Browser { get; set; }
    public string? OS { get; set; }
    public string? ScreenResolution { get; set; }

    // Geographic info
    public string? Country { get; set; }
    public string? Region { get; set; }
    public string? City { get; set; }

    // Performance metrics
    public double? DurationMs { get; set; }

    // For Cosmos DB partitioning
    public string PartitionKey => $"{EventCategory}_{Timestamp:yyyy-MM-dd}";
}

/// <summary>
/// Event types for analytics tracking
/// </summary>
public static class EventTypes
{
    // Conversation events
    public const string ConversationStarted = "conversation_started";
    public const string ConversationMessageSent = "conversation_message_sent";
    public const string ConversationCompleted = "conversation_completed";
    public const string ConversationAbandoned = "conversation_abandoned";

    // Profile events
    public const string ProfileCreated = "profile_created";
    public const string ProfileUpdated = "profile_updated";
    public const string ProfileViewed = "profile_viewed";

    // Search events
    public const string SearchPerformed = "search_performed";
    public const string SearchFilterApplied = "search_filter_applied";
    public const string SearchResultsViewed = "search_results_viewed";

    // Grant events
    public const string GrantViewed = "Grant_viewed";
    public const string GrantSaved = "Grant_saved";
    public const string GrantUnsaved = "Grant_unsaved";
    public const string GrantShared = "Grant_shared";
    public const string ApplicationLinkClicked = "application_link_clicked";

    // Page events
    public const string PageViewed = "page_viewed";
    public const string PageTimeSpent = "page_time_spent";

    // Error events
    public const string ErrorOccurred = "error_occurred";
    public const string ApiCallFailed = "api_call_failed";
}

/// <summary>
/// Event categories for analytics grouping
/// </summary>
public static class EventCategories
{
    public const string Conversation = "conversation";
    public const string Profile = "profile";
    public const string Search = "search";
    public const string Grant = "Grant";
    public const string Page = "page";
    public const string Error = "error";
}
