using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using GrantMatcher.Shared.Models;
using System.Net;
using System.Text.Json;

namespace GrantMatcher.Functions.Functions;

public class ProfileFunctions
{
    private readonly ILogger<ProfileFunctions> _logger;
    private readonly CosmosClient _cosmosClient;
    private readonly Container _container;

    public ProfileFunctions(ILogger<ProfileFunctions> logger, CosmosClient cosmosClient, IConfiguration configuration)
    {
        _logger = logger;
        _cosmosClient = cosmosClient;

        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "GrantMatcher";
        var containerName = configuration["CosmosDb:Containers:Nonprofits"] ?? "Nonprofits";

        _container = _cosmosClient.GetContainer(databaseName, containerName);
    }

    [Function("CreateProfile")]
    public async Task<HttpResponseData> CreateProfile(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "profiles")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Creating Nonprofit profile");

        try
        {
            var profile = await JsonSerializer.DeserializeAsync<NonprofitProfile>(req.Body);
            if (profile == null)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid profile data");
                return badRequest;
            }

            profile.Id = Guid.NewGuid();
            profile.CreatedAt = DateTime.UtcNow;
            profile.LastModified = DateTime.UtcNow;

            var response = await _container.CreateItemAsync(profile, new PartitionKey(profile.UserId));

            var httpResponse = req.CreateResponse(HttpStatusCode.Created);
            await httpResponse.WriteAsJsonAsync(response.Resource);
            return httpResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating profile");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("GetProfile")]
    public async Task<HttpResponseData> GetProfile(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "profiles/{id}")] HttpRequestData req,
        string id,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Getting profile {ProfileId}", id);

        try
        {
            // For now, we'll query by ID. In production, you'd also need the partition key (userId)
            var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                .WithParameter("@id", id);

            var iterator = _container.GetItemQueryIterator<NonprofitProfile>(query);
            var results = new List<NonprofitProfile>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            if (!results.Any())
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("Profile not found");
                return notFound;
            }

            var httpResponse = req.CreateResponse(HttpStatusCode.OK);
            await httpResponse.WriteAsJsonAsync(results.First());
            return httpResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting profile");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("UpdateProfile")]
    public async Task<HttpResponseData> UpdateProfile(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "profiles/{id}")] HttpRequestData req,
        string id,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Updating profile {ProfileId}", id);

        try
        {
            var profile = await JsonSerializer.DeserializeAsync<NonprofitProfile>(req.Body);
            if (profile == null)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid profile data");
                return badRequest;
            }

            profile.Id = Guid.Parse(id);
            profile.LastModified = DateTime.UtcNow;

            var response = await _container.ReplaceItemAsync(profile, id, new PartitionKey(profile.UserId));

            var httpResponse = req.CreateResponse(HttpStatusCode.OK);
            await httpResponse.WriteAsJsonAsync(response.Resource);
            return httpResponse;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync("Profile not found");
            return notFound;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("DeleteProfile")]
    public async Task<HttpResponseData> DeleteProfile(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "profiles/{id}")] HttpRequestData req,
        string id,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Deleting profile {ProfileId}", id);

        try
        {
            // First, get the profile to obtain the partition key
            var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                .WithParameter("@id", id);

            var iterator = _container.GetItemQueryIterator<NonprofitProfile>(query);
            var results = new List<NonprofitProfile>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            if (!results.Any())
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("Profile not found");
                return notFound;
            }

            var profile = results.First();
            await _container.DeleteItemAsync<NonprofitProfile>(id, new PartitionKey(profile.UserId));

            var httpResponse = req.CreateResponse(HttpStatusCode.NoContent);
            return httpResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting profile");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }
}
