using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GrantMatcher.Core.Interfaces;
using GrantMatcher.Shared.Models;
using System.Net;
using System.Text.Json;
using System.Diagnostics;

namespace GrantMatcher.Functions.Functions;

public class GrantSyncFunctions
{
    private readonly ILogger<GrantSyncFunctions> _logger;
    private readonly IOpportunityDataService _grantsService;
    private readonly IGroqService? _groqService;
    private readonly IOpenAIService? _openAIService;
    private readonly IEntityMatchingService _entityMatchingService;
    private readonly CosmosClient _cosmosClient;
    private readonly Container _grantsContainer;
    private readonly int _ttlDays = 90; // Grants expire after 90 days

    public GrantSyncFunctions(
        ILogger<GrantSyncFunctions> logger,
        IOpportunityDataService grantsService,
        IEntityMatchingService entityMatchingService,
        CosmosClient cosmosClient,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _grantsService = grantsService;
        _entityMatchingService = entityMatchingService;
        _cosmosClient = cosmosClient;

        // Try to get optional services
        _groqService = serviceProvider.GetService<IGroqService>();
        _openAIService = serviceProvider.GetService<IOpenAIService>();

        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "GrantMatcherDb";
        _grantsContainer = _cosmosClient.GetContainer(databaseName, "Grants");
    }

