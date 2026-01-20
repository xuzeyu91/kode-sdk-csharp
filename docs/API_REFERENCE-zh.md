# Kode Agent SDK (C#) API Reference

> **English version**: [API Reference (English)](./API_REFERENCE.md)

本文件概述常用 API、关键类型与事件模型；如有出入，以源码为准。

## Core Types

### `AgentConfig`

- 位置：`src/Kode.Agent.Sdk/Core/Types/AgentConfig.cs`
- 用途：创建/恢复 Agent 的运行时配置

关键字段：

- `Model`：`string`（可为空字符串；通常由 Template 合并提供）
- `SystemPrompt`：`string?`
- `TemplateId`：`string?`（启用 template merge）
- `Tools`：`IReadOnlyList<string>?`（`"*"` 表示允许全部 registry 工具）
- `Permissions`：`PermissionConfig?`
- `SandboxOptions`：`SandboxOptions?`
- `Context` / `Skills` / `SubAgents` / `Todo`：runtime 配置字段
- `MaxToolConcurrency` / `ToolTimeout`：工具并发与超时设置

### `AgentConfigOverrides`

- 位置：`src/Kode.Agent.Sdk/Core/Types/AgentConfig.cs`
- 用途：配合 "resume-from-meta" 从 Store 恢复时覆盖部分字段
- 语义：`null` 表示保留存储的值。

### `AgentDependencies`

- 位置：`src/Kode.Agent.Sdk/Core/Types/AgentDependencies.cs`
- 关键依赖：
  - `IAgentStore Store`
  - `ISandboxFactory SandboxFactory`
  - `IToolRegistry ToolRegistry`
  - `IModelProvider ModelProvider`
  - `AgentTemplateRegistry? TemplateRegistry`

## Agent Lifecycle

### Create

```csharp
await Agent.CreateAsync(agentId, config, deps, cancellationToken);
```

### Resume (two flavors)

- **From explicit config**（旧方式，仍保留）：

```csharp
await Agent.ResumeFromStoreAsync(agentId, config, deps, options, cancellationToken);
```

- **From meta.json**（推荐方式）：

```csharp
await Agent.ResumeFromStoreAsync(agentId, deps, options, overrides, cancellationToken);
```

该方式会读取 `Store.loadInfo/loadMessages/loadToolCallRecords`，并用 `meta.json` 的 `Metadata` 字段重建 `AgentConfig`（支持 overrides）。

## Running

### `IAgent`

- 位置：`src/Kode.Agent.Sdk/Core/Abstractions/IAgent.cs`
- 常用方法：
  - `RunAsync(input)`：阻塞运行直到本轮完成或暂停
  - `StepAsync()`：执行一个 step（model + tool 处理）
  - `PauseAsync()` / `ResumeAsync()`
  - `ApproveToolCallAsync()` / `DenyToolCallAsync()`
  - `SnapshotAsync(label?)`：保存安全分叉点 Snapshot
  - `ForkAsync(newAgentId, ..., snapshotId?)`：从 Snapshot 分叉新 Agent

### TS-aligned convenience methods (`Agent` concrete type)

- 位置：`src/Kode.Agent.Sdk/Core/Agent/Agent.cs`
- `Send(text, options)`：仅入队（user/reminder），user 会触发 processing loop
- `Schedule()`：获取 `Scheduler`（可注册 every-steps 触发器；会发 monitor `scheduler_triggered`）
- `On(eventType, handler)`：按 `event.type` 订阅 control/monitor（`permission_*` 走 control，其余走 monitor），返回 `IDisposable` 用于取消订阅
- `Subscribe(channels?, opts?)`：风格订阅（默认不回放历史；提供 `since/kinds`）
- `SubscribeProgress(opts?)`：仅订阅 progress（默认不回放历史；提供 `since/kinds`）
- `Kick()`：强制进入 processing loop（暂停时不触发）
- `InterruptAsync(note?)`：best-effort 中断当前 processing/tool 执行，并补齐 dangling `tool_use` 的 synthetic `tool_result`
- `ChatStream(input, opts?)` / `ChatStreamAsync(input, opts?)`：发送并返回 progress 流，直到收到 `done`
- `ChatAsync(input, opts?)` / `CompleteAsync(input, opts?)`：返回 `CompleteResult { status, text, last, permissionIds }`
- `Stream(input, opts?)`：`chatStream` 别名
- `SendAsync(text, options?)`：返回 messageId
- `StatusAsync()`：返回 `AgentStatus`（state/stepCount/lastBookmark/cursor/breakpoint）
- `InfoAsync()`：返回 `AgentInfo`（templateId/createdAt/lineage/metadata 等）

