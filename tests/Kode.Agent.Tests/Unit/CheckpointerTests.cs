using Kode.Agent.Sdk.Core.Checkpoints;
using Kode.Agent.Sdk.Core.Types;
using Xunit;
using Checkpoint = Kode.Agent.Sdk.Core.Checkpoints.Checkpoint;
using CheckpointConfig = Kode.Agent.Sdk.Core.Checkpoints.CheckpointConfig;

namespace Kode.Agent.Tests.Unit;

public class CheckpointerTests : IDisposable
{
    private readonly string _testDir;
    private readonly FileCheckpointer _checkpointer;

    public CheckpointerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"checkpointer_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _checkpointer = new FileCheckpointer(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }

    private Checkpoint CreateTestCheckpoint(string? id = null, string agentId = "test-agent")
    {
        return new Checkpoint
        {
            Id = id ?? $"cp_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            AgentId = agentId,
            SessionId = "session-1",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Version = "1.0",
            State = new CheckpointAgentState
            {
                Status = "ready",
                StepCount = 0,
                LastSfpIndex = 0
            },
            Messages = [
                Message.User("Hello"),
                Message.Assistant("Hi there!")
            ],
            ToolRecords = [],
            Tools = [],
            Config = new CheckpointConfig
            {
                Model = "claude-sonnet-4-20250514"
            },
            Metadata = new CheckpointMetadata()
        };
    }

    [Fact]
    public async Task SaveAsync_CreatesCheckpointFile()
    {
        // Arrange
        var checkpoint = CreateTestCheckpoint();
        
        // Act
        var checkpointId = await _checkpointer.SaveAsync(checkpoint);
        
        // Assert
        Assert.NotNull(checkpointId);
        var agentDir = Path.Combine(_testDir, checkpoint.AgentId, "checkpoints");
        Assert.True(Directory.Exists(agentDir));
    }

    [Fact]
    public async Task LoadAsync_ReturnsNullForMissingCheckpoint()
    {
        // Act
        var loaded = await _checkpointer.LoadAsync("non-existent-id");
        
        // Assert
        Assert.Null(loaded);
    }

    [Fact]
    public async Task LoadAsync_ReturnsSavedCheckpoint()
    {
        // Arrange
        var checkpoint = CreateTestCheckpoint();
        await _checkpointer.SaveAsync(checkpoint);
        
        // Act
        var loaded = await _checkpointer.LoadAsync(checkpoint.Id);
        
        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(checkpoint.Id, loaded.Id);
        Assert.Equal(checkpoint.AgentId, loaded.AgentId);
        Assert.Equal(2, loaded.Messages.Count);
    }

    [Fact]
    public async Task ListAsync_ReturnsAllCheckpoints()
    {
        // Arrange
        var agentId = "test-agent";
        for (int i = 0; i < 3; i++)
        {
            await _checkpointer.SaveAsync(CreateTestCheckpoint(
                $"cp_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + i}",
                agentId));
        }
        
        // Act
        var checkpoints = await _checkpointer.ListAsync(agentId);
        
        // Assert
        Assert.Equal(3, checkpoints.Count);
    }

    [Fact]
    public async Task DeleteAsync_RemovesCheckpoint()
    {
        // Arrange
        var checkpoint = CreateTestCheckpoint();
        await _checkpointer.SaveAsync(checkpoint);
        
        // Act
        await _checkpointer.DeleteAsync(checkpoint.Id);
        var loaded = await _checkpointer.LoadAsync(checkpoint.Id);
        
        // Assert
        Assert.Null(loaded);
    }

    [Fact]
    public async Task ForkAsync_CreatesNewCheckpoint()
    {
        // Arrange
        var checkpoint = CreateTestCheckpoint(agentId: "agent-1");
        await _checkpointer.SaveAsync(checkpoint);
        
        // Act
        var forkedId = await _checkpointer.ForkAsync(checkpoint.Id, "agent-2");
        var forked = await _checkpointer.LoadAsync(forkedId);
        
        // Assert
        Assert.NotNull(forked);
        Assert.NotEqual(checkpoint.Id, forked.Id);
        Assert.Equal("agent-2", forked.AgentId);
        Assert.Equal(checkpoint.Messages.Count, forked.Messages.Count);
    }

    [Fact]
    public async Task MemoryCheckpointer_SaveAndLoad()
    {
        // Arrange
        var memoryCheckpointer = new MemoryCheckpointer();
        var checkpoint = CreateTestCheckpoint();
        
        // Act
        await memoryCheckpointer.SaveAsync(checkpoint);
        var loaded = await memoryCheckpointer.LoadAsync(checkpoint.Id);
        
        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(checkpoint.Id, loaded.Id);
    }

    [Fact]
    public async Task MemoryCheckpointer_ListReturnsEmpty()
    {
        // Arrange
        var memoryCheckpointer = new MemoryCheckpointer();
        
        // Act
        var list = await memoryCheckpointer.ListAsync("non-existent");
        
        // Assert
        Assert.Empty(list);
    }
}
