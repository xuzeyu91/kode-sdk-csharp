using System.Text.Json;
using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Core.Hooks;

namespace Kode.Agent.Examples;

/// <summary>
/// Example demonstrating the Hook system for intercepting agent behavior.
/// </summary>
public static class HooksUsage
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Hooks Example ===\n");

        // Create a hook manager
        var hookManager = new HookManager();

        // 1. Register a logging hook
        Console.WriteLine("1. Registering logging hook...\n");
        var loggingHook = new Hooks
        {
            PreToolUse = async (call, ctx) =>
            {
                Console.WriteLine($"   [pre-tool] About to call: {call.Name}");
                Console.WriteLine($"              Call ID: {call.Id}");
                Console.WriteLine($"              Input: {call.Input}");
                await Task.CompletedTask;
                return null; // Allow execution to continue
            },
            PostToolUse = async (outcome, ctx) =>
            {
                Console.WriteLine($"   [post-tool] Completed: {outcome.Name}");
                Console.WriteLine($"               Duration: {outcome.Duration.TotalMilliseconds}ms");
                Console.WriteLine($"               Error: {outcome.IsError}");
                await Task.CompletedTask;
                return null; // No modification
            },
            PreModel = async request =>
            {
                Console.WriteLine($"   [pre-model] Sending request to: {request.Model}");
                Console.WriteLine($"               Message count: {request.Messages.Count}");
                await Task.CompletedTask;
            },
            PostModel = async response =>
            {
                Console.WriteLine($"   [post-model] Received response from: {response.Model}");
                Console.WriteLine($"                Content blocks: {response.Content.Count}");
                if (response.Usage != null)
                {
                    Console.WriteLine($"                Tokens: {response.Usage.InputTokens} in, {response.Usage.OutputTokens} out");
                }
                await Task.CompletedTask;
            }
        };
        hookManager.Register(loggingHook, HookOrigin.Plugin);

        // 2. Register a security hook that blocks certain tools
        Console.WriteLine("2. Registering security hook...\n");
        var securityHook = new Hooks
        {
            PreToolUse = async (call, ctx) =>
            {
                // Block any tool that tries to delete files
                if (call.Name.Contains("delete", StringComparison.OrdinalIgnoreCase) ||
                    call.Name.Contains("remove", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"   [security] BLOCKED: {call.Name} - dangerous operation!");
                    return HookDecision.Deny("This operation is not allowed for security reasons.");
                }
                
                // Require approval for shell commands
                if (call.Name == "bash_run" || call.Name == "shell_exec")
                {
                    Console.WriteLine($"   [security] Requiring approval for shell command");
                    return HookDecision.RequireApproval("Shell commands require user approval.");
                }
                
                await Task.CompletedTask;
                return null;
            }
        };
        hookManager.Register(securityHook, HookOrigin.ToolTune);

        // 3. Show registered hooks
        Console.WriteLine("3. Registered hooks:");
        foreach (var hook in hookManager.GetRegistered())
        {
            Console.WriteLine($"   - Origin: {hook.Origin}, Hooks: [{string.Join(", ", hook.Names)}]");
        }

        // 4. Simulate hook execution
        Console.WriteLine("\n4. Simulating tool call interception:\n");

        // Safe tool call
        var safeCall = new ToolCall(
            "call_001",
            "fs_read",
            JsonDocument.Parse("{\"path\": \"/home/user/readme.md\"}").RootElement
        );
        var safeContext = CreateContext();
        
        Console.WriteLine("   Testing fs_read (safe tool):");
        var decision = await hookManager.RunPreToolUseAsync(safeCall, safeContext);
        Console.WriteLine($"   Decision: {decision?.GetType().Name ?? "null (allowed)"}\n");

        // Dangerous tool call
        var dangerousCall = new ToolCall(
            "call_002",
            "fs_delete",
            JsonDocument.Parse("{\"path\": \"/home/user/important.txt\"}").RootElement
        );
        
        Console.WriteLine("   Testing fs_delete (dangerous tool):");
        decision = await hookManager.RunPreToolUseAsync(dangerousCall, safeContext);
        Console.WriteLine($"   Decision: {(decision is DenyDecision deny ? $"Denied - {deny.Reason}" : decision?.GetType().Name)}\n");

        // Shell command
        var shellCall = new ToolCall(
            "call_003",
            "bash_run",
            JsonDocument.Parse("{\"command\": \"rm -rf /tmp/test\"}").RootElement
        );
        
        Console.WriteLine("   Testing bash_run (requires approval):");
        decision = await hookManager.RunPreToolUseAsync(shellCall, safeContext);
        Console.WriteLine($"   Decision: {(decision is RequireApprovalDecision approval ? $"Approval Required - {approval.Reason}" : decision?.GetType().Name)}\n");

        // 5. Hook decisions overview
        Console.WriteLine("5. Available hook decisions:");
        Console.WriteLine("   - HookDecision.Allow() - Allow tool execution");
        Console.WriteLine("   - HookDecision.Deny(reason) - Block tool execution");
        Console.WriteLine("   - HookDecision.Skip(mockResult) - Skip with mock result");
        Console.WriteLine("   - HookDecision.RequireApproval(reason) - Require user approval");

        Console.WriteLine("\n=== Hooks Example Complete ===");
    }

    private static ToolContext CreateContext() => new()
    {
        AgentId = "demo-agent",
        CallId = "ctx_001",
        Sandbox = null!
    };
}
