using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Core.Templates;
using Kode.Agent.Sdk.Tools;
using Kode.Agent.Tools.Builtin;
using Xunit;

namespace Kode.Agent.Tests.Integration;

/// <summary>
/// Integration tests for Template Registry and Tool Registry working together.
/// </summary>
public class RegistryIntegrationTests
{
    [Fact]
    public void ToolRegistry_RegisterBuiltinToolkit_AllToolsAvailable()
    {
        // Arrange
        var registry = new ToolRegistry();
        var toolkit = new BuiltinToolKit();
        toolkit.Initialize();

        // Act
        foreach (var tool in toolkit.Tools)
        {
            registry.Register(tool);
        }

        // Assert - All builtin tools should be registered
        Assert.True(registry.Has("fs_read"));
        Assert.True(registry.Has("fs_write"));
        Assert.True(registry.Has("fs_edit"));
        Assert.True(registry.Has("fs_glob"));
        Assert.True(registry.Has("fs_grep"));
        Assert.True(registry.Has("fs_list"));
        Assert.True(registry.Has("fs_rm"));
        Assert.True(registry.Has("bash_run"));
        Assert.True(registry.Has("bash_kill"));
        Assert.True(registry.Has("bash_logs"));
        Assert.True(registry.Has("todo_read"));
        Assert.True(registry.Has("todo_write"));
    }

    [Fact]
    public void ToolRegistry_ToolAttributes_ReflectCorrectCapabilities()
    {
        // Arrange
        var registry = new ToolRegistry();
        var toolkit = new BuiltinToolKit();
        toolkit.Initialize();

        foreach (var tool in toolkit.Tools)
        {
            registry.Register(tool);
        }

        // Act & Assert - Read-only tools
        var fsReadTool = registry.Get("fs_read");
        Assert.NotNull(fsReadTool);
        Assert.True(fsReadTool.Attributes.ReadOnly);

        var fsListTool = registry.Get("fs_list");
        Assert.NotNull(fsListTool);
        Assert.True(fsListTool.Attributes.ReadOnly);

        // Act & Assert - Write tools are not read-only
        var fsWriteTool = registry.Get("fs_write");
        Assert.NotNull(fsWriteTool);
        Assert.False(fsWriteTool.Attributes.ReadOnly);

        // Act & Assert - Bash requires approval
        var bashRunTool = registry.Get("bash_run");
        Assert.NotNull(bashRunTool);
        Assert.True(bashRunTool.Attributes.RequiresApproval);
    }

    [Fact]
    public void AgentTemplateRegistry_RegisterAndRetrieve_Works()
    {
        // Arrange
        var registry = new AgentTemplateRegistry();

        var coderTemplate = new AgentTemplateDefinition
        {
            Id = "coder",
            Name = "Coder Agent",
            SystemPrompt = "You are an expert programmer. Write clean, efficient code."
        };

        var reviewerTemplate = new AgentTemplateDefinition
        {
            Id = "reviewer",
            Name = "Code Reviewer",
            SystemPrompt = "You are a code reviewer. Review code for quality and suggest improvements."
        };

        // Act
        registry.Register(coderTemplate);
        registry.Register(reviewerTemplate);

        // Assert
        var retrievedCoder = registry.Get("coder");
        Assert.NotNull(retrievedCoder);
        Assert.Equal("Coder Agent", retrievedCoder.Name);
        Assert.Contains("expert programmer", retrievedCoder.SystemPrompt);

        var retrievedReviewer = registry.Get("reviewer");
        Assert.NotNull(retrievedReviewer);
        Assert.Equal("Code Reviewer", retrievedReviewer.Name);
    }

    [Fact]
    public void AgentTemplateRegistry_ListTemplates_ReturnsAll()
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
    public void ToolRegistry_FactoryRegistration_CreatesNewInstances()
    {
        // Arrange
        var registry = new ToolRegistry();
        var instanceCount = 0;

        registry.Register("counter_tool", ctx =>
        {
            instanceCount++;
            return new TestTool($"counter_{instanceCount}", $"Counter tool {instanceCount}");
        });

        // Act
        var tool1 = registry.Create("counter_tool");
        var tool2 = registry.Create("counter_tool");
        var tool3 = registry.Create("counter_tool");

        // Assert - Each create should invoke factory
        Assert.Equal(3, instanceCount);
        Assert.Equal("counter_1", tool1?.Name);
        Assert.Equal("counter_2", tool2?.Name);
        Assert.Equal("counter_3", tool3?.Name);
    }

    [Fact]
    public void ToolRegistry_GetInputSchema_ReturnsValidSchema()
    {
        // Arrange
        var registry = new ToolRegistry();
        var toolkit = new BuiltinToolKit();
        toolkit.Initialize();

        foreach (var tool in toolkit.Tools)
        {
            registry.Register(tool);
        }

        // Act
        var fsWriteTool = registry.Get("fs_write");

        // Assert
        Assert.NotNull(fsWriteTool);
        Assert.NotNull(fsWriteTool.InputSchema);
    }

    [Fact]
    public void BuiltinToolKit_Initialize_RegistersAllTools()
    {
        // Arrange
        var toolkit = new BuiltinToolKit();

        // Act
        toolkit.Initialize();
        var tools = toolkit.Tools.ToList();

        // Assert
        Assert.True(tools.Count >= 12, $"Expected at least 12 tools, got {tools.Count}");
        
        var toolNames = tools.Select(t => t.Name).ToHashSet();
        Assert.Contains("fs_read", toolNames);
        Assert.Contains("fs_write", toolNames);
        Assert.Contains("bash_run", toolNames);
        Assert.Contains("todo_read", toolNames);
    }

    [Fact]
    public void AgentTemplateRegistry_TryGet_ReturnsFalseForMissing()
    {
        // Arrange
        var registry = new AgentTemplateRegistry();

        // Act
        var result = registry.TryGet("non-existent", out var template);

        // Assert
        Assert.False(result);
        Assert.Null(template);
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
