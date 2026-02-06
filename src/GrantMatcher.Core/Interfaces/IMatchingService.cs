using GrantMatcher.Shared.DTOs;
using GrantMatcher.Shared.Models;

namespace GrantMatcher.Core.Interfaces;

public interface IMatchingService
{
    /// <summary>
    /// Finds and ranks Grants for a Nonprofit using hybrid search
    /// </summary>
    Task<SearchResponse> FindGrantsAsync(SearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates composite match score for a Grant-Nonprofit pair
    /// </summary>
    MatchResult CalculateMatchScore(NonprofitProfile Nonprofit, GrantEntity Grant, double semanticSimilarity);

    /// <summary>
    /// Checks if Nonprofit meets all eligibility requirements
    /// </summary>
    (bool meetsAll, List<string> unmetRequirements) CheckEligibility(NonprofitProfile Nonprofit, GrantEntity Grant);
}
