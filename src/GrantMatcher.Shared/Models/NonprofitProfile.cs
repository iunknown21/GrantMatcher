namespace GrantMatcher.Shared.Models;

public class NonprofitProfile
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    // Organization Information
    public string OrganizationName { get; set; } = string.Empty;
    public string EIN { get; set; } = string.Empty;  // Employer Identification Number
    public string Email { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;

    // Organization Type
    public string OrganizationType { get; set; } = string.Empty;  // 501(c)(3), Educational, Tribal, etc.
    public List<string> ApplicantTypes { get; set; } = new();  // "State government", "Nonprofit", "IHE", etc.

    // Mission & Focus
    public string MissionStatement { get; set; } = string.Empty;
    public List<string> FundingCategories { get; set; } = new();  // Education, Health, Environment, etc.
    public List<string> ServiceAreas { get; set; } = new();  // Geographic areas served
    public List<string> PopulationsServed { get; set; } = new();  // Youth, Seniors, Veterans, etc.

    // Financial
    public decimal AnnualBudget { get; set; }
    public string BudgetRange { get; set; } = string.Empty;  // "Under $100K", "$100K-$500K", "$500K-$1M", "$1M+"
    public decimal? TypicalProjectBudget { get; set; }

    // Grant History
    public bool HasReceivedFederalGrants { get; set; }
    public List<string> PastGrantAgencies { get; set; } = new();  // EPA, DOE, NSF, etc.
    public List<string> ProjectTypes { get; set; } = new();  // Research, Infrastructure, Capacity Building, etc.

    // Capacity
    public int FullTimeStaff { get; set; }
    public bool HasGrantWriter { get; set; }
    public bool HasFinancialManagementSystem { get; set; }

    // Generated fields
    public string? ProfileSummary { get; set; }  // Natural language summary for embedding
    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }

    // Entity matching
    public string? EntityId { get; set; }  // ID in EntityMatchingAI system
}
