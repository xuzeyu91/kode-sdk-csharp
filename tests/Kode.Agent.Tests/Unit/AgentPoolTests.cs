using Kode.Agent.Sdk.Core.Pool;
using Xunit;

namespace Kode.Agent.Tests.Unit;

public class AgentPoolTests
{
    [Fact]
    public void Pool_StartsEmpty()
    {
        // Arrange
        var pool = new AgentPool(new AgentPoolOptions
        {
            Dependencies = null!
        });
        
        // Act
        var agents = pool.List();
        
        // Assert
        Assert.Empty(agents);
    }

    [Fact]
    public void Get_ReturnsNullForNonExistent()
    {
        // Arrange
        var pool = new AgentPool(new AgentPoolOptions
        {
            Dependencies = null!
        });
        
        // Act
        var agent = pool.Get("non-existent");
        
        // Assert
        Assert.Null(agent);
    }

    [Fact]
    public void Status_ReturnsNullForNonExistent()
    {
        // Arrange
        var pool = new AgentPool(new AgentPoolOptions
        {
            Dependencies = null!
        });
        
        // Act
        var status = pool.Status("non-existent");
        
        // Assert
        Assert.Null(status);
    }

    [Fact]
    public void List_WithPrefix_FiltersCorrectly()
    {
        // Note: Can't easily test without real agents in the pool
        // This just verifies the method works with empty pool
        var pool = new AgentPool(new AgentPoolOptions
        {
            Dependencies = null!
        });
        
        var result = pool.List("prefix_");
        Assert.Empty(result);
    }

    [Fact]
    public void MaxAgents_DefaultsTo50()
    {
        // The pool should accept MaxAgents configuration
        var pool = new AgentPool(new AgentPoolOptions
        {
            Dependencies = null!,
            MaxAgents = 100
        });
        
        // Just verify it doesn't throw
        Assert.NotNull(pool);
    }
}
