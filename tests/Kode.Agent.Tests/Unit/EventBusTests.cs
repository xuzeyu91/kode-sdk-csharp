using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Core.Events;
using Xunit;

namespace Kode.Agent.Tests.Unit;

public class EventBusTests : IAsyncDisposable
{
    private readonly EventBus _eventBus;

    public EventBusTests()
    {
        _eventBus = new EventBus();
    }

    public async ValueTask DisposeAsync()
    {
        await _eventBus.DisposeAsync();
    }

    [Fact]
    public void EmitProgress_ReturnsEnvelope()
    {
        // Arrange
        var evt = new TextChunkEvent { Type = "text_chunk", Step = 0, Delta = "Hello" };
        
        // Act
        var envelope = _eventBus.EmitProgress(evt);
        
        // Assert
        Assert.Equal("Hello", envelope.Event.Delta);
        Assert.Equal("progress", envelope.Event.Channel);
        Assert.True(envelope.Cursor >= 0);
    }

    [Fact]
    public void EmitControl_ReturnsEnvelope()
    {
        // Arrange
        var evt = new PermissionRequiredEvent 
        { 
            Type = "permission_required",
            Call = new ToolCallSnapshot
            {
                Id = "call_1",
                Name = "fs_write",
                State = ToolCallState.ApprovalRequired,
                Approval = new ToolCallApproval { Required = true }
            }
        };
        
        // Act
        var envelope = _eventBus.EmitControl(evt);
        
        // Assert
        Assert.Equal("call_1", envelope.Event.Call.Id);
        Assert.Equal("control", envelope.Event.Channel);
    }

    [Fact]
    public void EmitMonitor_ReturnsEnvelope()
    {
        // Arrange
        var evt = new StateChangedEvent
        {
            Type = "state_changed",
            State = AgentRuntimeState.Working
        };
        
        // Act
        var envelope = _eventBus.EmitMonitor(evt);
        
        // Assert
        Assert.Equal(AgentRuntimeState.Working, envelope.Event.State);
        Assert.Equal("monitor", envelope.Event.Channel);
    }

    [Fact]
    public void OnControl_SubscribesAndUnsubscribes()
    {
        // Arrange
        var receivedCount = 0;
        
        // Act
        var subscription = _eventBus.OnControl<PermissionRequiredEvent>(evt =>
        {
            receivedCount++;
        });
        
        // Emit event
        _eventBus.EmitControl(new PermissionRequiredEvent
        {
            Type = "permission_required",
            Call = new ToolCallSnapshot
            {
                Id = "test",
                Name = "test_tool",
                State = ToolCallState.ApprovalRequired,
                Approval = new ToolCallApproval { Required = true }
            }
        });
        
        // Assert - handler should be registered (actual invocation is async)
        Assert.NotNull(subscription);
        subscription.Dispose();
    }

    [Fact]
    public void OnMonitor_SubscribesAndUnsubscribes()
    {
        // Arrange
        var receivedCount = 0;
        
        // Act
        var subscription = _eventBus.OnMonitor<StateChangedEvent>(evt =>
        {
            receivedCount++;
        });
        
        // Assert
        Assert.NotNull(subscription);
        subscription.Dispose();
    }

    [Fact]
    public async Task SubscribeAsync_YieldsEmittedEvents()
    {
        // Arrange
        var receivedEvents = new List<EventEnvelope>();
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        
        // Start subscription in background
        var subscribeTask = Task.Run(async () =>
        {
            await foreach (var envelope in _eventBus.SubscribeAsync(EventChannel.Progress, cancellationToken: cts.Token))
            {
                receivedEvents.Add(envelope);
            }
        });
        
        // Emit event
        await Task.Delay(10);
        _eventBus.EmitProgress(new TextChunkEvent { Type = "text_chunk", Step = 0, Delta = "Test" });
        
        // Wait for subscription to complete
        try
        {
            await subscribeTask;
        }
        catch (OperationCanceledException) { }
        
        // Assert
        Assert.Single(receivedEvents);
    }

    [Fact]
    public void Cursor_IncrementsWithEachEmit()
    {
        // Arrange & Act
        var env1 = _eventBus.EmitProgress(new TextChunkEvent { Type = "text_chunk", Step = 0, Delta = "1" });
        var env2 = _eventBus.EmitProgress(new TextChunkEvent { Type = "text_chunk", Step = 0, Delta = "2" });
        var env3 = _eventBus.EmitProgress(new TextChunkEvent { Type = "text_chunk", Step = 0, Delta = "3" });
        
        // Assert
        Assert.True(env2.Cursor > env1.Cursor);
        Assert.True(env3.Cursor > env2.Cursor);
    }

    [Fact]
    public void Bookmark_ContainsSequenceAndTimestamp()
    {
        // Act
        var envelope = _eventBus.EmitProgress(new TextChunkEvent { Type = "text_chunk", Step = 0, Delta = "Test" });
        
        // Assert
        Assert.True(envelope.Bookmark.Seq >= 0);
        Assert.True(envelope.Bookmark.Timestamp > 0);
    }
}
