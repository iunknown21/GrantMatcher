using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using GrantMatcher.Core.Services;
using GrantMatcher.Shared.DTOs;
using GrantMatcher.Shared.Models;
using Xunit;

namespace GrantMatcher.Core.Tests.Services;

public class EntityMatchingServiceTests
{
    private readonly Mock<ILogger<EntityMatchingService>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private const string ApiKey = "test-api-key";

    public EntityMatchingServiceTests()
    {
        _loggerMock = new Mock<ILogger<EntityMatchingService>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://test-api.example.com/")
        };
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new EntityMatchingService(null!, ApiKey, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullApiKey_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new EntityMatchingService(_httpClient, null!, _loggerMock.Object));
    }

    [Fact]
    public async Task StoreGrantEntityAsync_WithNullGrant_ThrowsArgumentNullException()
    {
        // Arrange
        var service = new EntityMatchingService(_httpClient, ApiKey, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await service.StoreGrantEntityAsync(null!));
    }

    [Fact]
    public async Task StoreGrantEntityAsync_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var service = new EntityMatchingService(_httpClient, ApiKey, _loggerMock.Object);
        var Grant = new GrantEntity { Name = "" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.StoreGrantEntityAsync(Grant));
    }

    [Fact]
    public async Task StoreGrantEntityAsync_WithValidGrant_ReturnsEntityId()
    {
        // Arrange
        var expectedEntityId = Guid.NewGuid().ToString();
        var Grant = new GrantEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Grant",
            Provider = "Test Provider",
            AwardAmount = 5000,
            NaturalLanguageSummary = "A test Grant"
        };

        var responseContent = new EntityResponse
        {
            Id = expectedEntityId,
            Name = Grant.Name
        };

        SetupSuccessfulHttpResponse(HttpStatusCode.OK, responseContent);

        var service = new EntityMatchingService(_httpClient, ApiKey, _loggerMock.Object);

        // Act
        var result = await service.StoreGrantEntityAsync(Grant);

        // Assert
        Assert.Equal(expectedEntityId, result);
    }

    [Fact]
    public async Task SendConversationMessageAsync_WithEmptyMessage_ThrowsException()
    {
        // Arrange
        var service = new EntityMatchingService(_httpClient, ApiKey, _loggerMock.Object);
        var entityId = Guid.NewGuid().ToString();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.SendConversationMessageAsync(entityId, ""));
    }

    [Theory]
    [InlineData("Hello, how are you?")]
    [InlineData("I'm interested in computer science Grants")]
    [InlineData("My GPA is 3.8")]
    public async Task SendConversationMessageAsync_WithValidInput_ReturnsResponse(string input)
    {
        // Arrange
        var service = new EntityMatchingService(_httpClient, ApiKey, _loggerMock.Object);

        var responseContent = new EntityMatchingConversationResponse
        {
            AiResponse = "Test response",
            NewInsights = new List<EntityMatchingInsight>()
        };

        SetupSuccessfulHttpResponse(HttpStatusCode.OK, responseContent);

        var entityId = Guid.NewGuid().ToString();

        // Act
        var result = await service.SendConversationMessageAsync(entityId, input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test response", result.Reply);
    }

    [Fact]
    public async Task SendConversationMessageAsync_WithInsights_ExtractsData()
    {
        // Arrange
        var service = new EntityMatchingService(_httpClient, ApiKey, _loggerMock.Object);

        var responseContent = new EntityMatchingConversationResponse
        {
            AiResponse = "Great! Tell me more.",
            NewInsights = new List<EntityMatchingInsight>
            {
                new EntityMatchingInsight
                {
                    Category = "academic",
                    Insight = "My GPA is 3.8",
                    Confidence = 0.95
                },
                new EntityMatchingInsight
                {
                    Category = "hobby",
                    Insight = "enjoys hiking",
                    Confidence = 0.90
                }
            }
        };

        SetupSuccessfulHttpResponse(HttpStatusCode.OK, responseContent);

        var entityId = Guid.NewGuid().ToString();

        // Act
        var result = await service.SendConversationMessageAsync(entityId, "I have a 3.8 GPA and enjoy hiking");

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ExtractedData);
        Assert.Equal(3.8m, result.ExtractedData.GPA);
        Assert.NotNull(result.ExtractedData.ExtracurricularActivities);
        Assert.Contains("enjoys hiking", result.ExtractedData.ExtracurricularActivities);
    }

    [Fact]
    public async Task UploadEmbeddingAsync_WithValidData_Succeeds()
    {
        // Arrange
        var service = new EntityMatchingService(_httpClient, ApiKey, _loggerMock.Object);
        var entityId = Guid.NewGuid().ToString();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };

        SetupSuccessfulHttpResponse(HttpStatusCode.OK, new { success = true });

        // Act & Assert (should not throw)
        await service.UploadEmbeddingAsync(entityId, embedding);
    }

    [Fact]
    public async Task SearchGrantsAsync_ReturnsResults()
    {
        // Arrange
        var service = new EntityMatchingService(_httpClient, ApiKey, _loggerMock.Object);

        var Nonprofit = new NonprofitProfile
        {
            UserId = "test-user",
            GPA = 3.5m,
            State = "California",
            Major = "Computer Science",
            GraduationYear = 2026
        };

        var searchRequest = new VectorSearchRequest
        {
            Query = "Computer science Grants",
            MinSimilarity = 0.7,
            Limit = 10
        };

        var responseContent = new VectorSearchResponse
        {
            Results = new List<SearchResultItem>
            {
                new SearchResultItem
                {
                    ProfileId = Guid.NewGuid().ToString(),
                    Similarity = 0.85
                }
            },
            TotalResults = 1
        };

        SetupSuccessfulHttpResponse(HttpStatusCode.OK, responseContent);

        // Act
        var result = await service.SearchGrantsAsync(Nonprofit, searchRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Results);
        Assert.Equal(1, result.TotalResults);
    }

    [Fact]
    public async Task GetEntityAsync_WithExistingEntity_ReturnsEntity()
    {
        // Arrange
        var service = new EntityMatchingService(_httpClient, ApiKey, _loggerMock.Object);
        var entityId = Guid.NewGuid().ToString();

        var responseContent = new EntityResponse
        {
            Id = entityId,
            Name = "Test Entity",
            EntityType = 1
        };

        SetupSuccessfulHttpResponse(HttpStatusCode.OK, responseContent);

        // Act
        var result = await service.GetEntityAsync(entityId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(entityId, result.Id);
        Assert.Equal("Test Entity", result.Name);
    }

    [Fact]
    public async Task GetEntityAsync_WithNonExistentEntity_ReturnsNull()
    {
        // Arrange
        var service = new EntityMatchingService(_httpClient, ApiKey, _loggerMock.Object);
        var entityId = Guid.NewGuid().ToString();

        var httpResponse = new HttpResponseMessage(HttpStatusCode.NotFound);

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await service.GetEntityAsync(entityId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateNonprofitEntityAsync_WithValidData_ReturnsEntityId()
    {
        // Arrange
        var service = new EntityMatchingService(_httpClient, ApiKey, _loggerMock.Object);
        var expectedEntityId = Guid.NewGuid().ToString();

        var responseContent = new EntityResponse
        {
            Id = expectedEntityId,
            Name = "Nonprofit Profile",
            EntityType = 1
        };

        SetupSuccessfulHttpResponse(HttpStatusCode.OK, responseContent);

        // Act
        var result = await service.CreateNonprofitEntityAsync("test-user", "John Doe");

        // Assert
        Assert.Equal(expectedEntityId, result);
    }

    [Fact]
    public async Task DeleteEntityAsync_WithValidId_Succeeds()
    {
        // Arrange
        var service = new EntityMatchingService(_httpClient, ApiKey, _loggerMock.Object);
        var entityId = Guid.NewGuid().ToString();

        SetupSuccessfulHttpResponse(HttpStatusCode.NoContent, "");

        // Act & Assert (should not throw)
        await service.DeleteEntityAsync(entityId);
    }

    [Fact]
    public async Task StoreGrantEntityAsync_WithHttpError_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = new EntityMatchingService(_httpClient, ApiKey, _loggerMock.Object);
        var Grant = new GrantEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Grant",
            Provider = "Test Provider",
            AwardAmount = 5000,
            NaturalLanguageSummary = "A test Grant"
        };

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

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await service.StoreGrantEntityAsync(Grant));
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
