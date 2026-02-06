namespace GrantMatcher.Shared.Models;

public class AppConfiguration
{
    public EntityMatchingAIConfig EntityMatchingAI { get; set; } = new();
    public OpenAIConfig OpenAI { get; set; } = new();
    public CosmosDbConfig CosmosDb { get; set; } = new();
}

public class EntityMatchingAIConfig
{
    public string BaseUrl { get; set; } = "https://profilematching-apim.azure-api.net/api/v1";
    public string ApiKey { get; set; } = string.Empty;
}

public class OpenAIConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public string ChatModel { get; set; } = "gpt-4o-mini";
}

public class CosmosDbConfig
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "GrantMatcher";
    public ContainerNames Containers { get; set; } = new();
}

public class ContainerNames
{
    public string Nonprofits { get; set; } = "Nonprofits";
    public string Grants { get; set; } = "Grants";
    public string Matches { get; set; } = "matches";
    public string SavedGrants { get; set; } = "saved-Grants";
}
