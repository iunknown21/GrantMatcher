namespace GrantMatcher.Shared.Models;

public class SavedGrant
{
    public Guid Id { get; set; }
    public Guid NonprofitId { get; set; }
    public Guid GrantId { get; set; }

    public ApplicationStatus Status { get; set; }
    public DateTime SavedAt { get; set; }
    public DateTime? ApplicationStartedAt { get; set; }
    public DateTime? ApplicationSubmittedAt { get; set; }

    public string? Notes { get; set; }
}

public enum ApplicationStatus
{
    NotStarted,
    InProgress,
    Submitted,
    Awarded,
    Declined
}
