using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using GrantMatcher.Core.Interfaces;
using GrantMatcher.Shared.DTOs;
using System.Net;
using System.Text.Json;

namespace GrantMatcher.Functions.Functions;

public class MatchingFunctions
{
    private readonly ILogger<MatchingFunctions> _logger;
    private readonly IMatchingService _matchingService;

    public MatchingFunctions(ILogger<MatchingFunctions> logger, IMatchingService matchingService)
    {
        _logger = logger;
        _matchingService = matchingService;
    }

    [Function("SearchGrants")]
    public async Task<HttpResponseData> SearchGrants(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "matches/search")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Searching Grants");

        try
        {
            var searchRequest = await JsonSerializer.DeserializeAsync<SearchRequest>(req.Body);
            if (searchRequest == null)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid search request");
                return badRequest;
            }

            var result = await _matchingService.FindGrantsAsync(searchRequest);

            var httpResponse = req.CreateResponse(HttpStatusCode.OK);
            await httpResponse.WriteAsJsonAsync(result);
            return httpResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Grants");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }
}
