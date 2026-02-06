using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace GrantMatcher.Core.Services;

/// <summary>
/// Monitors and tracks performance metrics for operations
/// </summary>
public interface IPerformanceMonitor
{
    /// <summary>
    /// Tracks an operation and logs if it exceeds threshold
    /// </summary>
    Task<T> TrackAsync<T>(
        string operationName,
        Func<Task<T>> operation,
        TimeSpan? warningThreshold = null,
        Dictionary<string, object>? additionalProperties = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracks an operation and logs if it exceeds threshold (void return)
    /// </summary>
    Task TrackAsync(
        string operationName,
        Func<Task> operation,
        TimeSpan? warningThreshold = null,
        Dictionary<string, object>? additionalProperties = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a performance scope that can be disposed
    /// </summary>
    IPerformanceScope BeginScope(
        string operationName,
        TimeSpan? warningThreshold = null,
        Dictionary<string, object>? additionalProperties = null);

    /// <summary>
    /// Gets performance statistics
    /// </summary>
    PerformanceStatistics GetStatistics();

    /// <summary>
    /// Resets all statistics
    /// </summary>
    void ResetStatistics();
}

/// <summary>
/// Disposable performance tracking scope
/// </summary>
public interface IPerformanceScope : IDisposable
{
    string OperationName { get; }
    Stopwatch Stopwatch { get; }
    void AddProperty(string key, object value);
}

/// <summary>
/// Performance statistics
/// </summary>
public class PerformanceStatistics
{
    public int TotalOperations { get; set; }
    public int SlowOperations { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public TimeSpan MaxDuration { get; set; }
    public TimeSpan MinDuration { get; set; }
    public Dictionary<string, OperationStats> OperationBreakdown { get; set; } = new();
}

public class OperationStats
{
    public int Count { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan AverageDuration => Count > 0 ? TimeSpan.FromTicks(TotalDuration.Ticks / Count) : TimeSpan.Zero;
    public TimeSpan MaxDuration { get; set; }
    public TimeSpan MinDuration { get; set; }
}

public class PerformanceMonitor : IPerformanceMonitor
{
    private readonly ILogger<PerformanceMonitor> _logger;
    private readonly PerformanceStatistics _statistics;
    private readonly object _statsLock = new();

    // Default warning threshold
    private static readonly TimeSpan DefaultWarningThreshold = TimeSpan.FromSeconds(2);

    public PerformanceMonitor(ILogger<PerformanceMonitor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _statistics = new PerformanceStatistics
        {
            MinDuration = TimeSpan.MaxValue,
            MaxDuration = TimeSpan.Zero
        };
    }

    public async Task<T> TrackAsync<T>(
        string operationName,
        Func<Task<T>> operation,
        TimeSpan? warningThreshold = null,
        Dictionary<string, object>? additionalProperties = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operationName))
            throw new ArgumentException("Operation name cannot be null or empty", nameof(operationName));

        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        var stopwatch = Stopwatch.StartNew();
        var threshold = warningThreshold ?? DefaultWarningThreshold;
        Exception? exception = null;
        T? result = default;

        try
        {
            result = await operation();
            return result;
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            LogPerformance(operationName, stopwatch.Elapsed, threshold, additionalProperties, exception);
            UpdateStatistics(operationName, stopwatch.Elapsed, threshold);
        }
    }

    public async Task TrackAsync(
        string operationName,
        Func<Task> operation,
        TimeSpan? warningThreshold = null,
        Dictionary<string, object>? additionalProperties = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operationName))
            throw new ArgumentException("Operation name cannot be null or empty", nameof(operationName));

        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        var stopwatch = Stopwatch.StartNew();
        var threshold = warningThreshold ?? DefaultWarningThreshold;
        Exception? exception = null;

        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            LogPerformance(operationName, stopwatch.Elapsed, threshold, additionalProperties, exception);
            UpdateStatistics(operationName, stopwatch.Elapsed, threshold);
        }
    }

    public IPerformanceScope BeginScope(
        string operationName,
        TimeSpan? warningThreshold = null,
        Dictionary<string, object>? additionalProperties = null)
    {
        if (string.IsNullOrWhiteSpace(operationName))
            throw new ArgumentException("Operation name cannot be null or empty", nameof(operationName));

        return new PerformanceScope(
            operationName,
            warningThreshold ?? DefaultWarningThreshold,
            additionalProperties,
            this);
    }

    public PerformanceStatistics GetStatistics()
    {
        lock (_statsLock)
        {
            return new PerformanceStatistics
            {
                TotalOperations = _statistics.TotalOperations,
                SlowOperations = _statistics.SlowOperations,
                AverageDuration = _statistics.AverageDuration,
                MaxDuration = _statistics.MaxDuration,
                MinDuration = _statistics.MinDuration == TimeSpan.MaxValue ? TimeSpan.Zero : _statistics.MinDuration,
                OperationBreakdown = new Dictionary<string, OperationStats>(_statistics.OperationBreakdown)
            };
        }
    }

    public void ResetStatistics()
    {
        lock (_statsLock)
        {
            _statistics.TotalOperations = 0;
            _statistics.SlowOperations = 0;
            _statistics.AverageDuration = TimeSpan.Zero;
            _statistics.MaxDuration = TimeSpan.Zero;
            _statistics.MinDuration = TimeSpan.MaxValue;
            _statistics.OperationBreakdown.Clear();
        }

        _logger.LogInformation("Performance statistics reset");
    }

    private void LogPerformance(
        string operationName,
        TimeSpan duration,
        TimeSpan threshold,
        Dictionary<string, object>? additionalProperties,
        Exception? exception)
    {
        var properties = new Dictionary<string, object>
        {
            ["Operation"] = operationName,
            ["DurationMs"] = duration.TotalMilliseconds,
            ["DurationSeconds"] = duration.TotalSeconds
        };

        if (additionalProperties != null)
        {
            foreach (var kvp in additionalProperties)
            {
                properties[kvp.Key] = kvp.Value;
            }
        }

        if (exception != null)
        {
            _logger.LogError(
                exception,
                "Operation {Operation} failed after {Duration}ms. Properties: {@Properties}",
                operationName,
                duration.TotalMilliseconds,
                properties);
        }
        else if (duration > threshold)
        {
            _logger.LogWarning(
                "Slow operation detected: {Operation} took {Duration}ms (threshold: {Threshold}ms). Properties: {@Properties}",
                operationName,
                duration.TotalMilliseconds,
                threshold.TotalMilliseconds,
                properties);
        }
        else
        {
            _logger.LogDebug(
                "Operation {Operation} completed in {Duration}ms. Properties: {@Properties}",
                operationName,
                duration.TotalMilliseconds,
                properties);
        }
    }

    private void UpdateStatistics(string operationName, TimeSpan duration, TimeSpan threshold)
    {
        lock (_statsLock)
        {
            _statistics.TotalOperations++;

            if (duration > threshold)
            {
                _statistics.SlowOperations++;
            }

            // Update overall stats
            if (duration > _statistics.MaxDuration)
                _statistics.MaxDuration = duration;

            if (duration < _statistics.MinDuration)
                _statistics.MinDuration = duration;

            var totalTicks = _statistics.AverageDuration.Ticks * (_statistics.TotalOperations - 1) + duration.Ticks;
            _statistics.AverageDuration = TimeSpan.FromTicks(totalTicks / _statistics.TotalOperations);

            // Update per-operation stats
            if (!_statistics.OperationBreakdown.ContainsKey(operationName))
            {
                _statistics.OperationBreakdown[operationName] = new OperationStats
                {
                    MinDuration = TimeSpan.MaxValue,
                    MaxDuration = TimeSpan.Zero
                };
            }

            var opStats = _statistics.OperationBreakdown[operationName];
            opStats.Count++;
            opStats.TotalDuration += duration;

            if (duration > opStats.MaxDuration)
                opStats.MaxDuration = duration;

            if (duration < opStats.MinDuration)
                opStats.MinDuration = duration;
        }
    }

    private class PerformanceScope : IPerformanceScope
    {
        private readonly PerformanceMonitor _monitor;
        private readonly TimeSpan _threshold;
        private readonly Dictionary<string, object> _properties;

        public string OperationName { get; }
        public Stopwatch Stopwatch { get; }

        public PerformanceScope(
            string operationName,
            TimeSpan threshold,
            Dictionary<string, object>? additionalProperties,
            PerformanceMonitor monitor)
        {
            OperationName = operationName;
            _threshold = threshold;
            _monitor = monitor;
            _properties = additionalProperties != null
                ? new Dictionary<string, object>(additionalProperties)
                : new Dictionary<string, object>();
            Stopwatch = Stopwatch.StartNew();
        }

        public void AddProperty(string key, object value)
        {
            _properties[key] = value;
        }

        public void Dispose()
        {
            Stopwatch.Stop();
            _monitor.LogPerformance(OperationName, Stopwatch.Elapsed, _threshold, _properties, null);
            _monitor.UpdateStatistics(OperationName, Stopwatch.Elapsed, _threshold);
        }
    }
}
