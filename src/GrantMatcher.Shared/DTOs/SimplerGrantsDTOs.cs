namespace GrantMatcher.Shared.DTOs;

/// <summary>
/// Response from Simpler.Grants.gov API search endpoint
/// </summary>
public class SimplerGrantsResponse
{
    public int Total { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
    public List<SimplerGrantsOpportunity> Data { get; set; } = new();
}

/// <summary>
/// Individual grant opportunity from Simpler.Grants.gov
/// </summary>
public class SimplerGrantsOpportunity
{
    public string OpportunityId { get; set; } = string.Empty;
    public string OpportunityNumber { get; set; } = string.Empty;
    public string OpportunityTitle { get; set; } = string.Empty;
    public string OpportunityStatus { get; set; } = string.Empty;

    public SimplerGrantsSummary Summary { get; set; } = new();
    public SimplerGrantsAgency Agency { get; set; } = new();
    public SimplerGrantsAssistanceListing AssistanceListing { get; set; } = new();

    // Award details
    public decimal? AwardCeiling { get; set; }
    public decimal? AwardFloor { get; set; }
    public decimal? EstimatedTotalProgramFunding { get; set; }
    public int? ExpectedNumberOfAwards { get; set; }

    // Dates
    public string? PostDate { get; set; }
    public string? CloseDate { get; set; }
    public string? ArchiveDate { get; set; }

    // Eligibility
    public List<string> ApplicantEligibility { get; set; } = new();
    public List<string> FundingCategories { get; set; } = new();
    public string? FundingInstrumentType { get; set; }
    public string? CostSharing { get; set; }

    // Links
    public string? OpportunityUrl { get; set; }
    public string? GrantsGovUrl { get; set; }
}

public class SimplerGrantsSummary
{
    public string Description { get; set; } = string.Empty;
    public string AdditionalInformation { get; set; } = string.Empty;
    public List<string> FundingCategories { get; set; } = new();
}

public class SimplerGrantsAgency
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SubAgency { get; set; }
}

public class SimplerGrantsAssistanceListing
{
    public string? CFDANumber { get; set; }
    public string? ProgramTitle { get; set; }
}

/// <summary>
/// Search parameters for Simpler.Grants.gov API
/// </summary>
public class SimplerGrantsSearchRequest
{
    public string? Keyword { get; set; }
    public List<string>? FundingCategories { get; set; }
    public List<string>? ApplicantTypes { get; set; }
    public List<string>? Agencies { get; set; }
    public string? Status { get; set; }  // "posted", "forecasted", "closed", "archived"
    public int Limit { get; set; } = 25;
    public int Offset { get; set; } = 0;
}
