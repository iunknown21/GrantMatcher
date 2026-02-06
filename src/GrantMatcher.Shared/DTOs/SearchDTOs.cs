using GrantMatcher.Shared.Models;

namespace GrantMatcher.Shared.DTOs;

public class SearchRequest
{
    public Guid NonprofitId { get; set; }
    public string? Query { get; set; }  // Optional override of profile summary

    // Filters
    public decimal? MinAwardAmount { get; set; }
    public decimal? MaxAwardAmount { get; set; }
    public DateTime? DeadlineAfter { get; set; }
    public DateTime? DeadlineBefore { get; set; }
    public bool? RequiresEssay { get; set; }

    // Pagination
    public int Limit { get; set; } = 20;
    public int Offset { get; set; } = 0;

    // Matching
    public double MinSimilarity { get; set; } = 0.6;
}

public class SearchResponse
{
    public List<MatchResult> Matches { get; set; } = new();
    public int TotalCount { get; set; }
    public SearchMetadata Metadata { get; set; } = new();
}

public class SearchMetadata
{
    public TimeSpan ProcessingTime { get; set; }
    public int FilteredGrants { get; set; }
    public int EligibleGrants { get; set; }
    public string SearchStrategy { get; set; } = string.Empty;
    public bool FromCache { get; set; }
}
