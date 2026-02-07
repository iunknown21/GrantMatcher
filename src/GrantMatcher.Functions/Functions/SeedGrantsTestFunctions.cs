using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GrantMatcher.Core.Interfaces;
using System.Net;
using System.Text.Json;

namespace GrantMatcher.Functions.Functions;

/// <summary>
/// Test functions to isolate the 404 issue with SeedGrants
/// </summary>
public class SeedGrantsTestFunctions
{
    /// <summary>
    /// Test 1: Minimal function (like HealthCheck)
    /// </summary>
    [Function("SeedGrantsTest1_Minimal")]
    public HttpResponseData Test1_Minimal(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "test/simple")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.WriteString("Test 1: Minimal function works!");
        return response;
    }

    /// <summary>
    /// Test 1b: Try with admin prefix only
    /// </summary>
    [Function("SeedGrantsTest1b_AdminRoute")]
    public HttpResponseData Test1b_AdminRoute(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "admin/test")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.WriteString("Test 1b: Admin route works!");
        return response;
    }

    /// <summary>
    /// Test 1c: Try exact SeedGrants route
    /// </summary>
    [Function("SeedGrantsTest1c_ExactRoute")]
    public HttpResponseData Test1c_ExactRoute(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "admin/grants/testseed")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.WriteString("Test 1c: Exact route pattern works!");
        return response;
    }

    /// <summary>
    /// Test 2: With ILogger
    /// </summary>
    [Function("SeedGrantsTest2_Logger")]
    public HttpResponseData Test2_Logger(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "admin/grants/test2")] HttpRequestData req,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger("SeedGrantsTest2");
        logger.LogInformation("Test 2: Logger injection works!");

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.WriteString("Test 2: Logger works!");
        return response;
    }
}

/// <summary>
/// Test 3: With constructor DI (ILogger + IConfiguration)
/// </summary>
public class SeedGrantsTest3
{
    private readonly ILogger<SeedGrantsTest3> _logger;
    private readonly IConfiguration _configuration;

    public SeedGrantsTest3(
        ILogger<SeedGrantsTest3> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [Function("SeedGrantsTest3_BasicDI")]
    public HttpResponseData Test3_BasicDI(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "admin/grants/test3")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Test 3: Constructor DI works!");

        var dbName = _configuration["CosmosDb:DatabaseName"] ?? "Not configured";

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.WriteString($"Test 3: Basic DI works! DB Name: {dbName}");
        return response;
    }
}

/// <summary>
/// Test 4: Add IOpportunityDataService
/// </summary>
public class SeedGrantsTest4
{
    private readonly ILogger<SeedGrantsTest4> _logger;
    private readonly IOpportunityDataService _grantsService;

    public SeedGrantsTest4(
        ILogger<SeedGrantsTest4> logger,
        IOpportunityDataService grantsService)
    {
        _logger = logger;
        _grantsService = grantsService;
    }

    [Function("SeedGrantsTest4_GrantsService")]
    public HttpResponseData Test4_GrantsService(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "admin/grants/test4")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Test 4: Grants service injected!");

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.WriteString($"Test 4: Grants service works! Type: {_grantsService.GetType().Name}");
        return response;
    }
}

/// <summary>
/// Test 5: Add EntityMatchingService
/// </summary>
public class SeedGrantsTest5
{
    private readonly ILogger<SeedGrantsTest5> _logger;
    private readonly IOpportunityDataService _grantsService;
    private readonly IEntityMatchingService _entityMatchingService;

    public SeedGrantsTest5(
        ILogger<SeedGrantsTest5> logger,
        IOpportunityDataService grantsService,
        IEntityMatchingService entityMatchingService)
    {
        _logger = logger;
        _grantsService = grantsService;
        _entityMatchingService = entityMatchingService;
    }

    [Function("SeedGrantsTest5_EntityMatching")]
    public HttpResponseData Test5_EntityMatching(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "admin/grants/test5")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Test 5: Entity matching service injected!");

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.WriteString($"Test 5: EntityMatching works!");
        return response;
    }
}

/// <summary>
/// Test 6: Add CosmosClient
/// </summary>
public class SeedGrantsTest6
{
    private readonly ILogger<SeedGrantsTest6> _logger;
    private readonly IOpportunityDataService _grantsService;
    private readonly IEntityMatchingService _entityMatchingService;
    private readonly CosmosClient _cosmosClient;
    private readonly Container? _grantsContainer;

    public SeedGrantsTest6(
        ILogger<SeedGrantsTest6> logger,
        IOpportunityDataService grantsService,
        IEntityMatchingService entityMatchingService,
        CosmosClient cosmosClient,
        IConfiguration configuration)
    {
        _logger = logger;
        _grantsService = grantsService;
        _entityMatchingService = entityMatchingService;
        _cosmosClient = cosmosClient;

        try
        {
            var databaseName = configuration["CosmosDb:DatabaseName"] ?? "GrantMatcherDb";
            _grantsContainer = _cosmosClient.GetContainer(databaseName, "Grants");
            _logger.LogInformation("Cosmos container initialized: {Database}/Grants", databaseName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Cosmos container");
            _grantsContainer = null;
        }
    }

    [Function("SeedGrantsTest6_CosmosClient")]
    public HttpResponseData Test6_CosmosClient(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "admin/grants/test6")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Test 6: Cosmos client injected!");

        var containerStatus = _grantsContainer != null ? "Initialized" : "Failed";

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.WriteString($"Test 6: CosmosClient works! Container: {containerStatus}");
        return response;
    }
}

/// <summary>
/// Test 7: Add optional services (Groq, OpenAI) - the problematic ones
/// </summary>
public class SeedGrantsTest7
{
    private readonly ILogger<SeedGrantsTest7> _logger;
    private readonly IOpportunityDataService _grantsService;
    private readonly IEntityMatchingService _entityMatchingService;
    private readonly CosmosClient _cosmosClient;
    private readonly Container _grantsContainer;
    private readonly IGroqService? _groqService;
    private readonly IOpenAIService? _openAIService;

    public SeedGrantsTest7(
        ILogger<SeedGrantsTest7> logger,
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

        _logger.LogInformation("Groq service: {GroqStatus}", _groqService != null ? "Available" : "Not configured");
        _logger.LogInformation("OpenAI service: {OpenAIStatus}", _openAIService != null ? "Available" : "Not configured");

        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "GrantMatcherDb";
        _grantsContainer = _cosmosClient.GetContainer(databaseName, "Grants");
    }

    [Function("SeedGrantsTest7_OptionalServices")]
    public HttpResponseData Test7_OptionalServices(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "admin/grants/test7")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Test 7: All services including optional ones!");

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.WriteString($"Test 7: All services work! Groq: {_groqService != null}, OpenAI: {_openAIService != null}");
        return response;
    }
}
