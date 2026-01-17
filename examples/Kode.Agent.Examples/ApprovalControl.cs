using Kode.Agent.Examples.Shared;
using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Core.Hooks;
using Kode.Agent.Sdk.Core.Templates;
using Kode.Agent.Sdk.Core.Types;
using AgentImpl = Kode.Agent.Sdk.Core.Agent.Agent;

namespace Kode.Agent.Examples;

/// <summary>
/// Example 02: Approval Control - demonstrates permission and approval flow.
/// </summary>
public static class ApprovalControl
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Approval Control Example ===\n");

        Directory.CreateDirectory("./workspace");

        var deps = RuntimeFactory.CreateRuntime(
            storeDir: "./.kode/approval-control",
            configure: ctx =>
            {
                ctx.RegisterBuiltin([RuntimeFactory.BuiltinGroup.Fs, RuntimeFactory.BuiltinGroup.Bash, RuntimeFactory.BuiltinGroup.Todo]);

                ctx.Templates.Register(new AgentTemplateDefinition
                {
                    Id = "secure-runner",
                    SystemPrompt = "You are a cautious operator. Always respect approvals.",
                    Tools = ToolsConfig.Specific("fs_read", "fs_write", "bash_run", "bash_logs", "todo_read", "todo_write"),
                    Model = RuntimeFactory.GetDefaultModelId(),
                    Permission = new Kode.Agent.Sdk.Core.Templates.PermissionConfig
                    {
                        Mode = "approval",
                        RequireApprovalTools = ["bash_run"]
                    },
                    Runtime = new TemplateRuntimeConfig
                    {
                        Todo = new TodoConfig { Enabled = true, ReminderOnStart = true }
                    }
                });
            });

        var config = new AgentConfig
        {
            MaxIterations = 20,
            TemplateId = "secure-runner",
            SandboxOptions = new SandboxOptions
            {
                WorkingDirectory = "./workspace",
                EnforceBoundary = true,
                WatchFiles = false
            },
            Hooks =
            [
                new Hooks
                {
                    PreToolUse = (call, _) =>
                    {
                        if (call.Name == "bash_run")
                        {
                            var json = call.Input.ToString();
                            if (json.Contains("rm -rf", StringComparison.OrdinalIgnoreCase) ||
                                json.Contains("sudo", StringComparison.OrdinalIgnoreCase))
                            {
                                return Task.FromResult<HookDecision?>(HookDecision.Deny("命令命中禁用关键字"));
                            }
                        }
                        return Task.FromResult<HookDecision?>(null);
                    }
                }
            ]
        };

        var agent = await AgentImpl.CreateAsync(
            agentId: $"approval-control-{Guid.NewGuid():N}",
            config,
            deps);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var doneTcs = new TaskCompletionSource<int>();
        var expectedDoneCount = 2;

        // Subscribe to events
        var progressTask = SubscribeProgressAsync(agent, doneTcs, expectedDoneCount, cts.Token);
        var controlTask = SubscribeControlWithApprovalAsync(agent, cts.Token);
        var monitorTask = SubscribeMonitorAsync(agent, cts.Token);

        // First request: safe command
        Console.WriteLine("> Requesting safe command\n");
        Console.WriteLine("[user] 在 workspace 下列出文件，并生成下一步 todo。\n");
        Console.WriteLine("[assistant] ");

        await agent.SendAsync("在 workspace 下列出文件，并生成下一步 todo。");

        Console.WriteLine("\n\n> Requesting dangerous command\n");
        Console.WriteLine("[user] 执行命令: rm -rf /\n");
        Console.WriteLine("[assistant] ");

        await agent.SendAsync("执行命令: rm -rf /");

        await Task.WhenAny(doneTcs.Task, Task.Delay(45_000, cts.Token));
        cts.Cancel();

        try { await Task.WhenAll(progressTask, controlTask, monitorTask); }
        catch (OperationCanceledException) { }

        await agent.DisposeAsync();

        Console.WriteLine("\n[agent] Session complete");
    }

    private static async Task SubscribeProgressAsync(
        AgentImpl agent,
        TaskCompletionSource<int> doneTcs,
        int expectedDoneCount,
        CancellationToken cancellationToken)
    {
        try
        {
            var doneCount = 0;
            await foreach (var envelope in agent.SubscribeProgress(cancellationToken: cancellationToken))
            {
                switch (envelope.Event)
                {
                    case TextChunkEvent textChunk:
                        Console.Write(textChunk.Delta);
                        break;

                    case ToolStartEvent toolStart:
                        Console.WriteLine($"\n[tool:start] {toolStart.Call.Name}");
                        break;

                    case ToolEndEvent toolEnd:
                        Console.WriteLine($"[tool:end] {toolEnd.Call.Name}");
                        break;

                    case ToolErrorEvent toolError:
                        Console.WriteLine($"\n[tool:error] {toolError.Call.Name}: {toolError.Error}");
                        break;

                    case DoneEvent:
                        doneCount++;
                        if (doneCount >= expectedDoneCount)
                        {
                            doneTcs.TrySetResult(doneCount);
                        }
                        Console.WriteLine();
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private static async Task SubscribeControlWithApprovalAsync(AgentImpl agent, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var envelope in agent.EventBus.SubscribeAsync(EventChannel.Control, cancellationToken: cancellationToken))
            {
                if (envelope.Event is PermissionRequiredEvent permissionRequired)
                {
                    var argsStr = permissionRequired.Call.InputPreview?.ToString() ?? "";
                    Console.WriteLine($"\n[approval] pending for {permissionRequired.Call.Name} - {argsStr}");

                    // Simulate approval delay
                    await Task.Delay(500);

                    // Check if the command is dangerous
                    var isDangerous = permissionRequired.Call.Name == "bash_run" &&
                                     (argsStr.Contains("rm -rf") || argsStr.Contains("sudo"));

                    if (isDangerous)
                    {
                        if (permissionRequired.Respond != null)
                        {
                            await permissionRequired.Respond("deny", new PermissionRespondOptions
                            {
                                Note = "automated: deny"
                            });
                        }
                        else
                        {
                            await agent.DenyToolCallAsync(permissionRequired.Call.Id, "命令命中禁用关键字");
                        }
                        Console.WriteLine("[approval] DENIED - dangerous command");
                    }
                    else if (permissionRequired.Call.Name == "bash_run" && argsStr.Contains("ls"))
                    {
                        if (permissionRequired.Respond != null)
                        {
                            await permissionRequired.Respond("allow", new PermissionRespondOptions
                            {
                                Note = "automated: allow"
                            });
                        }
                        else
                        {
                            await agent.ApproveToolCallAsync(permissionRequired.Call.Id);
                        }
                        Console.WriteLine("[approval] ALLOWED - safe ls command");
                    }
                    else
                    {
                        if (permissionRequired.Respond != null)
                        {
                            await permissionRequired.Respond("allow", new PermissionRespondOptions
                            {
                                Note = "automated: allow"
                            });
                        }
                        else
                        {
                            await agent.ApproveToolCallAsync(permissionRequired.Call.Id);
                        }
                        Console.WriteLine("[approval] ALLOWED");
                    }
                }
                else if (envelope.Event is PermissionDecidedEvent permissionDecided)
                {
                    Console.WriteLine($"[approval:decided] {permissionDecided.CallId} decision={permissionDecided.Decision} by={permissionDecided.DecidedBy}");
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
                        Console.WriteLine($"[usage] {tokenUsage.InputTokens}+{tokenUsage.OutputTokens} tokens");
                        break;

                    case ToolExecutedEvent toolExecuted:
                        Console.WriteLine($"[tool_executed] {toolExecuted.Call.Name} {toolExecuted.Call.DurationMs ?? 0}ms");
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
