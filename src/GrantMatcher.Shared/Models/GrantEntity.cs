using System.Text.Json.Serialization;

namespace GrantMatcher.Shared.Models;

public class GrantEntity
{
    // Cosmos DB requires "id" field as lowercase string
    public string id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;  // Opportunity Title
    public string OpportunityNumber { get; set; } = string.Empty;  // Unique federal grant number
    public string Description { get; set; } = string.Empty;  // Full description

    // Agency Information
    public string Agency { get; set; } = string.Empty;  // NSF, DOE, EPA, etc.
    public string SubAgency { get; set; } = string.Empty;
    public string AgencyCode { get; set; } = string.Empty;

    // Eligibility Criteria (CRITICAL for filtering)
    public List<string> ApplicantTypes { get; set; } = new();  // State, Nonprofit, IHE, Tribal, etc.
    public List<string> FundingCategories { get; set; } = new();  // Education, Health, Environment, etc.
    public List<string> EligibleStates { get; set; } = new();  // Empty means all states eligible
    public List<string> EligibleCounties { get; set; } = new();

    // Award Details
    public decimal? AwardCeiling { get; set; }  // Maximum award amount
    public decimal? AwardFloor { get; set; }  // Minimum award amount
    public decimal? EstimatedTotalFunding { get; set; }
    public int? ExpectedNumberOfAwards { get; set; }
    public string FundingInstrument { get; set; } = string.Empty;  // Grant, Cooperative Agreement, etc.
    public string CostSharing { get; set; } = string.Empty;  // Required, Not Required

    // Important Dates
    public DateTime? PostDate { get; set; }
    public DateTime? CloseDate { get; set; }
    public DateTime? ArchiveDate { get; set; }

    // Application Details
    public string ApplicationUrl { get; set; } = string.Empty;  // Link to Grants.gov
    public string GrantsGovUrl { get; set; } = string.Empty;
    public List<string> RequiredDocuments { get; set; } = new();

    // Additional Info
    public string CFDANumber { get; set; } = string.Empty;  // Catalog of Federal Domestic Assistance
    public string FundingActivity { get; set; } = string.Empty;  // Research, Training, etc.
    public bool IsForecasted { get; set; }  // True if not yet officially posted
    public string Version { get; set; } = string.Empty;  // Version number if amended

    // For vector search and AI matching
    public string NaturalLanguageSummary { get; set; } = string.Empty;  // "Supporting environmental education in rural areas..."
    public List<string> Keywords { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }

    // Entity matching
    public string? EntityId { get; set; }  // ID in EntityMatchingAI system

    // Cosmos DB Time-to-Live (in seconds, -1 = never expire, null = use container default)
    public int? ttl { get; set; }  // Lowercase to match Cosmos DB convention
}
