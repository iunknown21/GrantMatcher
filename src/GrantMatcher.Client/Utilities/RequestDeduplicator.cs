using System.Collections.Concurrent;

namespace GrantMatcher.Client.Utilities;

/// <summary>
/// Deduplicates concurrent requests to prevent multiple identical API calls
/// Useful when multiple components request the same data simultaneously
/// </summary>
public class RequestDeduplicator
{
    private readonly ConcurrentDictionary<string, Task<object?>> _pendingRequests = new();

    /// <summary>
    /// Executes the request or returns the result of an in-flight request with the same key
    /// </summary>
    public async Task<T?> ExecuteAsync<T>(string key, Func<Task<T>> request, CancellationToken cancellationToken = default) where T : class
    {
        while (true)
        {
            // Try to get or add the request
            var tcs = new TaskCompletionSource<object?>();
            var requestTask = _pendingRequests.GetOrAdd(key, _ => ExecuteRequestAsync(key, request, tcs));

            try
            {
                var result = await requestTask;
                return result as T;
            }
            catch (Exception)
            {
                // If the request failed, remove it and retry
                _pendingRequests.TryRemove(key, out _);
                throw;
            }
        }
    }

    private async Task<object?> ExecuteRequestAsync<T>(string key, Func<Task<T>> request, TaskCompletionSource<object?> tcs)
    {
        try
        {
            var result = await request();
            tcs.SetResult(result);
            return result;
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
            throw;
        }
        finally
        {
            // Remove from pending after a short delay to allow concurrent requests to benefit
            _ = Task.Delay(100).ContinueWith(_ =>
            {
                _pendingRequests.TryRemove(key, out Task<object?>? _);
                return Task.CompletedTask;
            });
        }
    }

    /// <summary>
    /// Clears all pending requests (typically not needed, but available for cleanup)
    /// </summary>
    public void Clear()
    {
        _pendingRequests.Clear();
    }
}

/// <summary>
/// Static request deduplicator for simple use cases
/// </summary>
public static class RequestCache
{
    private static readonly RequestDeduplicator _deduplicator = new();

    /// <summary>
    /// Executes a request or returns the cached result if available
    /// </summary>
    public static Task<T?> ExecuteAsync<T>(string key, Func<Task<T>> request, CancellationToken cancellationToken = default) where T : class
    {
        return _deduplicator.ExecuteAsync(key, request, cancellationToken);
    }

    /// <summary>
    /// Clears all cached requests
    /// </summary>
    public static void Clear()
    {
        _deduplicator.Clear();
    }
}
