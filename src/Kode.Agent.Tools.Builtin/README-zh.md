# Kode.Agent.Tools.Builtin

中文 | [English](./README.md)

Kode Agent SDK 的内置工具包。本包提供了 Agent 可用于与文件系统交互、执行 Shell 命令、管理待办事项等功能的综合工具集。

## 目录结构

工具按类别组织到子目录中：

```
src/Kode.Agent.Tools.Builtin/
├── FileSystem/     # 文件系统操作工具 (fs_*)
├── Shell/          # Shell 命令执行工具 (bash_*)
├── Todo/           # 任务列表管理工具 (todo_*)
├── Skills/         # Skills 系统工具 (skill_*)
├── Task/           # 子 Agent 委派工具 (task_*)
├── ToolContextFilePool.cs
└── ServiceCollectionExtensions.cs
```

## 工具分类

### 文件系统工具 (`fs_*`)

| 工具 | 描述 | 权限 |
|------|-------------|------------|
| `fs_read` | 读取文件内容，支持可选行范围 | 只读 |
| `fs_write` | 写入文件内容（创建/覆盖） | 需审批 |
| `fs_edit` | 替换文件中的精确文本 | 需审批 |
| `fs_multi_edit` | 对文件应用多次编辑 | 需审批 |
| `fs_glob` | 使用 glob 模式查找文件 | 只读 |
| `fs_grep` | 使用正则表达式搜索文件内容 | 只读 |
| `fs_list` | 列出目录内容 | 只读 |
| `fs_rm` | 删除文件或目录 | 需审批 |

**命名空间：** `Kode.Agent.Tools.Builtin.FileSystem`

### Shell 工具 (`bash_*`)

| 工具 | 描述 | 权限 |
|------|-------------|------------|
| `bash_run` | 执行 Shell 命令（支持后台模式） | 需审批 |
| `bash_kill` | 终止后台 Shell 进程 | 禁止（默认） |
| `bash_logs` | 获取后台进程的日志 | 只读 |

**命名空间：** `Kode.Agent.Tools.Builtin.Shell`

### Todo 工具 (`todo_*`)

| 工具 | 描述 | 权限 |
|------|-------------|------------|
| `todo_read` | 读取当前待办列表 | 只读 |
| `todo_write` | 创建/更新/删除待办事项 | 只读 |

**命名空间：** `Kode.Agent.Tools.Builtin.Todo`

### Skills 工具 (`skill_*`)

| 工具 | 描述 | 权限 |
|------|-------------|------------|
| `skill_list` | 列出可用的 Skills 及其状态 | 只读 |
| `skill_activate` | 激活指定的 Skill | 只读 |
| `skill_resource` | 读取 Skill 资源文件 | 只读 |

**命名空间：** `Kode.Agent.Tools.Builtin.Skills`

### Task 工具 (`task_*`)

| 工具 | 描述 | 权限 |
|------|-------------|------------|
| `task_run` | 将任务委派给专门的子 Agent | 自动 |

**命名空间：** `Kode.Agent.Tools.Builtin.Task`

## 使用方法

### 注册所有工具

```csharp
using Kode.Agent.Tools.Builtin;

// 使用工具注册表注册
var registry = new ToolRegistry();
registry.RegisterBuiltinTools();

// 或使用依赖注入
services.AddBuiltinTools();
```

### 注册特定类别

```csharp
// 仅文件系统工具
using Kode.Agent.Tools.Builtin.FileSystem;

registry.Register(new FsReadTool());
registry.Register(new FsWriteTool());
// ...

// 仅 Shell 工具
using Kode.Agent.Tools.Builtin.Shell;

registry.Register(new BashRunTool());
registry.Register(new BashKillTool());
// ...
```

### 权限配置

所有内置工具的推荐权限配置：

```csharp
var permissions = new PermissionConfig
{
    Mode = "auto",
    // 允许所有工具（包括 MCP 工具）
    AllowTools = ["*"],
    // 危险操作需要审批
    RequireApprovalTools = [
        "bash_run",
        "fs_rm",
        "fs_write",
        "fs_edit",
        "fs_multi_edit",
        "bash_kill"
    ],
    // 禁止特别危险的工具
    DenyTools = ["bash_kill"]
};
```

### 使用 appsettings.json

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

## 工具详情

### fs_read

读取文件内容，支持可选行范围选择。

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

将内容写入文件。需要时会创建父目录。

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

通过替换精确文本编辑文件。需要提供足够的上下文以唯一标识位置。

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

使用 glob 模式查找文件。

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

在文件中搜索文本模式。

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

执行 Shell 命令，支持可选的后台模式。

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

管理待办事项以跟踪任务进度。

```json
{
  "tool": "todo_write",
  "arguments": {
    "todos": [
      { "content": "实现功能 X", "status": "pending" },
      { "content": "编写测试", "status": "in_progress" }
    ]
  }
}
```

### task_run

将复杂任务委派给专门的子 Agent。

```json
{
  "tool": "task_run",
  "arguments": {
    "agentTemplateId": "code-analyst",
    "description": "分析认证模块",
    "prompt": "审查认证代码的安全漏洞",
    "context": "基于 JWT 的 Node.js 应用"
  }
}
```

## 文件池跟踪

`ToolContextFilePool` 跟踪会话期间执行的文件操作：

- **读取跟踪**：记录由 `fs_read` 读取的所有文件
- **编辑跟踪**：记录由 `fs_write`、`fs_edit`、`fs_multi_edit` 修改的所有文件

这有助于保持对已访问或修改文件的感知。

## 扩展方法

### RegisterBuiltinTools

向 `IToolRegistry` 注册所有内置工具：

```csharp
public static IToolRegistry RegisterBuiltinTools(this IToolRegistry registry)
```

### AddBuiltinTools

将所有内置工具添加到 DI 容器：

```csharp
public static IServiceCollection AddBuiltinTools(this IServiceCollection services)
```

## 开发指南

### 添加新工具

1. 在相应的类别文件夹中创建工具类
2. 继承自 `ToolBase<TArgs>`
3. 应用 `[Tool("tool_name")]` 属性
4. 对参数类应用 `[GenerateToolSchema]`
5. 添加到 `ServiceCollectionExtensions.cs` 注册

示例：

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
        // 实现代码
    }
}

[GenerateToolSchema]
public class FsNewToolArgs
{
    [ToolParameter(Description = "...")]
    public required string Parameter { get; init; }
}
```

## 注意事项

### Task 命名空间冲突

`Task` 命名空间与 `System.Threading.Tasks.Task` 类型冲突。在 `Skills` 文件夹中的工具需要使用完全限定名称 `System.Threading.Tasks.Task` 来避免歧义。

## 许可证

Kode Agent SDK 项目的一部分。
