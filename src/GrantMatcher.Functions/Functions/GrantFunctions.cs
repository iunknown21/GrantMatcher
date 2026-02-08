using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using GrantMatcher.Core.Interfaces;
using GrantMatcher.Shared.Models;
using System.Net;
using System.Text.Json;

namespace GrantMatcher.Functions.Functions;

public class GrantFunctions
{
    private readonly ILogger<GrantFunctions> _logger;
    private readonly CosmosClient _cosmosClient;
    private readonly Container _container;
    private readonly IEntityMatchingService _entityMatchingService;
    private readonly IOpenAIService _openAIService;

    public GrantFunctions(
        ILogger<GrantFunctions> logger,
        CosmosClient cosmosClient,
        IConfiguration configuration,
        IEntityMatchingService entityMatchingService,
        IOpenAIService openAIService)
    {
        _logger = logger;
        _cosmosClient = cosmosClient;
        _entityMatchingService = entityMatchingService;
        _openAIService = openAIService;

        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "GrantMatcher";
        var containerName = configuration["CosmosDb:Containers:Grants"] ?? "Grants";

        _container = _cosmosClient.GetContainer(databaseName, containerName);
    }

    [Function("CreateGrant")]
    public async Task<HttpResponseData> CreateGrant(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "Grants")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Creating Grant");

        try
        {
            var Grant = await JsonSerializer.DeserializeAsync<GrantEntity>(req.Body);
            if (Grant == null)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid Grant data");
                return badRequest;
            }

            Grant.id = Guid.NewGuid().ToString();
            Grant.CreatedAt = DateTime.UtcNow;

            // Store in Cosmos DB
            var cosmosResponse = await _container.CreateItemAsync(Grant, new PartitionKey(Grant.Agency));

            // Store in EntityMatchingAI and generate embedding
            var entityId = await _entityMatchingService.StoreGrantEntityAsync(Grant);
            Grant.EntityId = entityId;

            // Generate and upload embedding
            // NOTE: EntityMatchingAI has OpenAI configured internally, but its upload endpoint
            // expects pre-computed embeddings. For now, we generate embeddings here.
            // Future: Request EntityMatchingAI to add auto-embedding generation on entity creation.
            var embedding = await _openAIService.GenerateEmbeddingAsync(Grant.NaturalLanguageSummary);
            await _entityMatchingService.UploadEmbeddingAsync(entityId, embedding);

            // Update with EntityId
            await _container.ReplaceItemAsync(Grant, Grant.id, new PartitionKey(Grant.Agency));

            var httpResponse = req.CreateResponse(HttpStatusCode.Created);
            await httpResponse.WriteAsJsonAsync(Grant);
            return httpResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Grant");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("GetGrant")]
    public async Task<HttpResponseData> GetGrant(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "Grants/{id}")] HttpRequestData req,
        string id,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Getting Grant {GrantId}", id);

        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                .WithParameter("@id", id);

            var iterator = _container.GetItemQueryIterator<GrantEntity>(query);
            var results = new List<GrantEntity>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            if (!results.Any())
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("Grant not found");
                return notFound;
            }

            var httpResponse = req.CreateResponse(HttpStatusCode.OK);
            await httpResponse.WriteAsJsonAsync(results.First());
            return httpResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Grant");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("ListGrants")]
    public async Task<HttpResponseData> ListGrants(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "Grants")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Listing Grants");

        try
        {
            var query = new QueryDefinition("SELECT * FROM c ORDER BY c.deadline ASC");
            var iterator = _container.GetItemQueryIterator<GrantEntity>(query);
            var results = new List<GrantEntity>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            var httpResponse = req.CreateResponse(HttpStatusCode.OK);
            await httpResponse.WriteAsJsonAsync(results);
            return httpResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Grants");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("ImportGrants")]
    public async Task<HttpResponseData> ImportGrants(
        [HttpTrigger(AuthorizationLevel.Admin, "post", Route = "admin/Grants/import")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Importing Grants");

        try
        {
            var Grants = await JsonSerializer.DeserializeAsync<List<GrantEntity>>(req.Body);
            if (Grants == null || !Grants.Any())
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("No Grants provided");
                return badRequest;
            }

            var imported = 0;
            var errors = new List<string>();

            foreach (var Grant in Grants)
            {
                try
                {
                    Grant.id = Guid.NewGuid().ToString();
                    Grant.CreatedAt = DateTime.UtcNow;

                    // Store in Cosmos DB
                    await _container.CreateItemAsync(Grant, new PartitionKey(Grant.Agency));

                    // Store in EntityMatchingAI
                    var entityId = await _entityMatchingService.StoreGrantEntityAsync(Grant);
                    Grant.EntityId = entityId;

                    // Generate and upload embedding
                    var embedding = await _openAIService.GenerateEmbeddingAsync(Grant.NaturalLanguageSummary);
                    await _entityMatchingService.UploadEmbeddingAsync(entityId, embedding);

                    // Update with EntityId
                    await _container.ReplaceItemAsync(Grant, Grant.id, new PartitionKey(Grant.Agency));

                    imported++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Error importing '{Grant.Name}': {ex.Message}");
                }
            }

            var httpResponse = req.CreateResponse(HttpStatusCode.OK);
            await httpResponse.WriteAsJsonAsync(new
            {
                imported,
                total = Grants.Count,
                errors
            });
            return httpResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing Grants");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }
}
