namespace GrantMatcher.Shared.DTOs;

public class ConversationMessage
{
    public string Role { get; set; } = string.Empty;  // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class ConversationRequest
{
    public Guid NonprofitId { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<ConversationMessage> History { get; set; } = new();
}

public class ConversationResponse
{
    public string Reply { get; set; } = string.Empty;
    public List<ConversationMessage> UpdatedHistory { get; set; } = new();
    public ExtractedProfileData? ExtractedData { get; set; }
    public bool ProfileComplete { get; set; }
}

public class ExtractedProfileData
{
    public string? OrganizationName { get; set; }
    public string? EIN { get; set; }
    public string? OrganizationType { get; set; }
    public string? MissionStatement { get; set; }
    public List<string>? ServiceAreas { get; set; }
    public List<string>? FundingCategories { get; set; }
    public decimal? AnnualBudget { get; set; }
    public string? ProfileSummary { get; set; }
}
