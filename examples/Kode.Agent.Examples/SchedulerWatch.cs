using Kode.Agent.Examples.Shared;
using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Core.Agent;
using Kode.Agent.Sdk.Core.Templates;
using Kode.Agent.Sdk.Core.Types;
using AgentImpl = Kode.Agent.Sdk.Core.Agent.Agent;

namespace Kode.Agent.Examples;

/// <summary>
/// TS-aligned example 04: scheduler + file watching + todo reminders.
/// Mirrors <c>examples/04-scheduler-watch.ts</c>.
/// </summary>
public static class SchedulerWatch
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Scheduler Watch (TS-aligned) ===\n");

        Directory.CreateDirectory("./workspace");

        var deps = RuntimeFactory.CreateRuntime(
            storeDir: "./.kode/scheduler-watch",
            configure: ctx =>
            {
                ctx.RegisterBuiltin([RuntimeFactory.BuiltinGroup.Fs, RuntimeFactory.BuiltinGroup.Todo]);

                ctx.Templates.Register(new AgentTemplateDefinition
                {
                    Id = "watcher",
                    SystemPrompt = "You are an operations engineer. Monitor files and summarize progress regularly.",
                    Tools = ToolsConfig.Specific("fs_read", "fs_write", "fs_glob", "todo_read", "todo_write"),
                    Model = RuntimeFactory.GetDefaultModelId(),
                    Runtime = new TemplateRuntimeConfig
                    {
                        Todo = new TodoConfig
                        {
                            Enabled = true,
                            ReminderOnStart = true,
                            RemindIntervalSteps = 10
                        }
                    }
                });
            });

        var agent = await AgentImpl.CreateAsync(
            agentId: $"watcher-{Guid.NewGuid():N}",
            config: new AgentConfig
            {
                TemplateId = "watcher",
                SandboxOptions = new SandboxOptions
                {
                    WorkingDirectory = "./workspace",
                    EnforceBoundary = true,
                    WatchFiles = true
                }
            },
            dependencies: deps);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));

        // Scheduler: remind every 2 steps (like TS example)
        var scheduler = agent.Schedule();
        scheduler.EverySteps(2, async ctx =>
        {
            if (cts.IsCancellationRequested) return;
            Console.WriteLine($"[scheduler] remind at step {ctx.StepCount}");
            try
            {
                await agent.SendAsync(
                    "系统提醒：请总结当前任务进度并更新时间线。",
                    new SendOptions { Kind = PendingKind.Reminder },
                    cancellationToken: cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"[scheduler:error] {ex.Message}");
            }
        });

        using var fileChangedSub = agent.On("file_changed", evt =>
        {
            var e = (FileChangedEvent)evt;
            Console.WriteLine($"[monitor:file_changed] {e.Path} mtime={e.Mtime}");
        });

        using var todoReminderSub = agent.On("todo_reminder", evt =>
        {
            var e = (TodoReminderEvent)evt;
            Console.WriteLine($"[monitor:todo_reminder] {e.Reason}");
        });

        // Progress printer
        var progressTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var envelope in agent.SubscribeProgress(cancellationToken: cts.Token))
                {
                    if (envelope.Event is TextChunkEvent chunk)
                    {
                        Console.Write(chunk.Delta);
                    }
                    if (envelope.Event is DoneEvent)
                    {
                        Console.WriteLine();
                    }
                }
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        // Trigger a few steps to demonstrate scheduler + todo reminders
        await agent.SendAsync("请列出 README 中所有与事件驱动相关的章节。", cancellationToken: cts.Token);
        await agent.SendAsync("根据刚才的输出，更新 todo 列表并加上到期时间。", cancellationToken: cts.Token);
        await agent.SendAsync("监控 workspace 目录变化，如果有文件被修改请提醒。", cancellationToken: cts.Token);

        Console.WriteLine("\nYou can now edit any file under ./workspace to see `file_changed` events.");
        await Task.WhenAny(progressTask, Task.Delay(10_000, cts.Token));
        cts.Cancel();

        try { await progressTask; } catch (OperationCanceledException) { }
        await agent.DisposeAsync();
        cts.Dispose();
    }
}
