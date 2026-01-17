using Kode.Agent.Sdk.Core.Todo;
using Xunit;

namespace Kode.Agent.Tests.Unit;

public class TodoTests
{
    [Fact]
    public void TodoItem_CanBeCreated()
    {
        // Arrange & Act
        var todo = new TodoItem
        {
            Id = "1",
            Title = "Test Task",
            Status = TodoStatus.Pending,
            Assignee = "User",
            Notes = "Some notes"
        };
        
        // Assert
        Assert.Equal("1", todo.Id);
        Assert.Equal("Test Task", todo.Title);
        Assert.Equal(TodoStatus.Pending, todo.Status);
        Assert.Equal("User", todo.Assignee);
        Assert.Equal("Some notes", todo.Notes);
    }

    [Fact]
    public void TodoSnapshot_CanBeCreated()
    {
        // Arrange
        var todos = new List<TodoItem>
        {
            new() { Id = "1", Title = "Task 1", Status = TodoStatus.Pending },
            new() { Id = "2", Title = "Task 2", Status = TodoStatus.InProgress }
        };
        
        // Act
        var snapshot = new TodoSnapshot
        {
            Todos = todos,
            Version = 1,
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        
        // Assert
        Assert.Equal(2, snapshot.Todos.Count);
        Assert.Equal(1, snapshot.Version);
    }

    [Theory]
    [InlineData(TodoStatus.Pending)]
    [InlineData(TodoStatus.InProgress)]
    [InlineData(TodoStatus.Completed)]
    public void TodoStatus_HasExpectedValues(TodoStatus status)
    {
        // Assert
        Assert.True(Enum.IsDefined(typeof(TodoStatus), status));
    }
}
