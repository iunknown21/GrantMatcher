using System.Net.Http.Json;
using GrantMatcher.Shared.DTOs;
using GrantMatcher.Shared.Models;

namespace GrantMatcher.Client.Services;

public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // Profile operations
    public async Task<NonprofitProfile> CreateProfileAsync(NonprofitProfile profile)
    {
        var response = await _httpClient.PostAsJsonAsync("profiles", profile);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NonprofitProfile>()
            ?? throw new InvalidOperationException("Failed to create profile");
    }

    public async Task<NonprofitProfile?> GetProfileAsync(Guid id)
    {
        var response = await _httpClient.GetAsync($"profiles/{id}");
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<NonprofitProfile>();
    }

    public async Task<NonprofitProfile> UpdateProfileAsync(Guid id, NonprofitProfile profile)
    {
        var response = await _httpClient.PutAsJsonAsync($"profiles/{id}", profile);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NonprofitProfile>()
            ?? throw new InvalidOperationException("Failed to update profile");
    }

    public async Task DeleteProfileAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"profiles/{id}");
        response.EnsureSuccessStatusCode();
    }

    // Grant operations
    public async Task<GrantEntity?> GetGrantAsync(Guid id)
    {
        var response = await _httpClient.GetAsync($"Grants/{id}");
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<GrantEntity>();
    }

    public async Task<List<GrantEntity>> ListGrantsAsync()
    {
        var response = await _httpClient.GetAsync("Grants");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<GrantEntity>>()
            ?? new List<GrantEntity>();
    }

    // Matching operations
    public async Task<SearchResponse> SearchGrantsAsync(SearchRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("matches/search", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SearchResponse>()
            ?? new SearchResponse();
    }

    // Conversation operations
    public async Task<ConversationResponse> ProcessConversationAsync(ConversationRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("conversation", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ConversationResponse>()
            ?? throw new InvalidOperationException("Failed to process conversation");
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var response = await _httpClient.PostAsJsonAsync("embeddings/generate", new { text });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>();
        return result?.Embedding ?? Array.Empty<float>();
    }

    private class EmbeddingResponse
    {
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}
