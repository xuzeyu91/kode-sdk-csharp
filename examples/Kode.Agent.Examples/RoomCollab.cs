using Kode.Agent.Examples.Shared;
using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Core.Collaboration;
using Kode.Agent.Sdk.Core.Pool;
using Kode.Agent.Sdk.Core.Templates;
using Kode.Agent.Sdk.Core.Types;
using AgentImpl = Kode.Agent.Sdk.Core.Agent.Agent;

namespace Kode.Agent.Examples;

/// <summary>
/// Example 03: Multi-Agent Room - demonstrates agent collaboration.
/// </summary>
public static class RoomCollab
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Room Collaboration Example ===\n");

        Directory.CreateDirectory("./workspace");

        var deps = RuntimeFactory.CreateRuntime(
            storeDir: "./.kode/room-collab",
            configure: ctx =>
            {
                ctx.RegisterBuiltin([RuntimeFactory.BuiltinGroup.Fs, RuntimeFactory.BuiltinGroup.Todo]);

                var model = RuntimeFactory.GetDefaultModelId();

                ctx.Templates.BulkRegister(
                [
                    new AgentTemplateDefinition
                    {
                        Id = "planner",
                        SystemPrompt = "You are the tech planner. Break work into tasks and delegate via @mentions.",
                        Tools = ToolsConfig.Specific("todo_read", "todo_write"),
                        Model = model,
                        Runtime = new TemplateRuntimeConfig
                        {
                            Todo = new TodoConfig { Enabled = true, ReminderOnStart = true, RemindIntervalSteps = 15 },
                            SubAgents = new SubAgentConfig { Templates = ["executor"], Depth = 1 }
                        }
                    },
                    new AgentTemplateDefinition
                    {
                        Id = "executor",
                        SystemPrompt = "You are an engineering specialist. Execute tasks sent by the planner.",
                        Tools = ToolsConfig.Specific("fs_read", "fs_write", "fs_edit", "todo_read", "todo_write"),
                        Model = model,
                        Runtime = new TemplateRuntimeConfig
                        {
                            Todo = new TodoConfig { Enabled = true, ReminderOnStart = false }
                        }
                    }
                ]);
            });

        await using var pool = new AgentPool(new AgentPoolOptions { Dependencies = deps, MaxAgents = 10 });
        var room = new Room(pool) { Name = "demo-room" };

        AgentConfig configFor(string templateId) => new()
        {
            TemplateId = templateId,
            SandboxOptions = new SandboxOptions
            {
                WorkingDirectory = "./workspace",
                EnforceBoundary = true,
                WatchFiles = false
            }
        };

        var planner = (AgentImpl)await pool.CreateAsync("agt:planner", configFor("planner"));
        var dev = (AgentImpl)await pool.CreateAsync("agt:dev", configFor("executor"));

        room.Join("planner", planner.AgentId);
        room.Join("dev", dev.AgentId);

        // Bind monitoring to both agents
        using var cts = new CancellationTokenSource();
        _ = BindMonitor(planner.AgentId, planner, cts.Token);
        _ = BindMonitor(dev.AgentId, dev, cts.Token);

        Console.WriteLine("\n[planner -> room] Kick-off");
        await room.SayAsync("planner", "Hi team, let us audit the repository README. @dev 请负责执行。", cts.Token);

        Console.WriteLine("\n[dev -> planner] Acknowledge");
        await room.SayAsync("dev", "收到，我会列出 README 权限与事件说明。", cts.Token);

        Console.WriteLine("\nCreating fork for alternative plan");
        var fork = (AgentImpl)await pool.ForkAsync(
            planner.AgentId,
            newAgentId: $"{planner.AgentId}/fork:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            cancellationToken: cts.Token);
        _ = BindMonitor(fork.AgentId, fork, cts.Token);
        await fork.SendAsync("这是分叉出来的方案备选，请记录不同的 README 修改建议。", cancellationToken: cts.Token);

        Console.WriteLine("\nCurrent room members:");
        foreach (var member in room.GetMembers())
        {
            Console.WriteLine($"- {member.Name}: {member.AgentId}");
        }

        cts.Cancel();

        Console.WriteLine("\n[room] Session complete");
        Console.WriteLine("[room] Members: planner, dev");
    }

    private static Task BindMonitor(string agentName, AgentImpl agent, CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            try
            {
                await foreach (var envelope in agent.EventBus.SubscribeAsync(EventChannel.Monitor, cancellationToken: cancellationToken))
                {
                    switch (envelope.Event)
                    {
                        case TokenUsageEvent tokenUsage:
                            Console.WriteLine($"[{agentName}] usage: {tokenUsage.InputTokens}+{tokenUsage.OutputTokens}");
                            break;

                        case ErrorEvent error:
                            Console.WriteLine($"[{agentName}] error: {error.Message}");
                            break;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        });
    }

    private static async Task RunAgentWithOutputAsync(string agentName, AgentImpl agent, string message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[{agentName}] {message}\n");
        Console.WriteLine($"[{agentName}:response] ");

        using var localCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var progressTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var envelope in agent.EventBus.SubscribeAsync(EventChannel.Progress, cancellationToken: localCts.Token))
                {
                    switch (envelope.Event)
                    {
                        case TextChunkEvent textChunk:
                            Console.Write(textChunk.Delta);
                            break;

                        case DoneEvent:
                            localCts.Cancel();
                            return;
                    }
                }
            }
            catch (OperationCanceledException) { }
        });

        await agent.RunAsync(message, localCts.Token);
        await Task.WhenAny(progressTask, Task.Delay(500));

        localCts.Cancel();
        Console.WriteLine();
    }
}
