using Kode.Agent.Examples.Shared;
using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Core.Templates;
using Kode.Agent.Sdk.Core.Types;
using AgentImpl = Kode.Agent.Sdk.Core.Agent.Agent;

namespace Kode.Agent.Examples;

/// <summary>
/// Getting started example - simple agent interaction.
/// </summary>
public static class GettingStarted
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Getting Started Example ===\n");

        Directory.CreateDirectory("./workspace");

        // TS-aligned: runtime + template registry
        var deps = RuntimeFactory.CreateRuntime(
            storeDir: "./.kode/getting-started",
            configure: ctx =>
            {
                ctx.RegisterBuiltin([RuntimeFactory.BuiltinGroup.Todo]);
                ctx.Templates.Register(new AgentTemplateDefinition
                {
                    Id = "hello-assistant",
                    SystemPrompt = "You are a helpful engineer. Keep answers short.",
                    Tools = ToolsConfig.Specific("todo_read", "todo_write"),
                    Model = RuntimeFactory.GetDefaultModelId(),
                    Runtime = new TemplateRuntimeConfig
                    {
                        Todo = new TodoConfig { Enabled = true, ReminderOnStart = true }
                    }
                });
            });

        var config = new AgentConfig
        {
            TemplateId = "hello-assistant",
            SandboxOptions = new SandboxOptions
            {
                WorkingDirectory = "./workspace",
                EnforceBoundary = true,
                WatchFiles = false
            }
        };

        var agent = await AgentImpl.CreateAsync(
            agentId: $"getting-started-{Guid.NewGuid():N}",
            config,
            deps);

        Console.WriteLine("[agent] Created agent, subscribing to progress...\n");

        using var cts = new CancellationTokenSource();
        var progressTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var envelope in agent.SubscribeProgress(cancellationToken: cts.Token))
                {
                    if (envelope.Event is TextChunkEvent textChunk)
                    {
                        Console.Write(textChunk.Delta);
                    }
                    if (envelope.Event is DoneEvent)
                    {
                        Console.WriteLine("\n--- conversation complete ---");
                        return;
                    }
                }
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        Console.WriteLine("[user] 你好！帮我总结下这个仓库的核心能力。\n");
        Console.WriteLine("[assistant] ");

        await agent.SendAsync("你好！帮我总结下这个仓库的核心能力。");

        await Task.WhenAny(progressTask, Task.Delay(30_000, cts.Token));
        cts.Cancel();

        try { await progressTask; } catch (OperationCanceledException) { }

        await agent.DisposeAsync();

        Console.WriteLine("\n[agent] Session complete");
    }
}
