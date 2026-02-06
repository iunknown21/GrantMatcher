namespace GrantMatcher.Client.Utilities;

/// <summary>
/// Provides debouncing functionality to reduce the frequency of operations
/// Useful for search inputs, window resize handlers, etc.
/// </summary>
public class DebounceHelper : IDisposable
{
    private CancellationTokenSource? _cts;
    private readonly int _delayMs;

    public DebounceHelper(int delayMs = 300)
    {
        _delayMs = delayMs;
    }

    /// <summary>
    /// Debounces the action - only executes after the specified delay with no new calls
    /// </summary>
    public async Task DebounceAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        // Cancel previous operation
        _cts?.Cancel();
        _cts?.Dispose();

        // Create new cancellation token source linked to the provided token
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            await Task.Delay(_delayMs, _cts.Token);
            await action();
        }
        catch (TaskCanceledException)
        {
            // Expected when debounced
        }
    }

    /// <summary>
    /// Debounces the action and returns a result
    /// </summary>
    public async Task<T?> DebounceAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        // Cancel previous operation
        _cts?.Cancel();
        _cts?.Dispose();

        // Create new cancellation token source linked to the provided token
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            await Task.Delay(_delayMs, _cts.Token);
            return await action();
        }
        catch (TaskCanceledException)
        {
            // Expected when debounced
            return default;
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}

/// <summary>
/// Static debounce helper for simple use cases
/// </summary>
public static class Debounce
{
    private static readonly Dictionary<string, DebounceHelper> _helpers = new();
    private static readonly object _lock = new();

    /// <summary>
    /// Debounces an action with a specific key
    /// </summary>
    public static async Task ExecuteAsync(string key, Func<Task> action, int delayMs = 300, CancellationToken cancellationToken = default)
    {
        DebounceHelper helper;
        lock (_lock)
        {
            if (!_helpers.TryGetValue(key, out helper!))
            {
                helper = new DebounceHelper(delayMs);
                _helpers[key] = helper;
            }
        }

        await helper.DebounceAsync(action, cancellationToken);
    }

    /// <summary>
    /// Debounces an action with a specific key and returns a result
    /// </summary>
    public static async Task<T?> ExecuteAsync<T>(string key, Func<Task<T>> action, int delayMs = 300, CancellationToken cancellationToken = default)
    {
        DebounceHelper helper;
        lock (_lock)
        {
            if (!_helpers.TryGetValue(key, out helper!))
            {
                helper = new DebounceHelper(delayMs);
                _helpers[key] = helper;
            }
        }

        return await helper.DebounceAsync(action, cancellationToken);
    }

    /// <summary>
    /// Clears all debounce helpers
    /// </summary>
    public static void Clear()
    {
        lock (_lock)
        {
            foreach (var helper in _helpers.Values)
            {
                helper.Dispose();
            }
            _helpers.Clear();
        }
    }
}
