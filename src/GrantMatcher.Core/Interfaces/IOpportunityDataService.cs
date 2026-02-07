using GrantMatcher.Shared.Models;

namespace GrantMatcher.Core.Interfaces;

/// <summary>
/// Generic interface for grant opportunity data sources
/// </summary>
public interface IOpportunityDataService
{
    /// <summary>
    /// Search for grant opportunities
    /// </summary>
    Task<List<GrantEntity>> SearchGrantsAsync(string keyword, int limit = 25, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific grant by opportunity number
    /// </summary>
    Task<GrantEntity?> GetGrantByOpportunityNumberAsync(string opportunityNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get grants by funding category
    /// </summary>
    Task<List<GrantEntity>> GetGrantsByCategoryAsync(string category, int limit = 25, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get grants by agency
    /// </summary>
    Task<List<GrantEntity>> GetGrantsByAgencyAsync(string agencyCode, int limit = 25, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetch all active grants (for seeding)
    /// </summary>
    Task<List<GrantEntity>> GetAllActiveGrantsAsync(int maxResults = 1000, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetch grants modified since a specific date (for daily sync)
    /// </summary>
    Task<List<GrantEntity>> GetRecentlyModifiedGrantsAsync(DateTime since, CancellationToken cancellationToken = default);
}
