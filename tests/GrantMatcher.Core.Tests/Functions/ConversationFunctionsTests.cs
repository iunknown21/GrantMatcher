using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Moq;
using GrantMatcher.Core.Interfaces;
using GrantMatcher.Functions.Functions;
using GrantMatcher.Shared.DTOs;
using Xunit;

namespace GrantMatcher.Core.Tests.Functions;

public class ConversationFunctionsTests
{
    private readonly Mock<ILogger<ConversationFunctions>> _loggerMock;
    private readonly Mock<IOpenAIService> _openAIServiceMock;
    private readonly Mock<IEntityMatchingService> _entityMatchingServiceMock;
    private readonly Mock<FunctionContext> _functionContextMock;
    private readonly ConversationFunctions _functions;

    public ConversationFunctionsTests()
    {
        _loggerMock = new Mock<ILogger<ConversationFunctions>>();
        _openAIServiceMock = new Mock<IOpenAIService>();
        _entityMatchingServiceMock = new Mock<IEntityMatchingService>();
        _functionContextMock = new Mock<FunctionContext>();

        _functions = new ConversationFunctions(
            _loggerMock.Object,
            _openAIServiceMock.Object,
            _entityMatchingServiceMock.Object
        );
    }

    [Fact]
    public async Task ProcessConversation_WithEmptyBody_ReturnsBadRequest()
    {
        // Arrange
        var request = CreateHttpRequestData(string.Empty);

        // Act
        var response = await _functions.ProcessConversation(request, _functionContextMock.Object);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProcessConversation_WithInvalidJson_ReturnsBadRequest()
    {
        // Arrange
        var request = CreateHttpRequestData("{invalid json}");

        // Act
        var response = await _functions.ProcessConversation(request, _functionContextMock.Object);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProcessConversation_WithEmptyMessage_ReturnsBadRequest()
    {
        // Arrange
        var conversationRequest = new ConversationRequest
        {
            NonprofitId = Guid.NewGuid(),
            Message = ""
        };
        var request = CreateHttpRequestData(JsonSerializer.Serialize(conversationRequest));

        // Act
        var response = await _functions.ProcessConversation(request, _functionContextMock.Object);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProcessConversation_WithMessageTooLong_ReturnsBadRequest()
    {
        // Arrange
        var longMessage = new string('a', 2001); // Exceeds 2000 character limit
        var conversationRequest = new ConversationRequest
        {
            NonprofitId = Guid.NewGuid(),
            Message = longMessage
        };
        var request = CreateHttpRequestData(JsonSerializer.Serialize(conversationRequest));

        // Act
        var response = await _functions.ProcessConversation(request, _functionContextMock.Object);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProcessConversation_WithValidMessage_CreatesEntityIfNotExists()
    {
        // Arrange
        var NonprofitId = Guid.NewGuid();
        var conversationRequest = new ConversationRequest
        {
            NonprofitId = NonprofitId,
            Message = "Hello, I need help finding Grants"
        };
        var request = CreateHttpRequestData(JsonSerializer.Serialize(conversationRequest));

        _entityMatchingServiceMock
            .Setup(x => x.GetEntityAsync(It.IsAny<string>(), default))
            .ReturnsAsync((EntityResponse?)null);

        var newEntityId = Guid.NewGuid().ToString();
        _entityMatchingServiceMock
            .Setup(x => x.CreateNonprofitEntityAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(newEntityId);

        _entityMatchingServiceMock
            .Setup(x => x.SendConversationMessageAsync(It.IsAny<string>(), It.IsAny<string>(), null, default))
            .ReturnsAsync(new ConversationResponse
            {
                Reply = "Hello! I'd be happy to help you find Grants.",
                ExtractedData = null,
                ProfileComplete = false,
                UpdatedHistory = new System.Collections.Generic.List<ConversationMessage>()
            });

        // Act
        var response = await _functions.ProcessConversation(request, _functionContextMock.Object);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _entityMatchingServiceMock.Verify(
            x => x.CreateNonprofitEntityAsync(It.IsAny<string>(), It.IsAny<string>(), default),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessConversation_WithExistingEntity_DoesNotCreateNew()
    {
        // Arrange
        var NonprofitId = Guid.NewGuid();
        var entityId = NonprofitId.ToString();
        var conversationRequest = new ConversationRequest
        {
            NonprofitId = NonprofitId,
            Message = "What Grants are available?"
        };
        var request = CreateHttpRequestData(JsonSerializer.Serialize(conversationRequest));

        _entityMatchingServiceMock
            .Setup(x => x.GetEntityAsync(entityId, default))
            .ReturnsAsync(new EntityResponse
            {
                Id = entityId,
                Name = "Nonprofit Profile",
                EntityType = 1
            });

        _entityMatchingServiceMock
            .Setup(x => x.SendConversationMessageAsync(entityId, It.IsAny<string>(), null, default))
            .ReturnsAsync(new ConversationResponse
            {
                Reply = "Here are some Grants you might be interested in...",
                ExtractedData = null,
                ProfileComplete = false,
                UpdatedHistory = new System.Collections.Generic.List<ConversationMessage>()
            });

        // Act
        var response = await _functions.ProcessConversation(request, _functionContextMock.Object);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _entityMatchingServiceMock.Verify(
            x => x.CreateNonprofitEntityAsync(It.IsAny<string>(), It.IsAny<string>(), default),
            Times.Never
        );
    }

    [Fact]
    public async Task ProcessConversation_WithValidMessage_SanitizesInput()
    {
        // Arrange
        var NonprofitId = Guid.NewGuid();
        var entityId = NonprofitId.ToString();
        var messageWithControlChars = "Hello\x00\x01\x02 World\x03";
        var conversationRequest = new ConversationRequest
        {
            NonprofitId = NonprofitId,
            Message = messageWithControlChars
        };
        var request = CreateHttpRequestData(JsonSerializer.Serialize(conversationRequest));

        _entityMatchingServiceMock
            .Setup(x => x.GetEntityAsync(entityId, default))
            .ReturnsAsync(new EntityResponse { Id = entityId, Name = "Nonprofit", EntityType = 1 });

        string? capturedMessage = null;
        _entityMatchingServiceMock
            .Setup(x => x.SendConversationMessageAsync(entityId, It.IsAny<string>(), null, default))
            .Callback<string, string, string?, System.Threading.CancellationToken>((id, msg, prompt, ct) => capturedMessage = msg)
            .ReturnsAsync(new ConversationResponse
            {
                Reply = "Response",
                UpdatedHistory = new System.Collections.Generic.List<ConversationMessage>()
            });

        // Act
        var response = await _functions.ProcessConversation(request, _functionContextMock.Object);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(capturedMessage);
        Assert.DoesNotContain("\x00", capturedMessage);
        Assert.DoesNotContain("\x01", capturedMessage);
        Assert.DoesNotContain("\x02", capturedMessage);
        Assert.DoesNotContain("\x03", capturedMessage);
    }

    [Fact]
    public async Task ProcessConversation_WhenEntityMatchingReturnsRateLimit_Returns429()
    {
        // Arrange
        var NonprofitId = Guid.NewGuid();
        var entityId = NonprofitId.ToString();
        var conversationRequest = new ConversationRequest
        {
            NonprofitId = NonprofitId,
            Message = "Hello"
        };
        var request = CreateHttpRequestData(JsonSerializer.Serialize(conversationRequest));

        _entityMatchingServiceMock
            .Setup(x => x.GetEntityAsync(entityId, default))
            .ReturnsAsync(new EntityResponse { Id = entityId, Name = "Nonprofit", EntityType = 1 });

        _entityMatchingServiceMock
            .Setup(x => x.SendConversationMessageAsync(entityId, It.IsAny<string>(), null, default))
            .ThrowsAsync(new HttpRequestException("429 rate limit exceeded"));

        // Act
        var response = await _functions.ProcessConversation(request, _functionContextMock.Object);

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task ProcessConversation_WhenEntityMatchingFails_Returns503()
    {
        // Arrange
        var NonprofitId = Guid.NewGuid();
        var entityId = NonprofitId.ToString();
        var conversationRequest = new ConversationRequest
        {
            NonprofitId = NonprofitId,
            Message = "Hello"
        };
        var request = CreateHttpRequestData(JsonSerializer.Serialize(conversationRequest));

        _entityMatchingServiceMock
            .Setup(x => x.GetEntityAsync(entityId, default))
            .ReturnsAsync(new EntityResponse { Id = entityId, Name = "Nonprofit", EntityType = 1 });

        _entityMatchingServiceMock
            .Setup(x => x.SendConversationMessageAsync(entityId, It.IsAny<string>(), null, default))
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        // Act
        var response = await _functions.ProcessConversation(request, _functionContextMock.Object);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task ProcessConversation_WhenCreateEntityFails_Returns500()
    {
        // Arrange
        var NonprofitId = Guid.NewGuid();
        var conversationRequest = new ConversationRequest
        {
            NonprofitId = NonprofitId,
            Message = "Hello"
        };
        var request = CreateHttpRequestData(JsonSerializer.Serialize(conversationRequest));

        _entityMatchingServiceMock
            .Setup(x => x.GetEntityAsync(It.IsAny<string>(), default))
            .ReturnsAsync((EntityResponse?)null);

        _entityMatchingServiceMock
            .Setup(x => x.CreateNonprofitEntityAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ThrowsAsync(new Exception("Failed to create entity"));

        // Act
        var response = await _functions.ProcessConversation(request, _functionContextMock.Object);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task GenerateEmbedding_WithEmptyText_ReturnsBadRequest()
    {
        // Arrange
        var requestBody = new { text = "" };
        var request = CreateHttpRequestData(JsonSerializer.Serialize(requestBody));

        // Act
        var response = await _functions.GenerateEmbedding(request, _functionContextMock.Object);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateEmbedding_WithValidText_ReturnsEmbedding()
    {
        // Arrange
        var requestBody = new { text = "Computer science Grant" };
        var request = CreateHttpRequestData(JsonSerializer.Serialize(requestBody));
        var expectedEmbedding = new float[] { 0.1f, 0.2f, 0.3f };

        _openAIServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), default))
            .ReturnsAsync(expectedEmbedding);

        // Act
        var response = await _functions.GenerateEmbedding(request, _functionContextMock.Object);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GenerateEmbedding_WhenOpenAIFails_Returns500()
    {
        // Arrange
        var requestBody = new { text = "Test text" };
        var request = CreateHttpRequestData(JsonSerializer.Serialize(requestBody));

        _openAIServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new Exception("OpenAI API error"));

        // Act
        var response = await _functions.GenerateEmbedding(request, _functionContextMock.Object);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    private HttpRequestData CreateHttpRequestData(string body)
    {
        var context = new Mock<FunctionContext>();
        var request = new Mock<HttpRequestData>(context.Object);

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(body));
        request.Setup(r => r.Body).Returns(stream);

        var responseData = new Mock<HttpResponseData>(context.Object);
        responseData.SetupProperty(r => r.StatusCode);
        responseData.Setup(r => r.Headers).Returns(new HttpHeadersCollection());

        request.Setup(r => r.CreateResponse()).Returns(() =>
        {
            var response = new Mock<HttpResponseData>(context.Object);
            response.SetupProperty(r => r.StatusCode);
            response.Setup(r => r.Headers).Returns(new HttpHeadersCollection());
            response.Setup(r => r.Body).Returns(new MemoryStream());
            return response.Object;
        });

        return request.Object;
    }
}
