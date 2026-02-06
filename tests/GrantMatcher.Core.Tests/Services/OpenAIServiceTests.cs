using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using GrantMatcher.Core.Services;
using GrantMatcher.Shared.DTOs;
using Xunit;

namespace GrantMatcher.Core.Tests.Services;

public class OpenAIServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private const string ApiKey = "test-api-key";

    public OpenAIServiceTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new OpenAIService(null!, ApiKey));
    }

    [Fact]
    public void Constructor_WithNullApiKey_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new OpenAIService(_httpClient, null!));
    }

    [Fact]
    public void Constructor_SetsDefaultModels()
    {
        // Act
        var service = new OpenAIService(_httpClient, ApiKey);

        // Assert - just verify it doesn't throw
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithCustomModels_UsesCustomModels()
    {
        // Act
        var service = new OpenAIService(_httpClient, ApiKey, "custom-embedding", "custom-chat");

        // Assert - just verify it doesn't throw
        Assert.NotNull(service);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithValidText_ReturnsEmbedding()
    {
        // Arrange
        var expectedEmbedding = new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };
        var responseContent = new
        {
            data = new[]
            {
                new
                {
                    embedding = expectedEmbedding
                }
            }
        };

        SetupSuccessfulHttpResponse(HttpStatusCode.OK, responseContent);

        var service = new OpenAIService(_httpClient, ApiKey);

        // Act
        var result = await service.GenerateEmbeddingAsync("Test text");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedEmbedding.Length, result.Length);
        Assert.Equal(expectedEmbedding[0], result[0]);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithEmptyResponse_ThrowsException()
    {
        // Arrange
        var responseContent = new
        {
            data = Array.Empty<object>()
        };

        SetupSuccessfulHttpResponse(HttpStatusCode.OK, responseContent);

        var service = new OpenAIService(_httpClient, ApiKey);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.GenerateEmbeddingAsync("Test text"));
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithHttpError_ThrowsException()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("Bad request")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var service = new OpenAIService(_httpClient, ApiKey);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await service.GenerateEmbeddingAsync("Test text"));
    }

    [Fact]
    public async Task ProcessConversationAsync_WithValidRequest_ReturnsResponse()
    {
        // Arrange
        var conversationRequest = new ConversationRequest
        {
            NonprofitId = Guid.NewGuid(),
            Message = "I'm interested in computer science Grants",
            History = new List<ConversationMessage>()
        };

        var aiResponse = new
        {
            reply = "Great! Tell me more about your background.",
            extractedData = new
            {
                major = "Computer Science"
            },
            profileComplete = false
        };

        var responseContent = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = JsonSerializer.Serialize(aiResponse)
                    }
                }
            }
        };

        SetupSuccessfulHttpResponse(HttpStatusCode.OK, responseContent);

        var service = new OpenAIService(_httpClient, ApiKey);

        // Act
        var result = await service.ProcessConversationAsync(conversationRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Great! Tell me more about your background.", result.Reply);
        Assert.False(result.ProfileComplete);
        Assert.NotNull(result.ExtractedData);
        Assert.Equal("Computer Science", result.ExtractedData.Major);
    }

    [Fact]
    public async Task ProcessConversationAsync_WithConversationHistory_IncludesHistory()
    {
        // Arrange
        var conversationRequest = new ConversationRequest
        {
            NonprofitId = Guid.NewGuid(),
            Message = "My GPA is 3.8",
            History = new List<ConversationMessage>
            {
                new ConversationMessage { Role = "user", Content = "Hello", Timestamp = DateTime.UtcNow },
                new ConversationMessage { Role = "assistant", Content = "Hi! How can I help?", Timestamp = DateTime.UtcNow }
            }
        };

        var aiResponse = new
        {
            reply = "Great GPA! What's your major?",
            extractedData = new
            {
                gpa = 3.8m
            },
            profileComplete = false
        };

        var responseContent = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = JsonSerializer.Serialize(aiResponse)
                    }
                }
            }
        };

        SetupSuccessfulHttpResponse(HttpStatusCode.OK, responseContent);

        var service = new OpenAIService(_httpClient, ApiKey);

        // Act
        var result = await service.ProcessConversationAsync(conversationRequest);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.UpdatedHistory);
        Assert.Contains(result.UpdatedHistory, m => m.Content == "Hello");
        Assert.Contains(result.UpdatedHistory, m => m.Content == "My GPA is 3.8");
    }

    [Fact]
    public async Task ProcessConversationAsync_WithEmptyResponse_ThrowsException()
    {
        // Arrange
        var conversationRequest = new ConversationRequest
        {
            NonprofitId = Guid.NewGuid(),
            Message = "Hello",
            History = new List<ConversationMessage>()
        };

        var responseContent = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = ""
                    }
                }
            }
        };

        SetupSuccessfulHttpResponse(HttpStatusCode.OK, responseContent);

        var service = new OpenAIService(_httpClient, ApiKey);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ProcessConversationAsync(conversationRequest));
    }

    [Fact]
    public async Task ProcessConversationAsync_WithProfileComplete_ReturnsTrue()
    {
        // Arrange
        var conversationRequest = new ConversationRequest
        {
            NonprofitId = Guid.NewGuid(),
            Message = "I want to be a software engineer",
            History = new List<ConversationMessage>()
        };

        var aiResponse = new
        {
            reply = "Perfect! You have a complete profile now.",
            extractedData = new
            {
                major = "Computer Science",
                gpa = 3.8m,
                careerGoals = new[] { "Software engineer" }
            },
            profileComplete = true
        };

        var responseContent = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = JsonSerializer.Serialize(aiResponse)
                    }
                }
            }
        };

        SetupSuccessfulHttpResponse(HttpStatusCode.OK, responseContent);

        var service = new OpenAIService(_httpClient, ApiKey);

        // Act
        var result = await service.ProcessConversationAsync(conversationRequest);

        // Assert
        Assert.True(result.ProfileComplete);
    }

    [Fact]
    public async Task GenerateProfileSummaryAsync_WithValidData_ReturnsSummary()
    {
        // Arrange
        var profileData = new Dictionary<string, object>
        {
            { "major", "Computer Science" },
            { "gpa", 3.8m },
            { "activities", new[] { "Debate Team", "Volunteer" } }
        };

        var expectedSummary = "A computer science Nonprofit with a 3.8 GPA who participates in debate team and volunteers.";

        var responseContent = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = expectedSummary
                    }
                }
            }
        };

        SetupSuccessfulHttpResponse(HttpStatusCode.OK, responseContent);

        var service = new OpenAIService(_httpClient, ApiKey);

        // Act
        var result = await service.GenerateProfileSummaryAsync(profileData);

        // Assert
        Assert.Equal(expectedSummary, result);
    }

    [Fact]
    public async Task GenerateProfileSummaryAsync_WithEmptyData_ReturnsEmptyString()
    {
        // Arrange
        var profileData = new Dictionary<string, object>();

        var responseContent = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = (string?)null
                    }
                }
            }
        };

        SetupSuccessfulHttpResponse(HttpStatusCode.OK, responseContent);

        var service = new OpenAIService(_httpClient, ApiKey);

        // Act
        var result = await service.GenerateProfileSummaryAsync(profileData);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GenerateProfileSummaryAsync_WithHttpError_ThrowsException()
    {
        // Arrange
        var profileData = new Dictionary<string, object>();

        var httpResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("Invalid API key")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var service = new OpenAIService(_httpClient, ApiKey);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await service.GenerateProfileSummaryAsync(profileData));
    }

    private void SetupSuccessfulHttpResponse<T>(HttpStatusCode statusCode, T content)
    {
        var json = JsonSerializer.Serialize(content);
        var httpResponse = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);
    }
}
