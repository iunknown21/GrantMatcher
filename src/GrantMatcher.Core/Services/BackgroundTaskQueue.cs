using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using GrantMatcher.Core.Interfaces;

namespace GrantMatcher.Core.Services;

/// <summary>
/// Background task queue for processing operations asynchronously
/// Useful for cache warming, bulk operations, analytics, etc.
/// </summary>
public interface IBackgroundTaskQueue
{
    /// <summary>
    /// Queues a task for background execution
    /// </summary>
    void QueueTask(Func<CancellationToken, Task> task);

    /// <summary>
    /// Queues a task with a name for tracking
    /// </summary>
    void QueueTask(string taskName, Func<CancellationToken, Task> task);

    /// <summary>
    /// Dequeues and returns a task to execute
    /// </summary>
    Task<(string taskName, Func<CancellationToken, Task> task)?> DequeueAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the number of pending tasks
    /// </summary>
    int PendingCount { get; }
}

public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly ConcurrentQueue<(string taskName, Func<CancellationToken, Task> task)> _tasks = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly ILogger<BackgroundTaskQueue>? _logger;

    public int PendingCount => _tasks.Count;

    public BackgroundTaskQueue(ILogger<BackgroundTaskQueue>? logger = null)
    {
        _logger = logger;
    }

    public void QueueTask(Func<CancellationToken, Task> task)
    {
        QueueTask("UnnamedTask", task);
    }

    public void QueueTask(string taskName, Func<CancellationToken, Task> task)
    {
        if (task == null)
            throw new ArgumentNullException(nameof(task));

        _tasks.Enqueue((taskName, task));
        _signal.Release();

        _logger?.LogDebug("Background task queued: {TaskName}. Pending: {PendingCount}", taskName, _tasks.Count);
    }

    public async Task<(string taskName, Func<CancellationToken, Task> task)?> DequeueAsync(CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(cancellationToken);

        if (_tasks.TryDequeue(out var task))
        {
            _logger?.LogDebug("Background task dequeued: {TaskName}. Remaining: {PendingCount}", task.taskName, _tasks.Count);
            return task;
        }

        return null;
    }
}

/// <summary>
/// Background service that processes queued tasks
/// Register as hosted service in ASP.NET Core or Azure Functions
/// </summary>
public class BackgroundTaskService : IDisposable
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ILogger<BackgroundTaskService> _logger;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _backgroundTask;
    private readonly int _maxConcurrentTasks;

    public BackgroundTaskService(
        IBackgroundTaskQueue taskQueue,
        ILogger<BackgroundTaskService> logger,
        int maxConcurrentTasks = 3)
    {
        _taskQueue = taskQueue ?? throw new ArgumentNullException(nameof(taskQueue));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxConcurrentTasks = maxConcurrentTasks;
        _cancellationTokenSource = new CancellationTokenSource();

        // Start background processing
        _backgroundTask = Task.Run(() => ProcessTasksAsync(_cancellationTokenSource.Token));
        _logger.LogInformation("BackgroundTaskService started with {MaxConcurrency} concurrent tasks", maxConcurrentTasks);
    }

    private async Task ProcessTasksAsync(CancellationToken cancellationToken)
    {
        var semaphore = new SemaphoreSlim(_maxConcurrentTasks, _maxConcurrentTasks);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var taskItem = await _taskQueue.DequeueAsync(cancellationToken);
                if (taskItem.HasValue)
                {
                    await semaphore.WaitAsync(cancellationToken);

                    // Process task asynchronously
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            _logger.LogInformation("Executing background task: {TaskName}", taskItem.Value.taskName);
                            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                            await taskItem.Value.task(cancellationToken);

                            stopwatch.Stop();
                            _logger.LogInformation(
                                "Background task completed: {TaskName} in {ElapsedMs}ms",
                                taskItem.Value.taskName,
                                stopwatch.ElapsedMilliseconds);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                ex,
                                "Background task failed: {TaskName}",
                                taskItem.Value.taskName);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when shutting down
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background task processing loop");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken); // Back off on error
            }
        }
    }

    public void Dispose()
    {
        _logger.LogInformation("Stopping BackgroundTaskService");
        _cancellationTokenSource.Cancel();

        try
        {
            _backgroundTask.Wait(TimeSpan.FromSeconds(30));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error waiting for background tasks to complete");
        }

        _cancellationTokenSource.Dispose();
    }
}

/// <summary>
/// Common background tasks
/// </summary>
public static class BackgroundTasks
{
    /// <summary>
    /// Warms up cache with frequently accessed data
    /// </summary>
    public static Func<CancellationToken, Task> CacheWarmup(
        ICachingService cache,
        Func<Task<object>> dataLoader,
        string cacheKey,
        TimeSpan expiration)
    {
        return async (ct) =>
        {
            var data = await dataLoader();
            if (data != null && data is not null)
            {
                await cache.SetAsync(cacheKey, data, expiration, cancellationToken: ct);
            }
        };
    }

    /// <summary>
    /// Invalidates cache after a delay (useful for temporary caching)
    /// </summary>
    public static Func<CancellationToken, Task> DelayedCacheInvalidation(
        ICachingService cache,
        string pattern,
        TimeSpan delay)
    {
        return async (ct) =>
        {
            await Task.Delay(delay, ct);
            await cache.RemoveByPatternAsync(pattern, ct);
        };
    }

    /// <summary>
    /// Bulk operation processor
    /// </summary>
    public static Func<CancellationToken, Task> BulkOperation<T>(
        IEnumerable<T> items,
        Func<T, CancellationToken, Task> processItem,
        int batchSize = 10)
    {
        return async (ct) =>
        {
            var batches = items
                .Select((item, index) => new { item, index })
                .GroupBy(x => x.index / batchSize)
                .Select(g => g.Select(x => x.item).ToList());

            foreach (var batch in batches)
            {
                var tasks = batch.Select(item => processItem(item, ct));
                await Task.WhenAll(tasks);
            }
        };
    }
}
