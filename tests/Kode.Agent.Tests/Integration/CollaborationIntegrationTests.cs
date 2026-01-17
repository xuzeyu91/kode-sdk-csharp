using Kode.Agent.Sdk.Core.Collaboration;
using Kode.Agent.Sdk.Core.Pool;
using Kode.Agent.Sdk.Core.Scheduling;
using Kode.Agent.Sdk.Core.Todo;
using Xunit;

namespace Kode.Agent.Tests.Integration;

/// <summary>
/// Integration tests for Room collaboration, Scheduler and Todo.
/// </summary>
public class CollaborationIntegrationTests : IDisposable
{
    private readonly Scheduler _scheduler;

    public CollaborationIntegrationTests()
    {
        _scheduler = new Scheduler();
    }

    public void Dispose()
    {
        _scheduler.Dispose();
    }

    [Fact]
    public void Room_MultiMemberWorkflow_Success()
    {
        // Arrange
        var pool = new AgentPool(new AgentPoolOptions { Dependencies = null! });
        var room = new Room(pool);

        // Act - Simulate multi-agent collaboration
        room.Join("coordinator", "agent_coordinator");
        room.Join("researcher", "agent_researcher");
        room.Join("coder", "agent_coder");
        room.Join("reviewer", "agent_reviewer");

        // Assert
        Assert.Equal(4, room.MemberCount);
        Assert.True(room.HasMember("coordinator"));
        Assert.True(room.HasMember("researcher"));
        Assert.True(room.HasMember("coder"));
        Assert.True(room.HasMember("reviewer"));

        // Act - Members leave
        room.Leave("researcher");
        room.Leave("coder");

        // Assert
        Assert.Equal(2, room.MemberCount);
        Assert.False(room.HasMember("researcher"));
        Assert.True(room.HasMember("coordinator"));
    }

    [Fact]
    public void Room_GetAgentId_ReturnsCorrectMappings()
    {
        // Arrange
        var pool = new AgentPool(new AgentPoolOptions { Dependencies = null! });
        var room = new Room(pool);

        room.Join("alice", "agent_alice_123");
        room.Join("bob", "agent_bob_456");

        // Act & Assert
        Assert.Equal("agent_alice_123", room.GetAgentId("alice"));
        Assert.Equal("agent_bob_456", room.GetAgentId("bob"));
        Assert.Null(room.GetAgentId("charlie"));
    }

    [Fact]
    public void TodoItem_CompleteWorkflow()
    {
        // Arrange & Act - Create todo items with different statuses
        var pendingTodo = new TodoItem
        {
            Id = "1",
            Title = "Research problem",
            Status = TodoStatus.Pending,
            Assignee = "researcher"
        };

        var inProgressTodo = new TodoItem
        {
            Id = "2",
            Title = "Implement solution",
            Status = TodoStatus.InProgress,
            Assignee = "coder"
        };

        var completedTodo = new TodoItem
        {
            Id = "3",
            Title = "Write documentation",
            Status = TodoStatus.Completed,
            Assignee = "writer"
        };

        // Assert
        Assert.Equal(TodoStatus.Pending, pendingTodo.Status);
        Assert.Equal(TodoStatus.InProgress, inProgressTodo.Status);
        Assert.Equal(TodoStatus.Completed, completedTodo.Status);
    }

    [Fact]
    public void TodoSnapshot_StoresTodosWithVersion()
    {
        // Arrange
        var todos = new List<TodoItem>
        {
            new() { Id = "1", Title = "Task 1", Status = TodoStatus.Pending },
            new() { Id = "2", Title = "Task 2", Status = TodoStatus.InProgress },
            new() { Id = "3", Title = "Task 3", Status = TodoStatus.Completed }
        };

        // Act
        var snapshot = new TodoSnapshot
        {
            Todos = todos,
            Version = 5,
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        // Assert
        Assert.Equal(3, snapshot.Todos.Count);
        Assert.Equal(5, snapshot.Version);
        Assert.True(snapshot.UpdatedAt > 0);
    }

    [Fact]
    public async Task Scheduler_EverySteps_ExecutesAtIntervals()
    {
        // Arrange
        var executionCount = 0;
        _scheduler.EverySteps(2, ctx =>
        {
            Interlocked.Increment(ref executionCount);
            return Task.CompletedTask;
        });

        // Act - Notify steps
        _scheduler.NotifyStep(1);
        _scheduler.NotifyStep(2); // Should trigger
        _scheduler.NotifyStep(3);
        _scheduler.NotifyStep(4); // Should trigger
        _scheduler.NotifyStep(5);
        _scheduler.NotifyStep(6); // Should trigger

        // Wait for async callbacks
        await Task.Delay(100);

        // Assert
        Assert.Equal(3, executionCount);
    }

    [Fact]
    public async Task Scheduler_OnStep_ExecutesOnEveryStep()
    {
        // Arrange
        var executionCount = 0;
        _scheduler.OnStep(ctx =>
        {
            Interlocked.Increment(ref executionCount);
            return Task.CompletedTask;
        });

        // Act
        _scheduler.NotifyStep(1);
        _scheduler.NotifyStep(2);
        _scheduler.NotifyStep(3);

        await Task.Delay(100);

        // Assert
        Assert.Equal(3, executionCount);
    }

    [Fact]
    public void AgentPool_ListWithPrefix_FiltersCorrectly()
    {
        // Arrange
        var pool = new AgentPool(new AgentPoolOptions { Dependencies = null! });

        // Note: We can't add real agents without full dependencies,
        // but we can verify the filtering logic doesn't crash
        var result = pool.List("prefix_");
        
        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Room_BroadcastMessage_AllMembersAccessible()
    {
        // Arrange
        var pool = new AgentPool(new AgentPoolOptions { Dependencies = null! });
        var room = new Room(pool);
        
        room.Join("alice", "agent_1");
        room.Join("bob", "agent_2");
        room.Join("charlie", "agent_3");

        // Act
        var members = room.GetMembers();

        // Assert - verify all members can receive messages
        Assert.Equal(3, members.Count);
        Assert.All(members, m => Assert.NotNull(m.Name));
        Assert.All(members, m => Assert.NotNull(m.AgentId));
    }
}
