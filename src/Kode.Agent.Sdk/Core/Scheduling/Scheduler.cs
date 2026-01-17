namespace Kode.Agent.Sdk.Core.Scheduling;

/// <summary>
/// Scheduler handle for cancelling tasks.
/// </summary>
public readonly record struct SchedulerHandle(string Id);

/// <summary>
/// Trigger kind for scheduler tasks.
/// </summary>
public enum TriggerKind
{
    Steps,
    Time,
    Cron
}

/// <summary>
/// Step callback context.
/// </summary>
public record StepContext(int StepCount);

/// <summary>
/// Trigger information.
/// </summary>
public record TriggerInfo(string TaskId, string Spec, TriggerKind Kind);

/// <summary>
/// Scheduler options.
/// </summary>
public record SchedulerOptions
{
    /// <summary>
    /// Callback when a task is triggered.
    /// </summary>
    public Action<TriggerInfo>? OnTrigger { get; init; }
}

/// <summary>
/// Step-based task.
/// </summary>
internal record StepTask(
    string Id,
    int Every,
    Func<StepContext, Task> Callback,
    int LastTriggered
);

/// <summary>
/// Agent scheduler for step-based and time-based tasks.
/// </summary>
public class Scheduler : IDisposable
{
    private readonly Dictionary<string, StepTask> _stepTasks = new();
    private readonly HashSet<Func<StepContext, Task>> _stepListeners = [];
    private readonly Action<TriggerInfo>? _onTrigger;
    private readonly object _lock = new();
    private Task _queuedTask = Task.CompletedTask;

    public Scheduler(SchedulerOptions? options = null)
    {
        _onTrigger = options?.OnTrigger;
    }

    /// <summary>
    /// Register a callback to run every N steps.
    /// </summary>
    public SchedulerHandle EverySteps(int every, Func<StepContext, Task> callback)
    {
        if (every <= 0)
        {
            throw new ArgumentException("Interval must be positive", nameof(every));
        }

        var id = GenerateId("steps");
        lock (_lock)
        {
            _stepTasks[id] = new StepTask(id, every, callback, 0);
        }
        return new SchedulerHandle(id);
    }

    /// <summary>
    /// Register a callback to run every N steps (sync version).
    /// </summary>
    public SchedulerHandle EverySteps(int every, Action<StepContext> callback)
    {
        return EverySteps(every, ctx =>
        {
            callback(ctx);
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Register a callback to run on every step.
    /// </summary>
    public Action OnStep(Func<StepContext, Task> callback)
    {
        lock (_lock)
        {
            _stepListeners.Add(callback);
        }
        return () =>
        {
            lock (_lock)
            {
                _stepListeners.Remove(callback);
            }
        };
    }

    /// <summary>
    /// Register a callback to run on every step (sync version).
    /// </summary>
    public Action OnStep(Action<StepContext> callback)
    {
        return OnStep(ctx =>
        {
            callback(ctx);
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Enqueue a task to run after current tasks complete.
    /// </summary>
    public void Enqueue(Func<Task> callback)
    {
        lock (_lock)
        {
            _queuedTask = _queuedTask.ContinueWith(async _ =>
            {
                try
                {
                    await callback();
                }
                catch
                {
                    // Silently ignore errors in enqueued tasks
                }
            }).Unwrap();
        }
    }

    /// <summary>
    /// Enqueue a task to run after current tasks complete (async version).
    /// </summary>
    public Task EnqueueAsync(Func<Task> callback, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<bool>();
        
        lock (_lock)
        {
            _queuedTask = _queuedTask.ContinueWith(async _ =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await callback();
                    tcs.TrySetResult(true);
                }
                catch (OperationCanceledException)
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }).Unwrap();
        }
        
        return tcs.Task;
    }

    /// <summary>
    /// Enqueue a task to run after current tasks complete (sync version).
    /// </summary>
    public void Enqueue(Action callback)
    {
        Enqueue(() =>
        {
            callback();
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Notify the scheduler that a step has completed.
    /// </summary>
    public void NotifyStep(int stepCount)
    {
        List<Func<StepContext, Task>> listeners;
        List<StepTask> tasksToRun = [];

        lock (_lock)
        {
            listeners = _stepListeners.ToList();

            foreach (var kvp in _stepTasks)
            {
                var task = kvp.Value;
                var shouldTrigger = stepCount - task.LastTriggered >= task.Every;
                if (shouldTrigger)
                {
                    _stepTasks[kvp.Key] = task with { LastTriggered = stepCount };
                    tasksToRun.Add(task);
                }
            }
        }

        var context = new StepContext(stepCount);

        // Run listeners (fire and forget)
        foreach (var listener in listeners)
        {
            _ = Task.Run(() => listener(context));
        }

        // Run scheduled tasks (fire and forget)
        foreach (var task in tasksToRun)
        {
            _ = Task.Run(() => task.Callback(context));
            _onTrigger?.Invoke(new TriggerInfo(task.Id, $"steps:{task.Every}", TriggerKind.Steps));
        }
    }

    /// <summary>
    /// Cancel a scheduled task.
    /// </summary>
    public bool Cancel(SchedulerHandle handle)
    {
        lock (_lock)
        {
            return _stepTasks.Remove(handle.Id);
        }
    }

    /// <summary>
    /// Clear all scheduled tasks and listeners.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _stepTasks.Clear();
            _stepListeners.Clear();
        }
    }

    /// <summary>
    /// Notify an external trigger (for time/cron-based tasks).
    /// </summary>
    public void NotifyExternalTrigger(string taskId, string spec, TriggerKind kind)
    {
        _onTrigger?.Invoke(new TriggerInfo(taskId, spec, kind));
    }

    /// <summary>
    /// Notify an external trigger (for time/cron-based tasks).
    /// </summary>
    public void NotifyExternalTrigger(ExternalTrigger trigger)
    {
        var kind = trigger.Kind.ToLowerInvariant() switch
        {
            "time" => TriggerKind.Time,
            "cron" => TriggerKind.Cron,
            "steps" => TriggerKind.Steps,
            _ => TriggerKind.Time
        };
        _onTrigger?.Invoke(new TriggerInfo(trigger.TaskId, trigger.Spec, kind));
    }

    /// <summary>
    /// Get the number of scheduled tasks.
    /// </summary>
    public int TaskCount
    {
        get
        {
            lock (_lock)
            {
                return _stepTasks.Count;
            }
        }
    }

    private static string GenerateId(string prefix)
    {
        return $"{prefix}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}";
    }

    public void Dispose()
    {
        Clear();
    }
}
