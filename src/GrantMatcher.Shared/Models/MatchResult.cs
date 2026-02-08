namespace GrantMatcher.Shared.Models;

public class MatchResult
{
    public string GrantId { get; set; } = string.Empty;
    public GrantEntity Grant { get; set; } = null!;

    // Matching scores
    public double SemanticSimilarity { get; set; }  // 0.0 to 1.0
    public double CompositeScore { get; set; }      // Final ranked score

    // Score breakdown
    public ScoreBreakdown Breakdown { get; set; } = new();

    // Eligibility
    public bool MeetsAllRequirements { get; set; }
    public List<string> UnmetRequirements { get; set; } = new();

    // Match metadata
    public DateTime MatchedAt { get; set; }
}

public class ScoreBreakdown
{
    public double SemanticScore { get; set; }        // 50%
    public double MissionAlignmentScore { get; set; } // 20%
    public double AwardAmountScore { get; set; }     // 20%
    public double DeadlineProximityScore { get; set; }  // 10%
}
