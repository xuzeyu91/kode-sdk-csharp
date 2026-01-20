# Kode Agent SDK (C#) API Reference

> **中文版**: [API 文档 (中文)](./API_REFERENCE-zh.md)

This document outlines common APIs, key types, and event models. Please refer to the source code for the most accurate information.

## Core Types

### `AgentConfig`

- Location: `src/Kode.Agent.Sdk/Core/Types/AgentConfig.cs`
- Purpose: Runtime configuration for creating/resuming Agents

Key fields:

- `Model`: `string` (can be empty string; typically provided by Template merge)
- `SystemPrompt`: `string?`
- `TemplateId`: `string?` (enables template merge)
- `Tools`: `IReadOnlyList<string>?` (`"*"` means allow all registry tools)
- `Permissions`: `PermissionConfig?`
- `SandboxOptions`: `SandboxOptions?`
- `Context` / `Skills` / `SubAgents` / `Todo`: Runtime configuration fields
- `MaxToolConcurrency` / `ToolTimeout`: Tool concurrency and timeout settings

### `AgentConfigOverrides`

- Location: `src/Kode.Agent.Sdk/Core/Types/AgentConfig.cs`
- Purpose: Override specific fields when resuming from Store with "resume-from-meta"
- Semantics: `null` means keep the stored value.

### `AgentDependencies`

- Location: `src/Kode.Agent.Sdk/Core/Types/AgentDependencies.cs`
- Key dependencies:
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

- **From explicit config** (legacy method, still supported):

```csharp
await Agent.ResumeFromStoreAsync(agentId, config, deps, options, cancellationToken);
```

- **From meta.json** (recommended method):

```csharp
await Agent.ResumeFromStoreAsync(agentId, deps, options, overrides, cancellationToken);
```

This method reads `Store.loadInfo/loadMessages/loadToolCallRecords` and rebuilds `AgentConfig` using the `Metadata` field from `meta.json` (supports overrides).

## Running

### `IAgent`

- Location: `src/Kode.Agent.Sdk/Core/Abstractions/IAgent.cs`
- Common methods:
  - `RunAsync(input)`: Run until current round completes or pauses
  - `StepAsync()`: Execute one step (model + tool processing)
  - `PauseAsync()` / `ResumeAsync()`
  - `ApproveToolCallAsync()` / `DenyToolCallAsync()`
  - `SnapshotAsync(label?)`: Save safe fork point Snapshot
  - `ForkAsync(newAgentId, ..., snapshotId?)`: Fork new Agent from Snapshot

### Convenience methods (`Agent` concrete type)

