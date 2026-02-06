using GrantMatcher.Shared.DTOs;
using GrantMatcher.Shared.Models;

namespace GrantMatcher.Core.Interfaces;

public interface IEntityMatchingService
{
    /// <summary>
    /// Stores a Grant as an entity in the EntityMatchingAI system
    /// </summary>
    Task<string> StoreGrantEntityAsync(GrantEntity Grant, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads an embedding for a Grant entity
    /// </summary>
    Task UploadEmbeddingAsync(string entityId, float[] embedding, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs hybrid search (filters + vector similarity) to find matching Grants
    /// </summary>
    Task<VectorSearchResponse> SearchGrantsAsync(NonprofitProfile Nonprofit, VectorSearchRequest searchRequest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an entity by ID
    /// </summary>
    Task<EntityResponse?> GetEntityAsync(string entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity
    /// </summary>
    Task DeleteEntityAsync(string entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message in a conversation with an entity (for profile building)
    /// </summary>
    Task<ConversationResponse> SendConversationMessageAsync(string entityId, string message, string? systemPrompt = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves conversation history for an entity
    /// </summary>
    Task<List<ConversationMessage>> GetConversationHistoryAsync(string entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an embedding using EntityMatchingAI's internal OpenAI service
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Nonprofit profile entity for conversational profile building
    /// </summary>
    Task<string> CreateNonprofitEntityAsync(string userId, string name, CancellationToken cancellationToken = default);
}
