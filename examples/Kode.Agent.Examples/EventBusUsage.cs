using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Core.Events;

namespace Kode.Agent.Examples;

/// <summary>
/// Example demonstrating the EventBus for event-driven architecture.
/// </summary>
public static class EventBusUsage
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== EventBus Example ===\n");

        // Create an event bus
        await using var eventBus = new EventBus();

        // 1. Subscribe to control events
        Console.WriteLine("1. Setting up control event handlers:\n");
        
        using var permissionHandler = eventBus.OnControl<PermissionRequiredEvent>(evt =>
        {
            Console.WriteLine($"   [CONTROL] Permission required for {evt.Call.Name}");
            Console.WriteLine($"             Call ID: {evt.Call.Id}");
        });
        Console.WriteLine("   ✓ Registered permission event handler");

        // 2. Subscribe to monitor events
        Console.WriteLine("\n2. Setting up monitor event handlers:\n");
        
        using var stateHandler = eventBus.OnMonitor<StateChangedEvent>(evt =>
        {
            Console.WriteLine($"   [MONITOR] State changed: {evt.State}");
        });
        Console.WriteLine("   ✓ Registered state change handler");

        // 3. Emit various events
        Console.WriteLine("\n3. Emitting events:\n");

        // Progress events
        Console.WriteLine("   Emitting progress events...");
        var textEnv = eventBus.EmitProgress(new TextChunkEvent 
        { 
            Type = "text_chunk",
            Step = 0,
            Delta = "Hello, world!"
        });
        Console.WriteLine($"   ✓ TextChunk emitted (cursor: {textEnv.Cursor})");

        var toolCall = new ToolCallSnapshot
        {
            Id = "call_001",
            Name = "fs_read",
            State = ToolCallState.Executing,
            Approval = new ToolCallApproval { Required = false },
            InputPreview = new { path = "/readme.md" }
        };

        var toolStartEnv = eventBus.EmitProgress(new ToolStartEvent
        {
            Type = "tool:start",
            Call = toolCall
        });
        Console.WriteLine($"   ✓ ToolStart emitted (cursor: {toolStartEnv.Cursor})");

        var toolEndEnv = eventBus.EmitProgress(new ToolEndEvent
        {
            Type = "tool:end",
            Call = toolCall with { State = ToolCallState.Completed, Result = "File contents..." }
        });
        Console.WriteLine($"   ✓ ToolEnd emitted (cursor: {toolEndEnv.Cursor})");

        // Control events
        Console.WriteLine("\n   Emitting control event...");
        eventBus.EmitControl(new PermissionRequiredEvent
        {
            Type = "permission_required",
            Call = new ToolCallSnapshot
            {
                Id = "call_002",
                Name = "bash_run",
                State = ToolCallState.ApprovalRequired,
                Approval = new ToolCallApproval { Required = true },
                InputPreview = new { command = "npm install" }
            }
        });

        // Monitor events
        Console.WriteLine("\n   Emitting monitor event...");
        eventBus.EmitMonitor(new StateChangedEvent
        {
            Type = "state_changed",
            State = AgentRuntimeState.Working
        });

        // 4. Show event channels
        Console.WriteLine("\n4. Event channels explained:");
        Console.WriteLine("   - Progress: UI streaming (text chunks, tool events)");
        Console.WriteLine("   - Control: Approval flow (permission requests)");
        Console.WriteLine("   - Monitor: Observability (state changes, metrics)");

        // 5. Bookmark example
        Console.WriteLine("\n5. Bookmark for resumption:");
        var lastEnv = eventBus.EmitProgress(new DoneEvent
        {
            Type = "done",
            Step = 0,
            Reason = "completed"
        });
        Console.WriteLine($"   Last bookmark: seq={lastEnv.Bookmark.Seq}, ts={lastEnv.Bookmark.Timestamp}");
        Console.WriteLine("   (Bookmarks can be used to resume event streams)");

        // 6. Async subscription example
        Console.WriteLine("\n6. Async subscription example:");
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        
        // Start subscription in background
        var subscriptionTask = Task.Run(async () =>
        {
            var count = 0;
            try
            {
                await foreach (var envelope in eventBus.SubscribeAsync(
                    EventChannel.Progress, 
                    cancellationToken: cts.Token))
                {
                    count++;
                    Console.WriteLine($"   [STREAM] Received: {envelope.Event.Type}");
                }
            }
            catch (OperationCanceledException) 
            {
                Console.WriteLine($"   Subscription ended after {count} events");
            }
        });

        // Emit some events for the subscription
        await Task.Delay(50);
        eventBus.EmitProgress(new TextChunkEvent { Type = "text_chunk", Step = 0, Delta = "Streaming..." });
        eventBus.EmitProgress(new TextChunkEvent { Type = "text_chunk", Step = 0, Delta = "More text..." });
        
        await subscriptionTask;

        Console.WriteLine("\n=== EventBus Example Complete ===");
    }
}
