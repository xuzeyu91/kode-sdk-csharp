# Kode.Agent.Tools.Builtin

[中文文档](./README-zh.md) | English

Built-in tools for the Kode Agent SDK. This package provides a comprehensive set of tools that Agents can use to interact with the file system, execute shell commands, manage todos, and more.

## Directory Structure

Tools are organized by category into subdirectories:

```
src/Kode.Agent.Tools.Builtin/
├── FileSystem/     # File system operation tools (fs_*)
├── Shell/          # Shell command execution tools (bash_*)
├── Todo/           # Task list management tools (todo_*)
├── Skills/         # Skills system tools (skill_*)
├── Task/           # Sub-agent delegation tools (task_*)
├── ToolContextFilePool.cs
└── ServiceCollectionExtensions.cs
```

## Tool Categories

### FileSystem Tools (`fs_*`)

| Tool | Description | Permission |
|------|-------------|------------|
| `fs_read` | Read file contents with optional line range | Read-only |
| `fs_write` | Write content to a file (creates/overwrites) | Approval |
| `fs_edit` | Replace exact text in a file | Approval |
| `fs_multi_edit` | Apply multiple edits to a file | Approval |
| `fs_glob` | Find files matching glob patterns | Read-only |
| `fs_grep` | Search text in files with regex support | Read-only |
| `fs_list` | List directory contents | Read-only |
| `fs_rm` | Remove files or directories | Approval |

**Namespace:** `Kode.Agent.Tools.Builtin.FileSystem`

### Shell Tools (`bash_*`)

| Tool | Description | Permission |
|------|-------------|------------|
| `bash_run` | Execute shell commands (with background mode support) | Approval |
| `bash_kill` | Terminate background shell processes | Deny (default) |
| `bash_logs` | Retrieve logs from background processes | Read-only |

**Namespace:** `Kode.Agent.Tools.Builtin.Shell`

### Todo Tools (`todo_*`)

| Tool | Description | Permission |
|------|-------------|------------|
| `todo_read` | Read current todo list | Read-only |
| `todo_write` | Create/update/delete todos | Read-only |

**Namespace:** `Kode.Agent.Tools.Builtin.Todo`

### Skills Tools (`skill_*`)

| Tool | Description | Permission |
|------|-------------|------------|
| `skill_list` | List available skills and their status | Read-only |
| `skill_activate` | Activate a specific skill | Read-only |
| `skill_resource` | Read skill resource files | Read-only |

**Namespace:** `Kode.Agent.Tools.Builtin.Skills`

### Task Tools (`task_*`)

| Tool | Description | Permission |
|------|-------------|------------|
| `task_run` | Delegate tasks to specialized sub-agents | Auto |

**Namespace:** `Kode.Agent.Tools.Builtin.Task`

## Usage

### Register All Tools

```csharp
using Kode.Agent.Tools.Builtin;

// Register with tool registry
var registry = new ToolRegistry();
registry.RegisterBuiltinTools();

// Or with dependency injection
services.AddBuiltinTools();
```

### Register Specific Categories

```csharp
// File system tools only
using Kode.Agent.Tools.Builtin.FileSystem;

registry.Register(new FsReadTool());
registry.Register(new FsWriteTool());
// ...

// Shell tools only
using Kode.Agent.Tools.Builtin.Shell;

registry.Register(new BashRunTool());
registry.Register(new BashKillTool());
// ...
```

### Permission Configuration

Recommended permission configuration for all built-in tools:

```csharp
var permissions = new PermissionConfig
{
    Mode = "auto",
    // Allow all tools (including MCP tools)
    AllowTools = ["*"],
    // Require approval for dangerous operations
    RequireApprovalTools = [
        "bash_run",
        "fs_rm",
        "fs_write",
        "fs_edit",
        "fs_multi_edit",
        "bash_kill"
    ],
    // Deny particularly dangerous tools
    DenyTools = ["bash_kill"]
};
```

### With appsettings.json