    /// <summary>
    /// Initial seed: Fetch all active grants from Grants.gov and store them
    /// HTTP Trigger - Run manually once
    /// </summary>
    [Function("SeedGrants")]
    public async Task<HttpResponseData> SeedGrants(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "management/grants/seed")] HttpRequestData req,
        FunctionContext executionContext)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("=== Starting Grant Seed Process ===");

        var stats = new SeedStatistics();

        try
        {
            // Fetch all active grants
            _logger.LogInformation("Fetching grants from Simpler.Grants.gov...");
            var grants = await _grantsService.GetAllActiveGrantsAsync(maxResults: 1000);
            stats.TotalFetched = grants.Count;
            _logger.LogInformation("Fetched {Count} grants from API", grants.Count);

            // Filter for nonprofits
            var nonprofitGrants = grants.Where(g =>
                g.ApplicantTypes.Any(t =>
                    t.Contains("Nonprofit", StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("501(c)(3)", StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("501c3", StringComparison.OrdinalIgnoreCase))
            ).ToList();

            stats.TotalFiltered = nonprofitGrants.Count;
            _logger.LogInformation("Filtered to {Count} grants eligible for nonprofits", nonprofitGrants.Count);

            // Process each grant
            foreach (var grant in nonprofitGrants)
            {
                try
                {
                    await ProcessGrantAsync(grant, isUpdate: false);
                    stats.Successful++;

                    if (stats.Successful % 10 == 0)
                    {
                        _logger.LogInformation("Progress: {Success}/{Total} grants processed", stats.Successful, nonprofitGrants.Count);
                    }
                }
                catch (Exception ex)
                {
                    stats.Failed++;
                    stats.Errors.Add($"{grant.OpportunityNumber}: {ex.Message}");
                    _logger.LogError(ex, "Failed to process grant {OpportunityNumber}", grant.OpportunityNumber);

                    // Continue processing remaining grants
                    if (stats.FailureRate > 0.1) // More than 10% failure rate
                    {
                        _logger.LogError("High failure rate detected ({Rate:P}), stopping seed", stats.FailureRate);
                        break;
                    }
                }
            }

            stopwatch.Stop();
            stats.DurationSeconds = stopwatch.Elapsed.TotalSeconds;

            _logger.LogInformation(
                "=== Seed Complete === Fetched: {Fetched}, Filtered: {Filtered}, Success: {Success}, Failed: {Failed}, Duration: {Duration}s",
                stats.TotalFetched, stats.TotalFiltered, stats.Successful, stats.Failed, stats.DurationSeconds);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(stats);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during grant seed");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Seed failed: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Daily sync: Update grants modified in the last 48 hours
    /// Timer Trigger - Runs daily at 2 AM UTC
    /// </summary>
    [Function("DailySyncGrants")]
    public async Task DailySyncGrants(
        [TimerTrigger("0 0 2 * * *")] TimerInfo timerInfo,
        FunctionContext executionContext)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("=== Starting Daily Grant Sync ===");

        var stats = new SyncStatistics();

        try
        {
            // Fetch grants modified in last 48 hours
            var since = DateTime.UtcNow.AddHours(-48);
            _logger.LogInformation("Fetching grants modified since {Since}", since);

            var recentGrants = await _grantsService.GetRecentlyModifiedGrantsAsync(since);
            stats.TotalFetched = recentGrants.Count;
            _logger.LogInformation("Found {Count} recently modified grants", recentGrants.Count);

            foreach (var grant in recentGrants)
            {
                try
                {
                    // Check if grant already exists
                    var existingGrant = await GetExistingGrantAsync(grant.OpportunityNumber);

                    if (existingGrant == null)
                    {
                        // New grant
                        await ProcessGrantAsync(grant, isUpdate: false);
                        stats.NewGrants++;
                    }
                    else
                    {
                        // Check if description changed (requires new summary/embedding)
                        if (existingGrant.Description != grant.Description)
                        {
                            await ProcessGrantAsync(grant, isUpdate: true);
                            stats.UpdatedGrants++;
                        }
                        else
                        {
                            // Just update metadata
                            await UpdateGrantMetadataAsync(existingGrant, grant);
                            stats.MetadataOnlyUpdates++;
                        }
                    }

                    stats.Successful++;
                }
                catch (Exception ex)
                {
                    stats.Failed++;
                    _logger.LogError(ex, "Failed to sync grant {OpportunityNumber}", grant.OpportunityNumber);
                }
            }

            stopwatch.Stop();
            stats.DurationSeconds = stopwatch.Elapsed.TotalSeconds;

            _logger.LogInformation(
                "=== Sync Complete === New: {New}, Updated: {Updated}, Metadata: {Metadata}, Failed: {Failed}, Duration: {Duration}s",
                stats.NewGrants, stats.UpdatedGrants, stats.MetadataOnlyUpdates, stats.Failed, stats.DurationSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during daily grant sync");
        }
    }

    /// <summary>
    /// Process a single grant: generate summary, embedding, and store
    /// </summary>
    private async Task ProcessGrantAsync(GrantEntity grant, bool isUpdate)
    {
        _logger.LogInformation("Processing grant {OpportunityNumber}: {Title}", grant.OpportunityNumber, grant.Name);

        // Generate AI summary if Groq is available
        if (_groqService != null && !string.IsNullOrEmpty(grant.Description))
        {
            try
            {
                grant.NaturalLanguageSummary = await _groqService.GenerateGrantSummaryAsync(
                    grant.Name,
                    grant.Agency,
                    grant.Description,
                    grant.AwardFloor,
                    grant.AwardCeiling,
                    grant.CloseDate?.ToString("MMMM d, yyyy"),
                    grant.ApplicantTypes);

                _logger.LogInformation("Generated AI summary for {OpportunityNumber}", grant.OpportunityNumber);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate AI summary for {OpportunityNumber}, using basic summary", grant.OpportunityNumber);
                // Fall back to basic summary (already set by SimplerGrantsService)
            }
        }

        // Store in EntityMatching API for vector search
        try
        {
            var entityId = await _entityMatchingService.StoreGrantEntityAsync(grant);
            grant.EntityId = entityId;

            // Generate and upload embedding if OpenAI is available
            if (_openAIService != null)
            {
                var summaryText = !string.IsNullOrEmpty(grant.NaturalLanguageSummary)
                    ? grant.NaturalLanguageSummary
                    : grant.Description.Substring(0, Math.Min(grant.Description.Length, 1000));

                var embedding = await _openAIService.GenerateEmbeddingAsync(summaryText);
                await _entityMatchingService.UploadEmbeddingAsync(entityId, embedding);

                _logger.LogInformation("Generated embedding for {OpportunityNumber}", grant.OpportunityNumber);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store grant in EntityMatching API: {OpportunityNumber}", grant.OpportunityNumber);
            // Continue anyway - we can still store in Cosmos DB
        }

        // Store in Cosmos DB with TTL
        grant.LastUpdated = DateTime.UtcNow;
        if (!isUpdate)
        {
            grant.CreatedAt = DateTime.UtcNow;
        }

        var itemWithTtl = new
        {
            grant.Id,
            grant.Name,
            grant.OpportunityNumber,
            grant.Description,
            grant.Agency,
            grant.SubAgency,
            grant.AgencyCode,
            grant.ApplicantTypes,
            grant.FundingCategories,
            grant.EligibleStates,
            grant.EligibleCounties,
            grant.AwardCeiling,
            grant.AwardFloor,
            grant.EstimatedTotalFunding,
            grant.ExpectedNumberOfAwards,
            grant.FundingInstrument,
            grant.CostSharing,
            grant.PostDate,
            grant.CloseDate,
            grant.ArchiveDate,
            grant.ApplicationUrl,
            grant.GrantsGovUrl,
            grant.RequiredDocuments,
            grant.CFDANumber,
            grant.FundingActivity,
            grant.IsForecasted,
            grant.Version,
            grant.NaturalLanguageSummary,
            grant.Keywords,
            grant.CreatedAt,
            grant.LastUpdated,
            grant.EntityId,
            ttl = _ttlDays * 24 * 60 * 60 // Convert days to seconds
        };

        await _grantsContainer.UpsertItemAsync(itemWithTtl, new PartitionKey(grant.Agency));

        _logger.LogInformation("Stored grant {OpportunityNumber} in Cosmos DB with {TTL} day TTL",
            grant.OpportunityNumber, _ttlDays);
    }

    /// <summary>
    /// Get existing grant from Cosmos DB
    /// </summary>
    private async Task<GrantEntity?> GetExistingGrantAsync(string opportunityNumber)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.OpportunityNumber = @oppNum")
                .WithParameter("@oppNum", opportunityNumber);

            var iterator = _grantsContainer.GetItemQueryIterator<GrantEntity>(query);
            var results = await iterator.ReadNextAsync();

            return results.FirstOrDefault();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Update grant metadata without regenerating summary/embedding
    /// </summary>
    private async Task UpdateGrantMetadataAsync(GrantEntity existing, GrantEntity updated)
    {
        existing.CloseDate = updated.CloseDate;
        existing.ExpectedNumberOfAwards = updated.ExpectedNumberOfAwards;
        existing.EstimatedTotalFunding = updated.EstimatedTotalFunding;
        existing.Version = updated.Version;
        existing.LastUpdated = DateTime.UtcNow;

        await _grantsContainer.ReplaceItemAsync(existing, existing.Id.ToString(), new PartitionKey(existing.Agency));

        _logger.LogInformation("Updated metadata for {OpportunityNumber}", existing.OpportunityNumber);
    }
}

// Statistics classes
public class SeedStatistics
{
    public int TotalFetched { get; set; }
    public int TotalFiltered { get; set; }
    public int Successful { get; set; }
    public int Failed { get; set; }
    public double DurationSeconds { get; set; }
    public List<string> Errors { get; set; } = new();

    public double FailureRate => (Successful + Failed) > 0 ? (double)Failed / (Successful + Failed) : 0;
}

public class SyncStatistics
{
    public int TotalFetched { get; set; }
    public int NewGrants { get; set; }
    public int UpdatedGrants { get; set; }
    public int MetadataOnlyUpdates { get; set; }
    public int Successful { get; set; }
    public int Failed { get; set; }
    public double DurationSeconds { get; set; }
}
