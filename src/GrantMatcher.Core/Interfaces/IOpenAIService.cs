using GrantMatcher.Shared.DTOs;

namespace GrantMatcher.Core.Interfaces;

public interface IOpenAIService
{
    /// <summary>
    /// Generates an embedding for text using OpenAI's embedding model
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a conversation turn for profile building
    /// </summary>
    Task<ConversationResponse> ProcessConversationAsync(ConversationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a natural language summary from structured profile data
    /// </summary>
    Task<string> GenerateProfileSummaryAsync(Dictionary<string, object> profileData, CancellationToken cancellationToken = default);
}