- Location: `src/Kode.Agent.Sdk/Core/Agent/Agent.cs`
- `Send(text, options)`: Only enqueue (user/reminder), user triggers processing loop
- `Schedule()`: Get `Scheduler` (can register every-steps triggers; sends monitor `scheduler_triggered`)
- `On(eventType, handler)`: Subscribe to control/monitor by `event.type` (`permission_*` goes to control, others to monitor), returns `IDisposable` for unsubscribe
- `Subscribe(channels?, opts?)`: Style subscription (no history replay by default; provides `since/kinds`)
- `SubscribeProgress(opts?)`: Subscribe to progress only (no history replay by default; provides `since/kinds`)
- `Kick()`: Force entry into processing loop (doesn't trigger when paused)
- `InterruptAsync(note?)`: Best-effort interrupt current processing/tool execution, and supply synthetic `tool_result` for dangling `tool_use`
- `ChatStream(input, opts?)` / `ChatStreamAsync(input, opts?)`: Send and return progress stream until `done` is received
- `ChatAsync(input, opts?)` / `CompleteAsync(input, opts?)`: Return `CompleteResult { status, text, last, permissionIds }`
- `Stream(input, opts?)`: `chatStream` alias
- `SendAsync(text, options?)`: Return messageId
- `StatusAsync()`: Return `AgentStatus` (state/stepCount/lastBookmark/cursor/breakpoint)
- `InfoAsync()`: Return `AgentInfo` (templateId/createdAt/lineage/metadata etc.)

## Events

### `IEventBus` + Bookmarks

- Location: `src/Kode.Agent.Sdk/Core/Abstractions/IEventBus.cs`
- Key capabilities:
  - `GetLastBookmark()`
  - `GetCursor()`
  - `GetFailedEventCount()` / `FlushFailedEventsAsync()`: In-memory buffer and retry for critical event persistence failures
  - `SubscribeAsync(EventChannel channels, Bookmark? since = null, ...)`
  - `SubscribeAsync(..., kinds: IReadOnlyCollection<string>?)`: Filter by `event.type`
  - `SubscribeProgressAsync(Bookmark? since = null, ...)`
  - `SubscribeProgressAsync(..., kinds: IReadOnlyCollection<string>?)`: Filter by `event.type`
  - EventEnvelope shape is `{ cursor, bookmark, event }`, where `event.channel` is `progress|control|monitor`
  - Besides envelope.bookmark, `AgentEvent` body also contains `bookmark` field
  - When `since` is `null`, don't replay history, only push new events; for replay, explicitly pass `since`
  - When encountering unknown `event.type`, deserialize as `UnknownEvent` (won't discard events directly)

### Progress / Control / Monitor events

Events are separated into three channels: `progress` (streaming output), `control` (approval), `monitor` (observability).

The `progress` channel is strongly related to OpenAI SSE mapping:

- `TextChunkStartEvent` / `TextChunkEvent` / `TextChunkEndEvent`
- `ThinkChunkStartEvent` / `ThinkChunkEvent` / `ThinkChunkEndEvent`
- `ToolStartEvent` / `ToolEndEvent` / `ToolErrorEvent`
- `DoneEvent`: Round completion marker

`DoneEvent`:

- `Step`: Step number
- `Reason`: Completion reason (`completed|interrupted`)

Enum serialization:

- `AgentRuntimeState` / `BreakpointState` / `ToolCallState` use uppercase enum values in JSON (e.g., `READY`, `PRE_MODEL`, `APPROVAL_REQUIRED`), and support reading old numeric values.

Common `monitor` events (partial):

- `StepCompleteEvent`: `step_complete`
- `SchedulerTriggeredEvent`: `scheduler_triggered`
- `AgentRecoveredEvent`: `agent_recovered`
- `TokenUsageEvent`: `token_usage` (`TotalTokens = InputTokens + OutputTokens`)
- `ContextRepairEvent`: `context_repair` (fix orphan `tool_result`)
- `StorageFailureEvent`: `storage_failure` (degradation warning when critical event persistence fails; may only be visible through `OnMonitor` callback)

## MCP Integration Types

### `McpConfig`

- Location: `src/Kode.Agent.Mcp/McpConfig.cs`
- Purpose: MCP server connection configuration

```csharp
public sealed class McpConfig
{
    public required McpTransportType Transport { get; init; }
    public string? Command { get; init; }                    // Command for stdio transport
    public IReadOnlyList<string>? Args { get; init; }        // Arguments for stdio transport
    public IReadOnlyDictionary<string, string>? Environment { get; init; }  // stdio environment variables
    public string? Url { get; init; }                        // URL for HTTP/SSE transport
    public IReadOnlyDictionary<string, string>? Headers { get; init; }      // HTTP request headers
    public string? ServerName { get; init; }                 // Server name (for namespacing)
    public IReadOnlyList<string>? Include { get; init; }     // Tool whitelist
    public IReadOnlyList<string>? Exclude { get; init; }     // Tool blacklist
}
```

### `McpTransportType`

- Location: `src/Kode.Agent.Mcp/McpConfig.cs`
- Purpose: MCP transport type enumeration

```csharp
public enum McpTransportType
{
    Stdio,           // Standard I/O (subprocess)
    Http,            // HTTP transport (SSE-based)
    StreamableHttp,  // Streamable HTTP transport (bidirectional stream)
    Sse              // Server-Sent Events transport
}
```

### `McpClientManager`

- Location: `src/Kode.Agent.Mcp/McpClientManager.cs`
- Purpose: Manage client connections for multiple MCP servers

```csharp
public sealed class McpClientManager : IAsyncDisposable
{
    // Connect to MCP server
    public Task<McpClient> ConnectAsync(
        string serverName,
        McpConfig config,
        CancellationToken cancellationToken = default);

    // Disconnect specified server connection
    public Task DisconnectAsync(string serverName);

    // Disconnect all server connections
    public Task DisconnectAllAsync();

    // Get connected client
    public McpClient? GetClient(string serverName);

    // Get all connected server names
    public IReadOnlyList<string> ConnectedServers { get; }
}
```

### `McpToolProvider`

- Location: `src/Kode.Agent.Mcp/McpToolProvider.cs`
- Purpose: Get tools from MCP servers and convert to ITool objects

```csharp
public static class McpToolProvider
{
    // Get tools from MCP servers and convert to ITool objects
    public static Task<IReadOnlyList<ITool>> GetToolsAsync(
        McpClientManager manager,
        McpConfig config,
        ILogger? logger = null,
        CancellationToken cancellationToken = default);
}
```

**MCP tool naming convention**: Tool names use namespaced format `mcp__{serverName}__{toolName}`, for example:
- `mcp__chrome-devtools__take_screenshot`
- `mcp__filesystem__read_file`
- `mcp__github__create_issue`

### `McpServersLoader`

- Location: `examples/Kode.Agent.WebApiAssistant/Services/McpServersLoader.cs`
- Purpose: Load MCP servers from appsettings.json configuration

```csharp
public sealed class McpServersLoader : IAsyncDisposable
{
    // Load MCP servers from configuration and register tools
    public async Task<IReadOnlyList<string>> LoadAndRegisterServersAsync(
        IConfiguration configuration,
        IToolRegistry toolRegistry,
        CancellationToken cancellationToken = default);

    // Disconnect all MCP server connections
    public Task DisconnectAllAsync();

    // Get list of loaded servers
    public IReadOnlyList<string> LoadedServers { get; }
}
```

**appsettings.json configuration format:**
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

- Location: `src/Kode.Agent.Sdk/Core/Abstractions/IAgentStore.cs`
- Storage capabilities include: runtime (messages/tool-calls/todos), meta/events/snapshots/history windows/compression/recovered files, etc.
- `tool-calls.json`: `ToolCallRecord` uses standard structure (`id/name/input/state/approval/auditTrail/createdAt/updatedAt...`), and supports automatic migration reading from old structure (`callId/toolName/arguments/state(int)`).
- `snapshots/`: `SaveSnapshotAsync/LoadSnapshotAsync/ListSnapshotsAsync` uses `Snapshot` structure (`id/messages/lastSfpIndex/lastBookmark/createdAt/metadata`).