## Events

### `IEventBus` + Bookmarks

- 位置：`src/Kode.Agent.Sdk/Core/Abstractions/IEventBus.cs`
- 关键能力：
  - `GetLastBookmark()`
  - `GetCursor()`
  - `GetFailedEventCount()` / `FlushFailedEventsAsync()`：关键事件持久化失败后的内存缓冲与重试
  - `SubscribeAsync(EventChannel channels, Bookmark? since = null, ...)`
  - `SubscribeAsync(..., kinds: IReadOnlyCollection<string>?)`：按 `event.type` 过滤
  - `SubscribeProgressAsync(Bookmark? since = null, ...)`
  - `SubscribeProgressAsync(..., kinds: IReadOnlyCollection<string>?)`：按 `event.type` 过滤
  - EventEnvelope 形状为 `{ cursor, bookmark, event }`，其中 `event.channel` 为 `progress|control|monitor`
  - 除 envelope.bookmark 外，`AgentEvent` 本体也会包含 `bookmark` 字段
  - 当 `since` 为 `null` 时，不回放历史，只推送新事件；需要回放请显式传入 `since`
  - 当遇到未知 `event.type` 时，会反序列化为 `UnknownEvent`（不会直接丢弃事件）

### Progress / Control / Monitor events

事件分三通道：`progress`（流式输出）、`control`（审批）、`monitor`（可观测性）。

其中与 OpenAI SSE 映射强相关的是 `progress`：

- `TextChunkStartEvent` / `TextChunkEvent` / `TextChunkEndEvent`
- `ThinkChunkStartEvent` / `ThinkChunkEvent` / `ThinkChunkEndEvent`
- `ToolStartEvent` / `ToolEndEvent` / `ToolErrorEvent`
- `DoneEvent`：本轮结束标记

`DoneEvent`：

- `Step`：步骤序号
- `Reason`：完成原因（`completed|interrupted`）

枚举序列化：

- `AgentRuntimeState` / `BreakpointState` / `ToolCallState` 在 JSON 中使用大写枚举值（例如 `READY`、`PRE_MODEL`、`APPROVAL_REQUIRED`），并兼容读取旧的数字值。

常用 `monitor` 事件（部分）：

- `StepCompleteEvent`：`step_complete`
- `SchedulerTriggeredEvent`：`scheduler_triggered`
- `AgentRecoveredEvent`：`agent_recovered`
- `TokenUsageEvent`：`token_usage`（`TotalTokens = InputTokens + OutputTokens`）
- `ContextRepairEvent`：`context_repair`（修复 orphan `tool_result`）
- `StorageFailureEvent`：`storage_failure`（关键事件持久化失败时的降级告警；可能仅通过 `OnMonitor` 回调可见）

## MCP Integration Types

### `McpConfig`

- 位置：`src/Kode.Agent.Mcp/McpConfig.cs`
- 用途：MCP 服务器连接配置

```csharp
public sealed class McpConfig
{
    public required McpTransportType Transport { get; init; }
    public string? Command { get; init; }                    // stdio 传输的命令
    public IReadOnlyList<string>? Args { get; init; }        // stdio 传输的参数
    public IReadOnlyDictionary<string, string>? Environment { get; init; }  // stdio 环境变量
    public string? Url { get; init; }                        // HTTP/SSE 传输的 URL
    public IReadOnlyDictionary<string, string>? Headers { get; init; }      // HTTP 请求头
    public string? ServerName { get; init; }                 // 服务器名称（用于命名空间）
    public IReadOnlyList<string>? Include { get; init; }     // 工具白名单
    public IReadOnlyList<string>? Exclude { get; init; }     // 工具黑名单
}
```

