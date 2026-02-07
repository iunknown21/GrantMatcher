using System.Net.Http.Json;
using System.Text.Json;
using GrantMatcher.Core.Interfaces;
using GrantMatcher.Shared.DTOs;
using GrantMatcher.Shared.Models;
using Microsoft.Extensions.Logging;

namespace GrantMatcher.Core.Services;

public class SimplerGrantsService : IOpportunityDataService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SimplerGrantsService> _logger;
    private readonly string _baseUrl;

    public SimplerGrantsService(
        HttpClient httpClient,
        ILogger<SimplerGrantsService> logger,
        string? baseUrl = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = baseUrl ?? "https://api.simpler.grants.gov/v1";
    }

    public async Task<List<GrantEntity>> SearchGrantsAsync(
        string keyword,
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_baseUrl}/opportunities?keyword={Uri.EscapeDataString(keyword)}&limit={limit}&status=posted";
            _logger.LogInformation("Searching Simpler.Grants.gov: {Url}", url);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var apiResponse = await response.Content.ReadFromJsonAsync<SimplerGrantsResponse>(cancellationToken);

            if (apiResponse?.Data == null)
            {
                _logger.LogWarning("No data returned from Simpler.Grants.gov");
                return new List<GrantEntity>();
            }

            _logger.LogInformation("Found {Count} grants from Simpler.Grants.gov", apiResponse.Data.Count);

            return apiResponse.Data.Select(MapToGrantEntity).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Simpler.Grants.gov for keyword: {Keyword}", keyword);
            throw;
        }
    }

    public async Task<GrantEntity?> GetGrantByOpportunityNumberAsync(
        string opportunityNumber,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_baseUrl}/opportunities/{Uri.EscapeDataString(opportunityNumber)}";
            _logger.LogInformation("Fetching grant from Simpler.Grants.gov: {OpportunityNumber}", opportunityNumber);

            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Grant not found: {OpportunityNumber}", opportunityNumber);
                return null;
            }

            var opportunity = await response.Content.ReadFromJsonAsync<SimplerGrantsOpportunity>(cancellationToken);

            if (opportunity == null)
            {
                return null;
            }

            return MapToGrantEntity(opportunity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching grant: {OpportunityNumber}", opportunityNumber);
            throw;
        }
    }

    public async Task<List<GrantEntity>> GetGrantsByCategoryAsync(
        string category,
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_baseUrl}/opportunities?fundingCategory={Uri.EscapeDataString(category)}&limit={limit}&status=posted";
            _logger.LogInformation("Searching Simpler.Grants.gov by category: {Category}", category);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var apiResponse = await response.Content.ReadFromJsonAsync<SimplerGrantsResponse>(cancellationToken);

            if (apiResponse?.Data == null)
            {
                return new List<GrantEntity>();
            }

            return apiResponse.Data.Select(MapToGrantEntity).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching by category: {Category}", category);
            throw;
        }
    }

    public async Task<List<GrantEntity>> GetGrantsByAgencyAsync(
        string agencyCode,
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_baseUrl}/opportunities?agency={Uri.EscapeDataString(agencyCode)}&limit={limit}&status=posted";
            _logger.LogInformation("Searching Simpler.Grants.gov by agency: {AgencyCode}", agencyCode);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var apiResponse = await response.Content.ReadFromJsonAsync<SimplerGrantsResponse>(cancellationToken);

            if (apiResponse?.Data == null)
            {
                return new List<GrantEntity>();
            }

            return apiResponse.Data.Select(MapToGrantEntity).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching by agency: {AgencyCode}", agencyCode);
            throw;
        }
    }

    /// <summary>
    /// Maps Simpler.Grants.gov API response to GrantEntity model
    /// </summary>
    private GrantEntity MapToGrantEntity(SimplerGrantsOpportunity opportunity)
    {
        return new GrantEntity
        {
            Id = Guid.NewGuid(),
            Name = opportunity.OpportunityTitle,
            OpportunityNumber = opportunity.OpportunityNumber,
            Description = opportunity.Summary?.Description ?? string.Empty,

            // Agency
            Agency = opportunity.Agency?.Name ?? string.Empty,
            SubAgency = opportunity.Agency?.SubAgency ?? string.Empty,
            AgencyCode = opportunity.Agency?.Code ?? string.Empty,

            // Eligibility
            ApplicantTypes = opportunity.ApplicantEligibility ?? new List<string>(),
            FundingCategories = opportunity.Summary?.FundingCategories ?? new List<string>(),
            EligibleStates = new List<string>(),  // API may provide this in additional fields
            EligibleCounties = new List<string>(),

            // Award details
            AwardCeiling = opportunity.AwardCeiling,
            AwardFloor = opportunity.AwardFloor,
            EstimatedTotalFunding = opportunity.EstimatedTotalProgramFunding,
            ExpectedNumberOfAwards = opportunity.ExpectedNumberOfAwards,
            FundingInstrument = opportunity.FundingInstrumentType ?? string.Empty,
            CostSharing = opportunity.CostSharing ?? "Not specified",

            // Dates
            PostDate = ParseDate(opportunity.PostDate),
            CloseDate = ParseDate(opportunity.CloseDate),
            ArchiveDate = ParseDate(opportunity.ArchiveDate),

            // Links
            ApplicationUrl = opportunity.OpportunityUrl ?? string.Empty,
            GrantsGovUrl = opportunity.GrantsGovUrl ?? $"https://www.grants.gov/search-results-detail/{opportunity.OpportunityNumber}",

            // Additional info
            CFDANumber = opportunity.AssistanceListing?.CFDANumber ?? string.Empty,
            FundingActivity = opportunity.AssistanceListing?.ProgramTitle ?? string.Empty,
            IsForecasted = opportunity.OpportunityStatus?.ToLower() == "forecasted",
            Version = "1",

            // Generated fields
            NaturalLanguageSummary = GenerateNaturalLanguageSummary(opportunity),
            Keywords = ExtractKeywords(opportunity),

            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        };
    }

    private DateTime? ParseDate(string? dateString)
    {
        if (string.IsNullOrEmpty(dateString))
        {
            return null;
        }

        if (DateTime.TryParse(dateString, out var date))
        {
            return date;
        }

        return null;
    }

    private string GenerateNaturalLanguageSummary(SimplerGrantsOpportunity opportunity)
    {
        var parts = new List<string>();

        if (opportunity.Summary?.FundingCategories?.Any() == true)
        {
            parts.Add($"Supporting {string.Join(", ", opportunity.Summary.FundingCategories.Take(3))}");
        }

        if (opportunity.ApplicantEligibility?.Any() == true)
        {
            parts.Add($"for {string.Join(", ", opportunity.ApplicantEligibility.Take(3))}");
        }

        if (opportunity.Agency?.Name != null)
        {
            parts.Add($"through {opportunity.Agency.Name}");
        }

        if (opportunity.AwardCeiling.HasValue)
        {
            parts.Add($"with awards up to ${opportunity.AwardCeiling.Value:N0}");
        }

        return parts.Any() ? string.Join(" ", parts) : opportunity.Summary?.Description ?? string.Empty;
    }

    private List<string> ExtractKeywords(SimplerGrantsOpportunity opportunity)
    {
        var keywords = new List<string>();

        if (opportunity.Summary?.FundingCategories != null)
        {
            keywords.AddRange(opportunity.Summary.FundingCategories);
        }

        if (opportunity.ApplicantEligibility != null)
        {
            keywords.AddRange(opportunity.ApplicantEligibility);
        }

        if (!string.IsNullOrEmpty(opportunity.Agency?.Name))
        {
            keywords.Add(opportunity.Agency.Name);
        }

        return keywords.Distinct().ToList();
    }

    /// <summary>
    /// Fetches all active grants from Simpler.Grants.gov with pagination
    /// </summary>
    public async Task<List<GrantEntity>> GetAllActiveGrantsAsync(
        int maxResults = 1000,
        CancellationToken cancellationToken = default)
    {
        var allGrants = new List<GrantEntity>();
        int pageSize = 100; // API limit per request
        int offset = 0;

        try
        {
            while (allGrants.Count < maxResults)
            {
                var url = $"{_baseUrl}/opportunities?status=posted&limit={pageSize}&offset={offset}";
                _logger.LogInformation("Fetching grants page: offset={Offset}, limit={Limit}", offset, pageSize);

                var response = await _httpClient.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch page at offset {Offset}: {StatusCode}", offset, response.StatusCode);
                    break;
                }

                var apiResponse = await response.Content.ReadFromJsonAsync<SimplerGrantsResponse>(cancellationToken);

                if (apiResponse?.Data == null || !apiResponse.Data.Any())
                {
                    _logger.LogInformation("No more grants found. Total fetched: {Count}", allGrants.Count);
                    break;
                }

                var grants = apiResponse.Data.Select(MapToGrantEntity).ToList();
                allGrants.AddRange(grants);

                _logger.LogInformation("Fetched {Count} grants, total so far: {Total}", grants.Count, allGrants.Count);

                // If we got fewer results than page size, we've reached the end
                if (grants.Count < pageSize)
                {
                    break;
                }

                offset += pageSize;

                // Rate limiting: small delay between requests
                await Task.Delay(500, cancellationToken);
            }

            _logger.LogInformation("Finished fetching grants. Total: {Count}", allGrants.Count);
            return allGrants.Take(maxResults).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all active grants");
            throw;
        }
    }

    /// <summary>
    /// Fetches grants modified since a specific date (for daily sync)
    /// </summary>
    public async Task<List<GrantEntity>> GetRecentlyModifiedGrantsAsync(
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Note: Simpler.Grants.gov API may not have a lastModified filter
            // We'll fetch recent grants and filter by postDate
            var sinceDate = since.ToString("yyyy-MM-dd");
            var url = $"{_baseUrl}/opportunities?status=posted&sortBy=postDate&sortOrder=desc&limit=200";

            _logger.LogInformation("Fetching grants modified since {Date}", sinceDate);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var apiResponse = await response.Content.ReadFromJsonAsync<SimplerGrantsResponse>(cancellationToken);

            if (apiResponse?.Data == null)
            {
                return new List<GrantEntity>();
            }

            // Filter by postDate
            var recentGrants = apiResponse.Data
                .Where(g => ParseDate(g.PostDate) >= since)
                .Select(MapToGrantEntity)
                .ToList();

            _logger.LogInformation("Found {Count} grants modified since {Date}", recentGrants.Count, sinceDate);

            return recentGrants;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching recently modified grants");
            throw;
        }
    }
}
