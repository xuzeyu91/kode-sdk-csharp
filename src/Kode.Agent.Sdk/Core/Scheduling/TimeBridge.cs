using Microsoft.Extensions.Logging;

namespace Kode.Agent.Sdk.Core.Scheduling;

/// <summary>
/// Options for TimeBridge.
/// </summary>
public record TimeBridgeOptions
{
    /// <summary>
    /// The scheduler to use.
    /// </summary>
    public required Scheduler Scheduler { get; init; }

    /// <summary>
    /// Tolerance for time drift in milliseconds.
    /// </summary>
    public int DriftToleranceMs { get; init; } = 5000;

    /// <summary>
    /// Logger for diagnostics.
    /// </summary>
    public ILogger<TimeBridge>? Logger { get; init; }
}

/// <summary>
/// Timer entry for tracking scheduled timers.
/// </summary>
internal sealed class TimerEntry : IDisposable
{
    public required string Id { get; init; }
    public CancellationTokenSource? Cts { get; set; }
    public Task? Task { get; set; }

    public void Cancel()
    {
        Cts?.Cancel();
    }

    public void Dispose()
    {
        Cts?.Dispose();
    }
}

/// <summary>
/// Time bridge for scheduling recurring tasks with pause/resume support.
/// Synchronizes time-based scheduling across agent lifecycle states.
/// </summary>
public sealed class TimeBridge : IAsyncDisposable
{
    private readonly Scheduler _scheduler;
    private readonly int _driftTolerance;
    private readonly ILogger<TimeBridge>? _logger;
    private readonly Dictionary<string, TimerEntry> _timers = [];
    private readonly object _lock = new();
    private int _idCounter;
    private bool _disposed;

    public TimeBridge(TimeBridgeOptions options)
    {
        _scheduler = options.Scheduler;
        _driftTolerance = options.DriftToleranceMs;
        _logger = options.Logger;
    }

    /// <summary>
    /// Schedules a recurring task every N minutes.
    /// </summary>
    /// <param name="minutes">Interval in minutes.</param>
    /// <param name="callback">Callback to execute.</param>
    /// <returns>Timer ID for cancellation.</returns>
    public string EveryMinutes(int minutes, Func<Task> callback)
    {
        if (minutes <= 0)
            throw new ArgumentException("Interval must be positive", nameof(minutes));

        var interval = TimeSpan.FromMinutes(minutes);
        var id = GenerateId("minutes");
        var spec = $"every:{minutes}m";

        var entry = new TimerEntry { Id = id };
        entry.Cts = new CancellationTokenSource();

        entry.Task = RunTimerLoopAsync(entry, interval, spec, callback, entry.Cts.Token);

        lock (_lock)
        {
            _timers[id] = entry;
        }

        _logger?.LogDebug("Started timer {TimerId} every {Minutes} minutes", id, minutes);
        return id;
    }

    /// <summary>
    /// Schedules a task using a simple cron expression (minute hour format).
    /// </summary>
    /// <param name="expression">Cron expression (minute hour * * *).</param>
    /// <param name="callback">Callback to execute.</param>
    /// <returns>Timer ID for cancellation.</returns>
    public string Cron(string expression, Func<Task> callback)
    {
        var parts = expression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
            throw new ArgumentException($"Unsupported cron expression: {expression}");

        if (!int.TryParse(parts[0], out var minute) || !int.TryParse(parts[1], out var hour))
            throw new ArgumentException($"Cron expression must have numeric minutes/hours: {expression}");

        var id = GenerateId("cron");
        var entry = new TimerEntry { Id = id };
        entry.Cts = new CancellationTokenSource();

        entry.Task = RunCronLoopAsync(entry, hour, minute, expression, callback, entry.Cts.Token);

        lock (_lock)
        {
            _timers[id] = entry;
        }

        _logger?.LogDebug("Started cron timer {TimerId} at {Hour}:{Minute:D2}", id, hour, minute);
        return id;
    }

    /// <summary>
    /// Schedules a one-time task after a delay.
    /// </summary>
    /// <param name="delay">Delay before execution.</param>
    /// <param name="callback">Callback to execute.</param>
    /// <returns>Timer ID for cancellation.</returns>
    public string After(TimeSpan delay, Func<Task> callback)
    {
        if (delay <= TimeSpan.Zero)
            throw new ArgumentException("Delay must be positive", nameof(delay));

        var id = GenerateId("after");
        var spec = $"after:{delay.TotalMilliseconds}ms";

        var entry = new TimerEntry { Id = id };
        entry.Cts = new CancellationTokenSource();

        entry.Task = RunOnceAsync(entry, delay, spec, callback, entry.Cts.Token);

        lock (_lock)
        {
            _timers[id] = entry;
        }

        _logger?.LogDebug("Scheduled one-time timer {TimerId} after {Delay}", id, delay);
        return id;
    }