### `McpTransportType`

- 位置：`src/Kode.Agent.Mcp/McpConfig.cs`
- 用途：MCP 传输类型枚举

```csharp
public enum McpTransportType
{
    Stdio,           // 标准 I/O（子进程）
    Http,            // HTTP 传输（基于 SSE）
    StreamableHttp,  // 可流式 HTTP 传输（双向流）
    Sse              // Server-Sent Events 传输
}
```

### `McpClientManager`

- 位置：`src/Kode.Agent.Mcp/McpClientManager.cs`
- 用途：管理多个 MCP 服务器的客户端连接

```csharp
public sealed class McpClientManager : IAsyncDisposable
{
    // 连接到 MCP 服务器
    public Task<McpClient> ConnectAsync(
        string serverName,
        McpConfig config,
        CancellationToken cancellationToken = default);

    // 断开指定服务器连接
    public Task DisconnectAsync(string serverName);

    // 断开所有服务器连接
    public Task DisconnectAllAsync();

    // 获取已连接的客户端
    public McpClient? GetClient(string serverName);

    // 获取所有已连接的服务器名称
    public IReadOnlyList<string> ConnectedServers { get; }
}
```

### `McpToolProvider`

- 位置：`src/Kode.Agent.Mcp/McpToolProvider.cs`
- 用途：从 MCP 服务器获取工具并转换为 ITool 对象

```csharp
public static class McpToolProvider
{
    // 从 MCP 服务器获取工具并转换为 ITool 对象
    public static Task<IReadOnlyList<ITool>> GetToolsAsync(
        McpClientManager manager,
        McpConfig config,
        ILogger? logger = null,
        CancellationToken cancellationToken = default);
}
```

**MCP 工具命名规则**：工具名称使用命名空间格式 `mcp__{serverName}__{toolName}`，例如：
- `mcp__chrome-devtools__take_screenshot`
- `mcp__filesystem__read_file`
- `mcp__github__create_issue`

### `McpServersLoader`

- 位置：`examples/Kode.Agent.WebApiAssistant/Services/McpServersLoader.cs`
- 用途：从 appsettings.json 配置加载 MCP 服务器

```csharp
public sealed class McpServersLoader : IAsyncDisposable
{
    // 从配置加载 MCP 服务器并注册工具
    public async Task<IReadOnlyList<string>> LoadAndRegisterServersAsync(
        IConfiguration configuration,
        IToolRegistry toolRegistry,
        CancellationToken cancellationToken = default);

    // 断开所有 MCP 服务器连接
    public Task DisconnectAllAsync();

    // 获取已加载的服务器列表
    public IReadOnlyList<string> LoadedServers { get; }
}
```

**appsettings.json 配置格式：**
```json
{
  "McpServers": {
    "server-name": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem", "/path"],
      "env": { "NODE_ENV": "production" },
      "transport": "stdio",
      "url": "https://api.example.com/mcp",
      "headers": { "Authorization": "Bearer token" }
    }
  }
}
```

## Store

- 位置：`src/Kode.Agent.Sdk/Core/Abstractions/IAgentStore.cs`
- 存储能力包含：runtime（messages/tool-calls/todos）、meta/events/snapshots/history windows/compression/recovered files 等。
- `tool-calls.json`：`ToolCallRecord` 使用标准结构（`id/name/input/state/approval/auditTrail/createdAt/updatedAt...`），并支持从旧结构（`callId/toolName/arguments/state(int)`）自动迁移读取。
- `snapshots/`：`SaveSnapshotAsync/LoadSnapshotAsync/ListSnapshotsAsync` 使用 `Snapshot` 结构（`id/messages/lastSfpIndex/lastBookmark/createdAt/metadata`）。