```json
{
  "Kode": {
    "PermissionMode": "auto",
    "AllowTools": "*,fs_read,fs_write,fs_edit,fs_multi_edit,fs_glob,fs_grep,fs_list,bash_run,bash_logs",
    "RequireApprovalTools": "bash_run,fs_rm,fs_write,fs_edit,fs_multi_edit,bash_kill",
    "DenyTools": "bash_kill"
  }
}
```

## Tool Details

### fs_read

Read file contents with optional line range selection.

```json
{
  "tool": "fs_read",
  "arguments": {
    "path": "/path/to/file.txt",
    "startLine": 10,
    "endLine": 20
  }
}
```

### fs_write

Write content to a file. Creates parent directories as needed.

```json
{
  "tool": "fs_write",
  "arguments": {
    "path": "/path/to/file.txt",
    "content": "Hello, World!"
  }
}
```

### fs_edit

Replace exact text in a file. Requires sufficient context to uniquely identify the location.

```json
{
  "tool": "fs_edit",
  "arguments": {
    "path": "/path/to/file.txt",
    "oldString": "old text",
    "newString": "new text",
    "replaceAll": false
  }
}
```

### fs_glob

Find files matching glob patterns.

```json
{
  "tool": "fs_glob",
  "arguments": {
    "pattern": "**/*.cs",
    "maxResults": 100
  }
}
```

### fs_grep

Search for text patterns in files.

```json
{
  "tool": "fs_grep",
  "arguments": {
    "pattern": "TODO:",
    "filePattern": "*.cs",
    "isRegex": false,
    "caseSensitive": true,
    "maxResults": 50,
    "contextLines": 2
  }
}
```

### bash_run

Execute shell commands with optional background mode.

```json
{
  "tool": "bash_run",
  "arguments": {
    "command": "ls -la",
    "workingDirectory": "/workspace",
    "timeoutSeconds": 300,
    "background": false
  }
}
```

### todo_write

Manage todos for tracking task progress.

```json
{
  "tool": "todo_write",
  "arguments": {
    "todos": [
      { "content": "Implement feature X", "status": "pending" },
      { "content": "Write tests", "status": "in_progress" }
    ]
  }
}
```

### task_run

Delegate complex tasks to specialized sub-agents.

```json
{
  "tool": "task_run",
  "arguments": {
    "agentTemplateId": "code-analyst",
    "description": "Analyze authentication module",
    "prompt": "Review the auth code for security vulnerabilities",
    "context": "JWT-based Node.js application"
  }
}
```

## File Pool Tracking

The `ToolContextFilePool` tracks file operations performed during a session:

- **Read Tracking**: Records all files read by `fs_read`
- **Edit Tracking**: Records all files modified by `fs_write`, `fs_edit`, `fs_multi_edit`

This helps maintain awareness of which files have been accessed or modified.

## Extension Methods

### RegisterBuiltinTools

Registers all built-in tools with a `IToolRegistry`:

```csharp
public static IToolRegistry RegisterBuiltinTools(this IToolRegistry registry)
```

### AddBuiltinTools

Adds all built-in tools to the DI container:

```csharp
public static IServiceCollection AddBuiltinTools(this IServiceCollection services)
```

## Development

### Adding New Tools

1. Create tool class in the appropriate category folder
2. Inherit from `ToolBase<TArgs>`
3. Apply `[Tool("tool_name")]` attribute
4. Apply `[GenerateToolSchema]` to arguments class
5. Add to `ServiceCollectionExtensions.cs` registrations

Example:

```csharp
using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Tools;

namespace Kode.Agent.Tools.Builtin.FileSystem;

[Tool("fs_new_tool")]
public sealed class FsNewTool : ToolBase<FsNewToolArgs>
{
    public override string Name => "fs_new_tool";
    public override string Description => "...";
    public override object InputSchema => JsonSchemaBuilder.BuildSchema<FsNewToolArgs>();

    protected override async Task<ToolResult> ExecuteAsync(
        FsNewToolArgs args,
        ToolContext context,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}

[GenerateToolSchema]
public class FsNewToolArgs
{
    [ToolParameter(Description = "...")]
    public required string Parameter { get; init; }
}
```

## License

Part of the Kode Agent SDK project.
