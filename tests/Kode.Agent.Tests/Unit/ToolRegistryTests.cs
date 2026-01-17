using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Tools;
using Xunit;

namespace Kode.Agent.Tests.Unit;

public class ToolRegistryTests
{
    [Fact]
    public void ToolRegistry_CanRegisterTool()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool = new TestTool("test_tool", "Test tool");
        
        // Act
        registry.Register(tool);
        
        // Assert
        Assert.True(registry.Has("test_tool"));
    }

    [Fact]
    public void ToolRegistry_CanGetTool()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool = new TestTool("test_tool", "Test tool");
        registry.Register(tool);
        
        // Act
        var retrieved = registry.Get("test_tool");
        
        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("test_tool", retrieved.Name);
    }

    [Fact]
    public void ToolRegistry_ReturnsNullForMissing()
    {
        // Arrange
        var registry = new ToolRegistry();
        
        // Act
        var retrieved = registry.Get("non_existent");
        
        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public void ToolRegistry_CanListTools()
    {
        // Arrange
        var registry = new ToolRegistry();
        registry.Register(new TestTool("tool1", "Tool 1"));
        registry.Register(new TestTool("tool2", "Tool 2"));
        registry.Register(new TestTool("tool3", "Tool 3"));
        
        // Act
        var tools = registry.List();
        
        // Assert
        Assert.Equal(3, tools.Count);
    }

    [Fact]
    public void ToolRegistry_HasReturnsFalseAfterNotRegistered()
    {
        // Arrange
        var registry = new ToolRegistry();
        
        // Act & Assert
        Assert.False(registry.Has("non_existent"));
    }

    [Fact]
    public void ToolRegistry_CanRegisterWithFactory()
    {
        // Arrange
        var registry = new ToolRegistry();
        
        // Act
        registry.Register("factory_tool", ctx => new TestTool("factory_tool", "Created via factory"));
        
        // Assert
        Assert.True(registry.Has("factory_tool"));
    }

    [Fact]
    public void ToolRegistry_CreateUsesFactory()
    {
        // Arrange
        var registry = new ToolRegistry();
        registry.Register("factory_tool", ctx => new TestTool("factory_tool", "Created via factory"));
        
        // Act
        var tool = registry.Create("factory_tool");
        
        // Assert
        Assert.NotNull(tool);
        Assert.Equal("factory_tool", tool.Name);
    }

    [Fact]
    public void ToolRegistry_CreateThrowsForMissing()
    {
        // Arrange
        var registry = new ToolRegistry();
        
        // Act & Assert
        Assert.Throws<Kode.Agent.Sdk.Core.ToolNotFoundException>(() => 
            registry.Create("non_existent"));
    }

    private class TestTool : ITool
    {
        public string Name { get; }
        public string Description { get; }
        public object InputSchema => new { type = "object" };
        public ToolAttributes Attributes { get; }

        public TestTool(string name, string description, bool readOnly = true)
        {
            Name = name;
            Description = description;
            Attributes = new ToolAttributes { ReadOnly = readOnly };
        }

        public ValueTask<string?> GetPromptAsync(ToolContext context) 
            => ValueTask.FromResult<string?>(null);

        public Task<ToolResult> ExecuteAsync(object arguments, ToolContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(ToolResult.Ok(new { output = "test" }));

        public ToolDescriptor ToDescriptor() => new()
        {
            Source = ToolSource.Registered,
            Name = Name
        };
    }
}
