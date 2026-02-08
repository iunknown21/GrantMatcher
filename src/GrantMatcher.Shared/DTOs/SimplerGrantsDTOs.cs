using System.Text.Json.Serialization;

namespace GrantMatcher.Shared.DTOs;

/// <summary>
/// Response from Traditional Grants.gov API search endpoint
/// https://apply07.grants.gov/grantsws/rest/opportunities/search/
/// </summary>
public class SimplerGrantsResponse
{
    [JsonPropertyName("hitCount")]
    public int HitCount { get; set; }

    [JsonPropertyName("startRecord")]
    public int StartRecord { get; set; }

    [JsonPropertyName("oppHits")]
    public List<GrantsGovOpportunityHit> OppHits { get; set; } = new();

    // For backwards compatibility with our code that expects "Data"
    [JsonIgnore]
    public List<SimplerGrantsOpportunity> Data => OppHits.Select(hit => new SimplerGrantsOpportunity
    {
        OpportunityId = hit.Id,
        OpportunityNumber = hit.Number,
        OpportunityTitle = hit.Title,
        OpportunityStatus = hit.OppStatus,
        Agency = new SimplerGrantsAgency
        {
            Code = hit.AgencyCode,
            Name = hit.Agency
        },
        PostDate = hit.OpenDate,
        CloseDate = hit.CloseDate,
        AssistanceListing = new SimplerGrantsAssistanceListing
        {
            CFDANumber = hit.CfdaList?.FirstOrDefault()
        }
    }).ToList();
}

/// <summary>
/// Individual opportunity hit from Grants.gov search results (minimal data)
/// To get full details, use the opportunity detail endpoint
/// </summary>
public class GrantsGovOpportunityHit
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("number")]
    public string Number { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("agencyCode")]
    public string AgencyCode { get; set; } = string.Empty;

    [JsonPropertyName("agency")]
    public string Agency { get; set; } = string.Empty;

    [JsonPropertyName("openDate")]
    public string? OpenDate { get; set; }

    [JsonPropertyName("closeDate")]
    public string? CloseDate { get; set; }

    [JsonPropertyName("oppStatus")]
    public string OppStatus { get; set; } = string.Empty;

    [JsonPropertyName("docType")]
    public string? DocType { get; set; }

    [JsonPropertyName("cfdaList")]
    public List<string>? CfdaList { get; set; }
}

/// <summary>
/// Full grant opportunity details from Simpler.Grants.gov or Grants.gov detail endpoint
/// This is used for mapping to our GrantEntity model
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
