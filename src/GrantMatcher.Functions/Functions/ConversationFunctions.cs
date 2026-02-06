using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using GrantMatcher.Core.Interfaces;
using GrantMatcher.Shared.DTOs;
using System.Net;
using System.Text.Json;

namespace GrantMatcher.Functions.Functions;

public class ConversationFunctions
{
    private readonly ILogger<ConversationFunctions> _logger;
    private readonly IOpenAIService _openAIService;
    private readonly IEntityMatchingService _entityMatchingService;

    public ConversationFunctions(
        ILogger<ConversationFunctions> logger,
        IOpenAIService openAIService,
        IEntityMatchingService entityMatchingService)
    {
        _logger = logger;
        _openAIService = openAIService;
        _entityMatchingService = entityMatchingService;
    }

    [Function("ProcessConversation")]
    public async Task<HttpResponseData> ProcessConversation(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "conversation")] HttpRequestData req,
        FunctionContext executionContext)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Processing conversation using EntityMatchingAI");

        try
        {
            // Validate request body
            if (req.Body == null || req.Body.Length == 0)
            {
                _logger.LogWarning("Empty request body received");
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Request body is required" });
                return badRequest;
            }

            var conversationRequest = await JsonSerializer.DeserializeAsync<ConversationRequest>(req.Body);

            if (conversationRequest == null)
            {
                _logger.LogWarning("Failed to deserialize conversation request");
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Invalid conversation request format" });
                return badRequest;
            }

            // Validate message content
            if (string.IsNullOrWhiteSpace(conversationRequest.Message))
            {
                _logger.LogWarning("Empty message received");
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Message is required and cannot be empty" });
                return badRequest;
            }

            // Sanitize input to prevent injection attacks
            var sanitizedMessage = SanitizeInput(conversationRequest.Message);

            if (sanitizedMessage.Length > 2000)
            {
                _logger.LogWarning("Message too long: {Length} characters", sanitizedMessage.Length);
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Message too long. Maximum 2000 characters allowed." });
                return badRequest;
            }

            // For new conversations, we need to create or get an entity ID
            var entityId = conversationRequest.NonprofitId == Guid.Empty
                ? Guid.NewGuid().ToString()
                : conversationRequest.NonprofitId.ToString();

            // Try to get existing entity, create if doesn't exist
            var existingEntity = await _entityMatchingService.GetEntityAsync(entityId);
            if (existingEntity == null)
            {
                _logger.LogInformation("Creating new Nonprofit entity for conversation: {EntityId}", entityId);
                try
                {
                    entityId = await _entityMatchingService.CreateNonprofitEntityAsync(
                        conversationRequest.NonprofitId == Guid.Empty ? "anonymous" : conversationRequest.NonprofitId.ToString(),
                        "Nonprofit Profile"
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create Nonprofit entity");
                    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errorResponse.WriteAsJsonAsync(new { error = "Failed to initialize conversation. Please try again." });
                    return errorResponse;
                }
            }

            // Use EntityMatchingAI for conversation (powered by Groq)
            ConversationResponse result;
            try
            {
                result = await _entityMatchingService.SendConversationMessageAsync(
                    entityId,
                    sanitizedMessage
                );
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error during conversation processing");

                // Check for rate limiting
                if (ex.Message.Contains("429") || ex.Message.Contains("rate limit"))
                {
                    var rateLimitResponse = req.CreateResponse(HttpStatusCode.TooManyRequests);
                    await rateLimitResponse.WriteAsJsonAsync(new {
                        error = "Too many requests. Please wait a moment and try again."
                    });
                    return rateLimitResponse;
                }

                var errorResponse = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
                await errorResponse.WriteAsJsonAsync(new { error = "Conversation service temporarily unavailable. Please try again." });
                return errorResponse;
            }

            // Include the entity ID in the response so client can track it
            var response = new
            {
                result.Reply,
                result.ExtractedData,
                result.ProfileComplete,
                EntityId = entityId,
                UpdatedHistory = result.UpdatedHistory,
                ProcessingTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds
            };

            var httpResponse = req.CreateResponse(HttpStatusCode.OK);
            await httpResponse.WriteAsJsonAsync(response);

            _logger.LogInformation("Conversation processed successfully in {Ms}ms", (DateTime.UtcNow - startTime).TotalMilliseconds);
            return httpResponse;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error");
            var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await errorResponse.WriteAsJsonAsync(new { error = "Invalid JSON format" });
            return errorResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing conversation");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "An unexpected error occurred. Please try again later." });
            return errorResponse;
        }
    }

    /// <summary>
    /// Sanitizes user input to prevent injection attacks
    /// </summary>
    private string SanitizeInput(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Remove any potentially dangerous characters
        input = input.Trim();

        // Remove control characters except newlines and tabs
        input = new string(input.Where(c => c == '\n' || c == '\t' || !char.IsControl(c)).ToArray());

        return input;
    }

    [Function("GenerateEmbedding")]
    public async Task<HttpResponseData> GenerateEmbedding(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "embeddings/generate")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Generating embedding");

        try
        {
            var requestBody = await JsonSerializer.DeserializeAsync<JsonDocument>(req.Body);
            var text = requestBody?.RootElement.GetProperty("text").GetString();

            if (string.IsNullOrEmpty(text))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Text is required");
                return badRequest;
            }

            var embedding = await _openAIService.GenerateEmbeddingAsync(text);

            var httpResponse = req.CreateResponse(HttpStatusCode.OK);
            await httpResponse.WriteAsJsonAsync(new { embedding });
            return httpResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }
}