    /// <summary>
    /// Cancels a scheduled timer.
    /// </summary>
    /// <param name="timerId">The timer ID to cancel.</param>
    /// <returns>True if the timer was found and cancelled.</returns>
    public bool Cancel(string timerId)
    {
        lock (_lock)
        {
            if (_timers.TryGetValue(timerId, out var entry))
            {
                entry.Cancel();
                _timers.Remove(timerId);
                _logger?.LogDebug("Cancelled timer {TimerId}", timerId);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Cancels all active timers.
    /// </summary>
    public void CancelAll()
    {
        lock (_lock)
        {
            foreach (var entry in _timers.Values)
            {
                entry.Cancel();
            }
            _timers.Clear();
        }
        _logger?.LogDebug("Cancelled all timers");
    }

    /// <summary>
    /// Gets the count of active timers.
    /// </summary>
    public int ActiveTimerCount
    {
        get
        {
            lock (_lock)
            {
                return _timers.Count;
            }
        }
    }

    /// <summary>
    /// Gets a snapshot of the state for persistence.
    /// </summary>
    public TimeBridgeSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return new TimeBridgeSnapshot
            {
                TimerIds = _timers.Keys.ToList(),
                CapturedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }
    }

    private async Task RunTimerLoopAsync(
        TimerEntry entry,
        TimeSpan interval,
        string spec,
        Func<Task> callback,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var expectedTime = DateTime.UtcNow.Add(interval);
                await Task.Delay(interval, cancellationToken);

                var drift = Math.Abs((DateTime.UtcNow - expectedTime).TotalMilliseconds);
                if (drift > _driftTolerance)
                {
                    _logger?.LogWarning("Timer {TimerId} drift: {Drift}ms (tolerance: {Tolerance}ms)",
                        entry.Id, drift, _driftTolerance);
                }

                await _scheduler.EnqueueAsync(async () =>
                {
                    await callback();
                    _scheduler.NotifyExternalTrigger(new ExternalTrigger
                    {
                        TaskId = entry.Id,
                        Spec = spec,
                        Kind = "time"
                    });
                }, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Timer {TimerId} failed", entry.Id);
        }
        finally
        {
            RemoveTimer(entry.Id);
        }
    }

    private async Task RunCronLoopAsync(
        TimerEntry entry,
        int hour,
        int minute,
        string expression,
        Func<Task> callback,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var next = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);
                if (next <= now)
                {
                    next = next.AddDays(1);
                }

                var delay = next - now;
                await Task.Delay(delay, cancellationToken);

                await _scheduler.EnqueueAsync(async () =>
                {
                    await callback();
                    _scheduler.NotifyExternalTrigger(new ExternalTrigger
                    {
                        TaskId = entry.Id,
                        Spec = $"cron:{expression}",
                        Kind = "time"
                    });
                }, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Cron timer {TimerId} failed", entry.Id);
        }
        finally
        {
            RemoveTimer(entry.Id);
        }
    }

    private async Task RunOnceAsync(
        TimerEntry entry,
        TimeSpan delay,
        string spec,
        Func<Task> callback,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);

            await _scheduler.EnqueueAsync(async () =>
            {
                await callback();
                _scheduler.NotifyExternalTrigger(new ExternalTrigger
                {
                    TaskId = entry.Id,
                    Spec = spec,
                    Kind = "time"
                });
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "One-time timer {TimerId} failed", entry.Id);
        }
        finally
        {
            RemoveTimer(entry.Id);
        }
    }

    private void RemoveTimer(string id)
    {
        lock (_lock)
        {
            if (_timers.TryGetValue(id, out var entry))
            {
                entry.Dispose();
                _timers.Remove(id);
            }
        }
    }

    private string GenerateId(string prefix)
    {
        var counter = Interlocked.Increment(ref _idCounter);
        return $"timer-{prefix}-{counter}";
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        CancelAll();

        // Wait for all timer tasks to complete
        List<Task> tasks;
        lock (_lock)
        {
            tasks = _timers.Values
                .Where(e => e.Task != null)
                .Select(e => e.Task!)
                .ToList();
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }
}

/// <summary>
/// Snapshot of TimeBridge state for persistence.
/// </summary>
public record TimeBridgeSnapshot
{
    public required IReadOnlyList<string> TimerIds { get; init; }
    public long CapturedAt { get; init; }
}

/// <summary>
/// External trigger notification.
/// </summary>
public record ExternalTrigger
{
    public required string TaskId { get; init; }
    public required string Spec { get; init; }
    public required string Kind { get; init; }
}
