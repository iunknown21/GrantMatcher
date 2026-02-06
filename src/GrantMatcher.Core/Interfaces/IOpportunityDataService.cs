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
}
