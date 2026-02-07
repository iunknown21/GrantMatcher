namespace GrantMatcher.Core.Interfaces;

/// <summary>
/// Service for interacting with Groq AI for fast LLM generation
/// </summary>
public interface IGroqService
{
    /// <summary>
    /// Generates a natural language summary for a grant opportunity
    /// </summary>
    Task<string> GenerateGrantSummaryAsync(
        string title,
        string agency,
        string description,
        decimal? fundingFloor,
        decimal? fundingCeiling,
        string? closeDate,
        List<string> eligibleApplicants,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates text completion using Groq AI
    /// </summary>
    Task<string> GenerateCompletionAsync(
        string prompt,
        string systemMessage = "",
        CancellationToken cancellationToken = default);
}
