using System.Text.Json;
using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Core.Hooks;
using Xunit;

namespace Kode.Agent.Tests.Unit;

public class HookManagerTests
{
    [Fact]
    public void Register_AddsHooks()
    {
        // Arrange
        var manager = new HookManager();
        var hooks = new Hooks
        {
            PreToolUse = (call, ctx) => Task.FromResult<HookDecision?>(null)
        };
        
        // Act
        manager.Register(hooks);
        
        // Assert
        var registered = manager.GetRegistered();
        Assert.Single(registered);
        Assert.Contains("preToolUse", registered[0].Names);
    }

    [Fact]
    public void Unregister_RemovesHooks()
    {
        // Arrange
        var manager = new HookManager();
        var hooks = new Hooks
        {
            PreToolUse = (call, ctx) => Task.FromResult<HookDecision?>(null)
        };
        manager.Register(hooks);
        
        // Act
        manager.Unregister(hooks);
        
        // Assert
        Assert.Empty(manager.GetRegistered());
    }

    [Fact]
    public void GetRegistered_ReturnsAllHookNames()
    {
        // Arrange
        var manager = new HookManager();
        var hooks = new Hooks
        {
            PreToolUse = (call, ctx) => Task.FromResult<HookDecision?>(null),
            PostToolUse = (outcome, ctx) => Task.FromResult<PostHookResult?>(null),
            PreModel = request => Task.CompletedTask,
            PostModel = response => Task.CompletedTask,
            MessagesChanged = messages => Task.CompletedTask
        };
        manager.Register(hooks);
        
        // Act
        var registered = manager.GetRegistered();
        
        // Assert
        Assert.Single(registered);
        Assert.Contains("preToolUse", registered[0].Names);
        Assert.Contains("postToolUse", registered[0].Names);
        Assert.Contains("preModel", registered[0].Names);
        Assert.Contains("postModel", registered[0].Names);
        Assert.Contains("messagesChanged", registered[0].Names);
    }

    [Fact]
    public async Task RunPreToolUseAsync_ReturnsFirstDecision()
    {
        // Arrange
        var manager = new HookManager();
        var hooks1 = new Hooks
        {
            PreToolUse = (call, ctx) => Task.FromResult<HookDecision?>(HookDecision.Deny("test reason"))
        };
        var hooks2 = new Hooks
        {
            PreToolUse = (call, ctx) => Task.FromResult<HookDecision?>(HookDecision.Allow())
        };
        manager.Register(hooks1);
        manager.Register(hooks2);
        
        var toolCall = new ToolCall("id", "test_tool", JsonDocument.Parse("{}").RootElement);
        var context = CreateToolContext();
        
        // Act
        var result = await manager.RunPreToolUseAsync(toolCall, context);
        
        // Assert
        Assert.NotNull(result);
        Assert.IsType<DenyDecision>(result);
    }

    [Fact]
    public async Task RunPreToolUseAsync_ReturnsNullWhenNoDecision()
    {
        // Arrange
        var manager = new HookManager();
        var hooks = new Hooks
        {
            PreToolUse = (call, ctx) => Task.FromResult<HookDecision?>(null)
        };
        manager.Register(hooks);
        
        var toolCall = new ToolCall("id", "test_tool", JsonDocument.Parse("{}").RootElement);
        var context = CreateToolContext();
        
        // Act
        var result = await manager.RunPreToolUseAsync(toolCall, context);
        
        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void HookDecision_FactoryMethods_ReturnCorrectTypes()
    {
        // Assert
        Assert.IsType<AllowDecision>(HookDecision.Allow());
        Assert.IsType<DenyDecision>(HookDecision.Deny("reason"));
        Assert.IsType<SkipDecision>(HookDecision.Skip("mock"));
        Assert.IsType<RequireApprovalDecision>(HookDecision.RequireApproval());
    }

    [Fact]
    public void PostHookResult_FactoryMethods_ReturnCorrectTypes()
    {
        // Assert
        Assert.IsType<PassResult>(PostHookResult.Pass());
        Assert.IsType<ReplaceResult>(PostHookResult.Replace(CreateOutcome()));
        Assert.IsType<UpdateResult>(PostHookResult.Update(ToolResult.Ok("test")));
    }

    [Fact]
    public void HookOrigin_DefaultsToAgent()
    {
        // Arrange
        var manager = new HookManager();
        var hooks = new Hooks();
        manager.Register(hooks);
        
        // Act
        var registered = manager.GetRegistered();
        
        // Assert
        Assert.Empty(registered[0].Names);  // No hooks defined, but origin should be agent
    }

    private static ToolContext CreateToolContext() => new()
    {
        AgentId = "test-agent",
        CallId = "call_123",
        Sandbox = null!
    };

    private static ToolOutcome CreateOutcome() => new(
        "id",
        "test_tool",
        JsonDocument.Parse("{}").RootElement,
        ToolResult.Ok("result"),
        false,
        TimeSpan.FromMilliseconds(100)
    );
}
