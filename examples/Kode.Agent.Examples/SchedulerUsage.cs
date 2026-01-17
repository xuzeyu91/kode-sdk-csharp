using Kode.Agent.Sdk.Core.Scheduling;

namespace Kode.Agent.Examples;

/// <summary>
/// Example demonstrating the Scheduler and TimeBridge features.
/// </summary>
public static class SchedulerUsage
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Scheduler & TimeBridge Example ===\n");

        // Create a scheduler
        using var scheduler = new Scheduler(new SchedulerOptions
        {
            OnTrigger = info => Console.WriteLine($"[trigger] Task {info.TaskId} triggered (spec: {info.Spec})")
        });

        // Create a time bridge for time-based scheduling
        await using var timeBridge = new TimeBridge(new TimeBridgeOptions
        {
            Scheduler = scheduler
        });

        // 1. Step-based scheduling
        Console.WriteLine("1. Registering step-based task (every 3 steps)...");
        var stepHandle = scheduler.EverySteps(3, ctx =>
        {
            Console.WriteLine($"   [step-task] Executed at step {ctx.StepCount}");
            return Task.CompletedTask;
        });

        // 2. On-step listener (every step)
        Console.WriteLine("2. Registering on-step listener...");
        var unsubscribe = scheduler.OnStep(ctx =>
        {
            Console.WriteLine($"   [on-step] Notified of step {ctx.StepCount}");
            return Task.CompletedTask;
        });

        // 3. Simulate steps
        Console.WriteLine("\n3. Simulating 10 steps:\n");
        for (int i = 1; i <= 10; i++)
        {
            scheduler.NotifyStep(i);
            await Task.Delay(100); // Small delay to see output
        }

        // 4. Time-based scheduling with TimeBridge
        Console.WriteLine("\n4. TimeBridge scheduling examples:");
        
        // Schedule a task after 2 seconds
        Console.WriteLine("   Scheduling one-time task after 2 seconds...");
        var afterId = timeBridge.After(TimeSpan.FromSeconds(2), async () =>
        {
            Console.WriteLine("   [after] One-time task executed!");
            await Task.CompletedTask;
        });

        // Schedule recurring task (every 1 minute - won't actually run in this demo)
        var minutesId = timeBridge.EveryMinutes(1, async () =>
        {
            Console.WriteLine("   [minutes] Recurring task executed!");
            await Task.CompletedTask;
        });

        Console.WriteLine($"   Timer IDs: after={afterId}, minutes={minutesId}");
        Console.WriteLine($"   Active timers: {timeBridge.ActiveTimerCount}");

        // Wait for the "after" task
        Console.WriteLine("\n   Waiting 2.5 seconds for one-time task...");
        await Task.Delay(2500);

        // 5. Cancel and cleanup
        Console.WriteLine("\n5. Cleanup:");
        unsubscribe();
        Console.WriteLine("   - Unsubscribed on-step listener");
        
        scheduler.Cancel(stepHandle);
        Console.WriteLine("   - Cancelled step-based task");
        
        timeBridge.CancelAll();
        Console.WriteLine("   - Cancelled all timers");

        // 6. Enqueue example
        Console.WriteLine("\n6. Enqueue example:");
        scheduler.Enqueue(async () =>
        {
            Console.WriteLine("   [enqueue] First queued task");
            await Task.Delay(100);
        });
        scheduler.Enqueue(async () =>
        {
            Console.WriteLine("   [enqueue] Second queued task");
            await Task.Delay(100);
        });
        await scheduler.EnqueueAsync(() =>
        {
            Console.WriteLine("   [enqueue] Third queued task (awaited)");
            return Task.CompletedTask;
        });
        Console.WriteLine("   All queued tasks complete!");

        Console.WriteLine("\n=== Scheduler Example Complete ===");
    }
}
