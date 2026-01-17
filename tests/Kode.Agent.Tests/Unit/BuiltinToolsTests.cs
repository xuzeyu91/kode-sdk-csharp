using Kode.Agent.Tools.Builtin;
using Kode.Agent.Tools.Builtin.FileSystem;
using Kode.Agent.Tools.Builtin.Shell;
using Kode.Agent.Tools.Builtin.Todo;
using Xunit;

namespace Kode.Agent.Tests.Unit;

public class BuiltinToolsTests
{
    [Fact]
    public void FsReadTool_HasCorrectName()
    {
        // Arrange
        var tool = new FsReadTool();
        
        // Assert
        Assert.Equal("fs_read", tool.Name);
        Assert.NotNull(tool.Description);
        Assert.NotNull(tool.InputSchema);
    }

    [Fact]
    public void FsWriteTool_HasCorrectName()
    {
        // Arrange
        var tool = new FsWriteTool();
        
        // Assert
        Assert.Equal("fs_write", tool.Name);
        Assert.NotNull(tool.Description);
    }

    [Fact]
    public void BashRunTool_RequiresApproval()
    {
        // Arrange
        var tool = new BashRunTool();
        
        // Assert
        Assert.Equal("bash_run", tool.Name);
        Assert.True(tool.Attributes.RequiresApproval);
        Assert.False(tool.Attributes.ReadOnly);
    }

    [Fact]
    public void BashKillTool_DoesNotRequireApproval()
    {
        // Arrange
        var tool = new BashKillTool();
        
        // Assert
        Assert.Equal("bash_kill", tool.Name);
        Assert.False(tool.Attributes.RequiresApproval);
    }

    [Fact]
    public void BashLogsTool_IsReadOnly()
    {
        // Arrange
        var tool = new BashLogsTool();
        
        // Assert
        Assert.Equal("bash_logs", tool.Name);
        Assert.True(tool.Attributes.ReadOnly);
    }

    [Fact]
    public void TodoReadTool_IsReadOnly()
    {
        // Arrange
        var tool = new TodoReadTool();
        
        // Assert
        Assert.Equal("todo_read", tool.Name);
        Assert.True(tool.Attributes.ReadOnly);
    }

    [Fact]
    public void TodoWriteTool_IsNotReadOnly()
    {
        // Arrange
        var tool = new TodoWriteTool();
        
        // Assert
        Assert.Equal("todo_write", tool.Name);
        Assert.False(tool.Attributes.ReadOnly);
    }

    [Fact]
    public void BuiltinToolKit_RegistersAllTools()
    {
        // Arrange
        var toolkit = new BuiltinToolKit();
        toolkit.Initialize();
        
        // Act
        var tools = toolkit.Tools.ToList();
        
        // Assert
        Assert.Contains(tools, t => t.Name == "fs_read");
        Assert.Contains(tools, t => t.Name == "fs_write");
        Assert.Contains(tools, t => t.Name == "fs_glob");
        Assert.Contains(tools, t => t.Name == "fs_grep");
        Assert.Contains(tools, t => t.Name == "fs_edit");
        Assert.Contains(tools, t => t.Name == "fs_rm");
        Assert.Contains(tools, t => t.Name == "fs_list");
        Assert.Contains(tools, t => t.Name == "bash_run");
        Assert.Contains(tools, t => t.Name == "bash_kill");
        Assert.Contains(tools, t => t.Name == "bash_logs");
        Assert.Contains(tools, t => t.Name == "todo_read");
        Assert.Contains(tools, t => t.Name == "todo_write");
    }
}
