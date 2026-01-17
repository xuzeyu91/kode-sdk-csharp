using Kode.Agent.Sdk.Core.Types;
using Xunit;

namespace Kode.Agent.Tests.Unit;

public class MessageTests
{
    [Fact]
    public void SystemMessage_HasCorrectRole()
    {
        // Act
        var message = Message.System("You are a helpful assistant");
        
        // Assert
        Assert.Equal(MessageRole.System, message.Role);
        Assert.NotNull(message.Content);
        Assert.Single(message.Content);
        Assert.IsType<TextContent>(message.Content[0]);
        Assert.Equal("You are a helpful assistant", ((TextContent)message.Content[0]).Text);
    }

    [Fact]
    public void UserMessage_HasCorrectRole()
    {
        // Act
        var message = Message.User("Hello");
        
        // Assert
        Assert.Equal(MessageRole.User, message.Role);
        Assert.NotNull(message.Content);
        Assert.Single(message.Content);
        Assert.IsType<TextContent>(message.Content[0]);
        Assert.Equal("Hello", ((TextContent)message.Content[0]).Text);
    }

    [Fact]
    public void AssistantMessage_HasCorrectRole()
    {
        // Act
        var message = Message.Assistant("Hi there!");
        
        // Assert
        Assert.Equal(MessageRole.Assistant, message.Role);
        Assert.NotNull(message.Content);
        Assert.Single(message.Content);
        Assert.IsType<TextContent>(message.Content[0]);
        Assert.Equal("Hi there!", ((TextContent)message.Content[0]).Text);
    }

    [Fact]
    public void AssistantMessage_WithMultipleContentBlocks()
    {
        // Arrange
        var text = new TextContent { Text = "I'll help you" };
        var toolUse = new ToolUseContent 
        { 
            Id = "call-123",
            Name = "test_tool",
            Input = new Dictionary<string, object>()
        };
        
        // Act
        var message = Message.Assistant(text, toolUse);
        
        // Assert
        Assert.Equal(MessageRole.Assistant, message.Role);
        Assert.Equal(2, message.Content.Count);
        Assert.IsType<TextContent>(message.Content[0]);
        Assert.IsType<ToolUseContent>(message.Content[1]);
    }
}
