using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Cosmos;
using GrantMatcher.Core.Interfaces;
using GrantMatcher.Core.Services;
using GrantMatcher.Shared.Models;
using GrantMatcher.Functions.Middleware;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;

var builder = FunctionsApplication.CreateBuilder(args);

// Middleware configuration removed for compatibility with newer Azure Functions SDK

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Configuration
var configuration = builder.Configuration;

// Cosmos DB
var cosmosConnectionString = configuration["CosmosDb:ConnectionString"]
    ?? throw new InvalidOperationException("CosmosDb:ConnectionString is required");
var databaseName = configuration["CosmosDb:DatabaseName"] ?? "GrantMatcher";

builder.Services.AddSingleton<CosmosClient>(sp =>
{
    return new CosmosClient(cosmosConnectionString, new CosmosClientOptions
    {
        SerializerOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        }
    });
});

// HTTP Clients and Services
var entityMatchingApiKey = configuration["EntityMatchingApi:ApiKey"]
    ?? throw new InvalidOperationException("EntityMatchingApi:ApiKey is required");
var entityMatchingBaseUrl = configuration["EntityMatchingApi:BaseUrl"] ?? "https://entityaiapi.azurewebsites.net";

builder.Services.AddHttpClient<IEntityMatchingService, EntityMatchingService>((sp, client) =>
{
    client.BaseAddress = new Uri(entityMatchingBaseUrl);
})
.ConfigureHttpClient((sp, client) => {})
.AddTypedClient<IEntityMatchingService>((client, sp) =>
{
    return new EntityMatchingService(client, entityMatchingApiKey);
});

// OpenAI is optional - only needed if you want to generate embeddings locally
// EntityMatching API can also handle embedding generation
var openAIApiKey = configuration["OpenAI:ApiKey"];
var embeddingModel = configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";
var chatModel = configuration["OpenAI:ChatModel"] ?? "gpt-4o-mini";

if (!string.IsNullOrEmpty(openAIApiKey))
{
    builder.Services.AddHttpClient<IOpenAIService, OpenAIService>()
    .AddTypedClient<IOpenAIService>((client, sp) =>
    {
        return new OpenAIService(client, openAIApiKey, embeddingModel, chatModel);
    });
}
else
{
    // Provide a null implementation if OpenAI is not configured
    builder.Services.AddScoped<IOpenAIService>(sp => null!);
}

// SimplerGrants Service (for federal grant opportunities)
var simplerGrantsBaseUrl = configuration["SimplerGrants:BaseUrl"] ?? "https://api.simpler.grants.gov/v1";
var simplerGrantsApiKey = configuration["SimplerGrants:ApiKey"];

builder.Services.AddHttpClient<IOpportunityDataService, SimplerGrantsService>((sp, client) =>
{
    client.BaseAddress = new Uri(simplerGrantsBaseUrl);
    if (!string.IsNullOrEmpty(simplerGrantsApiKey))
    {
        client.DefaultRequestHeaders.Add("X-API-Key", simplerGrantsApiKey);
    }
})
.AddTypedClient<IOpportunityDataService>((client, sp) =>
{
    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SimplerGrantsService>>();
    return new SimplerGrantsService(client, logger, simplerGrantsBaseUrl);
});

// Groq AI Service (for fast grant summary generation)
var groqApiKey = configuration["Groq:ApiKey"];
if (!string.IsNullOrEmpty(groqApiKey))
{
    builder.Services.AddHttpClient<IGroqService, GroqService>()
    .AddTypedClient<IGroqService>((client, sp) =>
    {
        var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<GroqService>>();
        return new GroqService(client, groqApiKey, logger);
    });
}
else
{
    // Provide null implementation if Groq is not configured
    builder.Services.AddScoped<IGroqService>(sp => null!);
}

// Caching Services
builder.Services.AddMemoryCache();

// Optional: Add Redis distributed cache if configured
var redisConnection = configuration["Redis:ConnectionString"];
if (!string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "GrantMatcher:";
    });
}

builder.Services.AddSingleton<ICachingService>(sp =>
{
    var memoryCache = sp.GetRequiredService<IMemoryCache>();
    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CachingService>>();
    var distributedCache = sp.GetService<IDistributedCache>();
    return new CachingService(memoryCache, logger, distributedCache);
});

// Performance Monitoring
builder.Services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();

// Core Services
builder.Services.AddScoped<IMatchingService, MatchingService>();
builder.Services.AddScoped<IAnalyticsService>(sp =>
{
    var cosmosClient = sp.GetRequiredService<CosmosClient>();
    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AnalyticsService>>();
    return new AnalyticsService(cosmosClient, logger, databaseName);
});

// Response compression removed - not compatible with isolated worker model

builder.Build().Run();
