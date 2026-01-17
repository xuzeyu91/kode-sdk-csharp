using Kode.Agent.Sdk.Core.Collaboration;
using Kode.Agent.Sdk.Core.Pool;
using Xunit;

namespace Kode.Agent.Tests.Unit;

public class RoomTests
{
    private readonly AgentPool _pool;
    private readonly Room _room;

    public RoomTests()
    {
        _pool = new AgentPool(new AgentPoolOptions { Dependencies = null! });
        _room = new Room(_pool);
    }

    [Fact]
    public void Join_AddsMemberToRoom()
    {
        // Act
        _room.Join("alice", "agent_1");
        
        // Assert
        Assert.True(_room.HasMember("alice"));
        Assert.Equal(1, _room.MemberCount);
    }

    [Fact]
    public void Join_ThrowsWhenMemberExists()
    {
        // Arrange
        _room.Join("alice", "agent_1");
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _room.Join("alice", "agent_2"));
    }

    [Fact]
    public void Leave_RemovesMemberFromRoom()
    {
        // Arrange
        _room.Join("alice", "agent_1");
        
        // Act
        _room.Leave("alice");
        
        // Assert
        Assert.False(_room.HasMember("alice"));
        Assert.Equal(0, _room.MemberCount);
    }

    [Fact]
    public void Leave_DoesNotThrowForNonExistingMember()
    {
        // Act (should not throw)
        _room.Leave("nonexistent");
    }

    [Fact]
    public void GetMembers_ReturnsAllMembers()
    {
        // Arrange
        _room.Join("alice", "agent_1");
        _room.Join("bob", "agent_2");
        _room.Join("charlie", "agent_3");
        
        // Act
        var members = _room.GetMembers();
        
        // Assert
        Assert.Equal(3, members.Count);
        Assert.Contains(members, m => m.Name == "alice");
        Assert.Contains(members, m => m.Name == "bob");
        Assert.Contains(members, m => m.Name == "charlie");
    }

    [Fact]
    public void HasMember_ReturnsTrueForExisting()
    {
        // Arrange
        _room.Join("alice", "agent_1");
        
        // Assert
        Assert.True(_room.HasMember("alice"));
        Assert.False(_room.HasMember("bob"));
    }

    [Fact]
    public void GetAgentId_ReturnsCorrectId()
    {
        // Arrange
        _room.Join("alice", "agent_123");
        
        // Act
        var agentId = _room.GetAgentId("alice");
        
        // Assert
        Assert.Equal("agent_123", agentId);
    }

    [Fact]
    public void GetAgentId_ReturnsNullForNonExisting()
    {
        // Act
        var agentId = _room.GetAgentId("nonexistent");
        
        // Assert
        Assert.Null(agentId);
    }

    [Fact]
    public void Name_CanBeSetAndRetrieved()
    {
        // Act
        _room.Name = "dev-team";
        
        // Assert
        Assert.Equal("dev-team", _room.Name);
    }

    [Fact]
    public void GetHistory_ReturnsEmptyInitially()
    {
        // Act
        var history = _room.GetHistory();
        
        // Assert
        Assert.Empty(history);
    }

    [Fact]
    public void ClearHistory_ClearsAllMessages()
    {
        // Note: Can't easily test SayAsync without a mock agent
        // So we just verify ClearHistory doesn't throw
        _room.ClearHistory();
        Assert.Empty(_room.GetHistory());
    }

    [Fact]
    public void MemberCount_ReflectsCurrentState()
    {
        // Initial
        Assert.Equal(0, _room.MemberCount);
        
        // Add members
        _room.Join("alice", "agent_1");
        Assert.Equal(1, _room.MemberCount);
        
        _room.Join("bob", "agent_2");
        Assert.Equal(2, _room.MemberCount);
        
        // Remove member
        _room.Leave("alice");
        Assert.Equal(1, _room.MemberCount);
    }
}
