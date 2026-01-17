using Kode.Agent.Sdk.Core.Templates;
using Xunit;

namespace Kode.Agent.Tests.Unit;

public class TemplateRegistryTests
{
    [Fact]
    public void TemplateRegistry_CanRegisterAndRetrieve()
    {
        // Arrange
        var registry = new AgentTemplateRegistry();
        var template = new AgentTemplateDefinition
        {
            Id = "test-template",
            Name = "Test Template",
            SystemPrompt = "You are a test agent."
        };
        
        // Act
        registry.Register(template);
        var retrieved = registry.Get("test-template");
        
        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("Test Template", retrieved.Name);
        Assert.Equal("You are a test agent.", retrieved.SystemPrompt);
    }

    [Fact]
    public void TemplateRegistry_ThrowsForMissingTemplate()
    {
        // Arrange
        var registry = new AgentTemplateRegistry();
        
        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() => registry.Get("non-existent"));
    }

    [Fact]
    public void TemplateRegistry_TryGetReturnsFalseForMissing()
    {
        // Arrange
        var registry = new AgentTemplateRegistry();
        
        // Act
        var result = registry.TryGet("non-existent", out var template);
        
        // Assert
        Assert.False(result);
        Assert.Null(template);
    }

    [Fact]
    public void TemplateRegistry_CanListAllTemplates()
    {
        // Arrange
        var registry = new AgentTemplateRegistry();
        registry.Register(new AgentTemplateDefinition { Id = "t1", Name = "Template 1", SystemPrompt = "Prompt 1" });
        registry.Register(new AgentTemplateDefinition { Id = "t2", Name = "Template 2", SystemPrompt = "Prompt 2" });
        registry.Register(new AgentTemplateDefinition { Id = "t3", Name = "Template 3", SystemPrompt = "Prompt 3" });
        
        // Act
        var templates = registry.List();
        
        // Assert
        Assert.Equal(3, templates.Count);
        Assert.Contains(templates, t => t.Id == "t1");
        Assert.Contains(templates, t => t.Id == "t2");
        Assert.Contains(templates, t => t.Id == "t3");
    }

    [Fact]
    public void TemplateRegistry_CanRemoveTemplate()
    {
        // Arrange
        var registry = new AgentTemplateRegistry();
        registry.Register(new AgentTemplateDefinition { Id = "to-remove", Name = "To Remove", SystemPrompt = "Prompt" });
        
        // Act
        var removed = registry.Remove("to-remove");
        var hasTemplate = registry.Has("to-remove");
        
        // Assert
        Assert.True(removed);
        Assert.False(hasTemplate);
    }

    [Fact]
    public void TemplateRegistry_RemoveReturnsFalseForMissing()
    {
        // Arrange
        var registry = new AgentTemplateRegistry();
        
        // Act
        var removed = registry.Remove("non-existent");
        
        // Assert
        Assert.False(removed);
    }

    [Fact]
    public void TemplateRegistry_HasReturnsCorrectly()
    {
        // Arrange
        var registry = new AgentTemplateRegistry();
        registry.Register(new AgentTemplateDefinition 
        { 
            Id = "coder", 
            Name = "Coder Template",
            SystemPrompt = "You are a coder."
        });
        
        // Assert
        Assert.True(registry.Has("coder"));
        Assert.False(registry.Has("non-existent"));
    }

    [Fact]
    public void TemplateRegistry_ThrowsForEmptyId()
    {
        // Arrange
        var registry = new AgentTemplateRegistry();
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => registry.Register(
            new AgentTemplateDefinition { Id = "", SystemPrompt = "Prompt" }));
    }

    [Fact]
    public void TemplateRegistry_ThrowsForEmptySystemPrompt()
    {
        // Arrange
        var registry = new AgentTemplateRegistry();
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => registry.Register(
            new AgentTemplateDefinition { Id = "test", SystemPrompt = "" }));
    }

    [Fact]
    public void TemplateRegistry_BulkRegisterWorks()
    {
        // Arrange
        var registry = new AgentTemplateRegistry();
        var templates = new[]
        {
            new AgentTemplateDefinition { Id = "t1", SystemPrompt = "Prompt 1" },
            new AgentTemplateDefinition { Id = "t2", SystemPrompt = "Prompt 2" },
        };
        
        // Act
        registry.BulkRegister(templates);
        
        // Assert
        Assert.Equal(2, registry.List().Count);
        Assert.True(registry.Has("t1"));
        Assert.True(registry.Has("t2"));
    }

    [Fact]
    public void TemplateRegistry_ClearRemovesAll()
    {
        // Arrange
        var registry = new AgentTemplateRegistry();
        registry.Register(new AgentTemplateDefinition { Id = "t1", SystemPrompt = "Prompt 1" });
        registry.Register(new AgentTemplateDefinition { Id = "t2", SystemPrompt = "Prompt 2" });
        
        // Act
        registry.Clear();
        
        // Assert
        Assert.Empty(registry.List());
    }
}
