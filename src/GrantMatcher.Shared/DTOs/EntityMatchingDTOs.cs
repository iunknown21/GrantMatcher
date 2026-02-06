using System.Text.Json.Serialization;

namespace GrantMatcher.Shared.DTOs;

// DTOs for EntityMatchingAI API integration
public class EntityRequest
{
    [JsonPropertyName("entityType")]
    public int EntityType { get; set; }  // 3 for Product/Service (Grants)

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("attributes")]
    public Dictionary<string, object> Attributes { get; set; } = new();
}

public class EntityResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("entityType")]
    public int EntityType { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("attributes")]
    public Dictionary<string, object> Attributes { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public class EmbeddingUploadRequest
{
    [JsonPropertyName("embedding")]
    public float[] Embedding { get; set; } = Array.Empty<float>();

    [JsonPropertyName("embeddingModel")]
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
}

public class VectorSearchRequest
{
    [JsonPropertyName("query")]
    public string? Query { get; set; }

    [JsonPropertyName("queryEmbedding")]
    public float[]? QueryEmbedding { get; set; }

    [JsonPropertyName("attributeFilters")]
    public FilterGroup? AttributeFilters { get; set; }

    [JsonPropertyName("minSimilarity")]
    public double MinSimilarity { get; set; } = 0.6;

    [JsonPropertyName("limit")]
    public int Limit { get; set; } = 20;

    [JsonPropertyName("includeEmbeddings")]
    public bool IncludeEmbeddings { get; set; } = false;
}

public class FilterGroup
{
    [JsonPropertyName("logicalOperator")]
    public string LogicalOperator { get; set; } = "And";  // "And" or "Or"

    [JsonPropertyName("filters")]
    public List<AttributeFilter> Filters { get; set; } = new();
}

public class AttributeFilter
{
    [JsonPropertyName("fieldPath")]
    public string FieldPath { get; set; } = string.Empty;

    [JsonPropertyName("operator")]
    public string Operator { get; set; } = string.Empty;  // LessThanOrEqual, Contains, etc.

    [JsonPropertyName("value")]
    public object? Value { get; set; }
}

public class VectorSearchResponse
{
    [JsonPropertyName("results")]
    public List<SearchResultItem> Results { get; set; } = new();

    [JsonPropertyName("totalResults")]
    public int TotalResults { get; set; }
}

public class SearchResultItem
{
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("similarity")]
    public double Similarity { get; set; }

    [JsonPropertyName("profile")]
    public EntityResponse? Profile { get; set; }
}

// Conversation DTOs for EntityMatchingAI
public class EntityMatchingConversationResponse
{
    [JsonPropertyName("aiResponse")]
    public string? AiResponse { get; set; }

    [JsonPropertyName("newInsights")]
    public List<EntityMatchingInsight>? NewInsights { get; set; }

    [JsonPropertyName("conversationId")]
    public string? ConversationId { get; set; }
}

public class EntityMatchingInsight
{
    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("insight")]
    public string? Insight { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}

public class EntityMatchingConversationContext
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("entityId")]
    public string? EntityId { get; set; }

    [JsonPropertyName("conversationChunks")]
    public List<ConversationChunk>? ConversationChunks { get; set; }
}

public class ConversationChunk
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; set; }
}
