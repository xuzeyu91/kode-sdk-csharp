using Kode.Agent.Examples.Shared;
using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Core.Templates;
using Kode.Agent.Sdk.Core.Types;
using AgentImpl = Kode.Agent.Sdk.Core.Agent.Agent;

namespace Kode.Agent.Examples;

/// <summary>
/// Example 01: Agent Inbox - demonstrates event streaming and tool execution monitoring.
/// </summary>
public static class AgentInbox
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Agent Inbox Example ===\n");

        Directory.CreateDirectory("./workspace");

        var deps = RuntimeFactory.CreateRuntime(
            storeDir: "./.kode/agent-inbox",
            configure: ctx =>
            {
                ctx.RegisterBuiltin([RuntimeFactory.BuiltinGroup.Fs, RuntimeFactory.BuiltinGroup.Bash, RuntimeFactory.BuiltinGroup.Todo]);

                ctx.Templates.Register(new AgentTemplateDefinition
                {
                    Id = "inbox",
                    SystemPrompt = "You are the repo teammate. Be concise and actionable.",
                    Tools = ToolsConfig.Specific("fs_read", "fs_write", "fs_glob", "fs_grep", "bash_run", "todo_read", "todo_write"),
                    Model = RuntimeFactory.GetDefaultModelId(),
                    Runtime = new TemplateRuntimeConfig
                    {
                        Todo = new TodoConfig { Enabled = true, ReminderOnStart = true }
                    },
                    Permission = new Kode.Agent.Sdk.Core.Templates.PermissionConfig
                    {
                        Mode = "auto",
                        RequireApprovalTools = ["bash_run"]
                    }
                });
            });

        var config = new AgentConfig
        {
            TemplateId = "inbox",
            SandboxOptions = new SandboxOptions
            {
                WorkingDirectory = "./workspace",
                EnforceBoundary = true,
                WatchFiles = false
            },
            MaxIterations = 20
        };

        var agent = await AgentImpl.CreateAsync(
            agentId: $"agent-inbox-{Guid.NewGuid():N}",
            config,
            deps);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var doneTcs = new TaskCompletionSource<bool>();

        // Subscribe to progress events (streaming)
        var progressTask = SubscribeProgressAsync(agent, doneTcs, cts.Token);

        // Subscribe to control events (approval requests)
        var controlTask = SubscribeControlAsync(agent, cts.Token);

        // Subscribe to monitor events (audit logging)
        var monitorTask = SubscribeMonitorAsync(agent, cts.Token);

        Console.WriteLine("[user] 请总结项目目录，并列出接下来可以执行的两个 todo。\n");
        Console.WriteLine("[assistant] ");

        await agent.SendAsync("请总结项目目录，并列出接下来可以执行的两个 todo。", cancellationToken: cts.Token);

        await Task.WhenAny(doneTcs.Task, Task.Delay(30_000, cts.Token));
        await cts.CancelAsync();

        try { await Task.WhenAll(progressTask, controlTask, monitorTask); }
        catch (OperationCanceledException) { }

        await agent.DisposeAsync();

        Console.WriteLine("\n[agent] Session complete");
    }

    private static async Task SubscribeProgressAsync(AgentImpl agent, TaskCompletionSource<bool> doneTcs, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var envelope in agent.SubscribeProgress(cancellationToken: cancellationToken))
            {
                switch (envelope.Event)
                {
                    case TextChunkEvent textChunk:
                        Console.Write(textChunk.Delta);
                        break;

                    case ToolStartEvent toolStart:
                        Console.WriteLine($"\n[tool] {toolStart.Call.Name} start");
                        break;

                    case ToolEndEvent toolEnd:
                        Console.WriteLine($"[tool] {toolEnd.Call.Name} end");
                        break;

                    case ToolErrorEvent toolError:
                        Console.WriteLine($"\n[tool:error] {toolError.Call.Name}: {toolError.Error}");
                        break;

                    case DoneEvent done:
                        Console.WriteLine($"\n[progress] done, reason: {done.Reason}, step: {done.Step}");
                        doneTcs.TrySetResult(true);
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private static async Task SubscribeControlAsync(AgentImpl agent, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var envelope in agent.EventBus.SubscribeAsync(EventChannel.Control, cancellationToken: cancellationToken))
            {
                if (envelope.Event is PermissionRequiredEvent permissionRequired)
                {
                    Console.WriteLine($"\n[approval] pending for {permissionRequired.Call.Name}");

                    // Auto-deny bash_run for demo
                    if (permissionRequired.Call.Name == "bash_run")
                    {
                        if (permissionRequired.Respond != null)
                        {
                            await permissionRequired.Respond("deny", new PermissionRespondOptions
                            {
                                Note = "Demo inbox denies bash_run by default."
                            });
                        }
                        else
                        {
                            await agent.DenyToolCallAsync(permissionRequired.Call.Id, "Demo inbox denies bash_run by default.");
                        }
                        Console.WriteLine("[approval] denied");
                    }
                    else
                    {
                        if (permissionRequired.Respond != null)
                        {
                            await permissionRequired.Respond("allow", new PermissionRespondOptions { Note = "auto-approved" });
                        }
                        else
                        {
                            await agent.ApproveToolCallAsync(permissionRequired.Call.Id);
                        }
                        Console.WriteLine("[approval] approved");
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private static async Task SubscribeMonitorAsync(AgentImpl agent, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var envelope in agent.EventBus.SubscribeAsync(EventChannel.Monitor, cancellationToken: cancellationToken))
            {
                switch (envelope.Event)
                {
                    case TokenUsageEvent tokenUsage:
                        Console.WriteLine($"[usage] input: {tokenUsage.InputTokens}, output: {tokenUsage.OutputTokens}");
                        break;

                    case ToolExecutedEvent toolExecuted:
                        Console.WriteLine($"[audit] {toolExecuted.Call.Name} {toolExecuted.Call.DurationMs ?? 0}ms");
                        break;

                    case ErrorEvent error:
                        Console.WriteLine($"[monitor:error] {error.Message}");
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
    }
}
