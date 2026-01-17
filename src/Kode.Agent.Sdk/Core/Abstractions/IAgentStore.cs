using System.Text.Json;
using System.Text.Json.Serialization;
using Kode.Agent.Sdk.Core.Context;
using Kode.Agent.Sdk.Core.Skills;
using Kode.Agent.Sdk.Core.Todo;

namespace Kode.Agent.Sdk.Core.Abstractions;

/// <summary>
/// Storage interface for agent state persistence.
/// </summary>
public interface IAgentStore
{
    #region Runtime State

    /// <summary>
    /// Saves the message history for an agent.
    /// </summary>
    Task SaveMessagesAsync(string agentId, IReadOnlyList<Message> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the message history for an agent.
    /// </summary>
    Task<IReadOnlyList<Message>> LoadMessagesAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves tool call records for an agent.
    /// </summary>
    Task SaveToolCallRecordsAsync(string agentId, IReadOnlyList<ToolCallRecord> records, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads tool call records for an agent.
    /// </summary>
    Task<IReadOnlyList<ToolCallRecord>> LoadToolCallRecordsAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves todo items for an agent.
    /// </summary>
    Task SaveTodosAsync(string agentId, TodoSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads todo items for an agent.
    /// </summary>
    Task<TodoSnapshot?> LoadTodosAsync(string agentId, CancellationToken cancellationToken = default);

    #endregion

    #region Events

    /// <summary>
    /// Appends an event to the timeline.
    /// </summary>
    Task AppendEventAsync(string agentId, Timeline timeline, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads events from the timeline.
    /// </summary>
    IAsyncEnumerable<Timeline> ReadEventsAsync(
        string agentId,
        EventChannel? channel = null,
        Bookmark? since = null,
        CancellationToken cancellationToken = default);

    #endregion

    #region History / Compression

    /// <summary>
    /// Saves a history window (pre-compression snapshot).
    /// </summary>
    Task SaveHistoryWindowAsync(string agentId, HistoryWindow window, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads all history windows.
    /// </summary>
    Task<IReadOnlyList<HistoryWindow>> LoadHistoryWindowsAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a compression record.
    /// </summary>
    Task SaveCompressionRecordAsync(string agentId, CompressionRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads all compression records.
    /// </summary>
    Task<IReadOnlyList<CompressionRecord>> LoadCompressionRecordsAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a recovered file snapshot.
    /// </summary>
    Task SaveRecoveredFileAsync(string agentId, RecoveredFile file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads all recovered file snapshots.
    /// </summary>
    Task<IReadOnlyList<RecoveredFile>> LoadRecoveredFilesAsync(string agentId, CancellationToken cancellationToken = default);

    #endregion

    #region Snapshots

    /// <summary>
    /// Saves a snapshot (TS-aligned Store.saveSnapshot()).
    /// </summary>
    Task SaveSnapshotAsync(string agentId, Snapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a snapshot (TS-aligned Store.loadSnapshot()).
    /// </summary>
    Task<Snapshot?> LoadSnapshotAsync(string agentId, string snapshotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all snapshots for an agent (TS-aligned Store.listSnapshots()).
    /// </summary>
    Task<IReadOnlyList<Snapshot>> ListSnapshotsAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a snapshot.
    /// </summary>
    Task DeleteSnapshotAsync(string agentId, string snapshotId, CancellationToken cancellationToken = default);

    #endregion

    #region Meta

    /// <summary>
    /// Saves agent info (meta.json equivalent).
    /// </summary>
    Task SaveInfoAsync(string agentId, AgentInfo info, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads agent info (meta.json equivalent).
    /// </summary>
    Task<AgentInfo?> LoadInfoAsync(string agentId, CancellationToken cancellationToken = default);

    #endregion

    #region Skills State

    /// <summary>
    /// Saves skills state for an agent.
    /// </summary>
    Task SaveSkillsStateAsync(string agentId, SkillsState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads skills state for an agent.
    /// </summary>
    Task<SkillsState?> LoadSkillsStateAsync(string agentId, CancellationToken cancellationToken = default);

    #endregion

    #region Agent Lifecycle

    /// <summary>
    /// Checks if an agent exists in storage.
    /// </summary>
    Task<bool> ExistsAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all agent IDs in storage.
    /// </summary>
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all data for an agent.
    /// </summary>
    Task DeleteAsync(string agentId, CancellationToken cancellationToken = default);

    #endregion
}

/// <summary>
/// Agent meta info (aligned with TS Store meta.json / AgentInfo).
/// </summary>
public record AgentInfo
{
    public required string AgentId { get; init; }
    public string? TemplateId { get; init; }
    public string? CreatedAt { get; init; }
    public IReadOnlyList<string>? Lineage { get; init; }
    public string? ConfigVersion { get; init; }
    public int MessageCount { get; init; }
    public int LastSfpIndex { get; init; }
    public Bookmark? LastBookmark { get; init; }
    public BreakpointState? Breakpoint { get; init; }
    public IReadOnlyDictionary<string, JsonElement>? Metadata { get; init; }
}

/// <summary>
/// Timeline entry for event persistence.
/// </summary>
public record Timeline
{
    public required long Cursor { get; init; }
    public required Bookmark Bookmark { get; init; }
    public required AgentEvent Event { get; init; }
}

/// <summary>
/// Snapshot used for safe-fork points (aligned with TS Snapshot).
/// </summary>
public record Snapshot
{
    public required string Id { get; init; }
    public required IReadOnlyList<Message> Messages { get; init; }
    public required int LastSfpIndex { get; init; }
    public required Bookmark LastBookmark { get; init; }
    public required string CreatedAt { get; init; }
    public IReadOnlyDictionary<string, JsonElement>? Metadata { get; init; }
}

/// <summary>
/// Tool descriptor for serialization.
/// </summary>
public record ToolDescriptor
{
    public required ToolSource Source { get; init; }
    public required string Name { get; init; }
    public string? RegistryId { get; init; }
    public Dictionary<string, object>? Config { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Source of a tool.
/// </summary>
public enum ToolSource
{
    Builtin,
    Registered,
    Mcp
}

/// <summary>
/// Tool call record for persistence.
/// </summary>
public record ToolCallRecord
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required object Input { get; init; }

    [JsonConverter(typeof(ToolCallStateJsonConverter))]
    public required ToolCallState State { get; init; }

    public ToolCallApproval Approval { get; init; } = new() { Required = false };
    public object? Result { get; init; }
    public string? Error { get; init; }
    public bool IsError { get; init; }
    public long? StartedAt { get; init; }
    public long? CompletedAt { get; init; }
    public long? DurationMs { get; init; }
    public long CreatedAt { get; init; }
    public long UpdatedAt { get; init; }
    public List<ToolCallAuditEntry> AuditTrail { get; init; } = [];
}

/// <summary>
/// Tool call approval state (aligned with TS ToolCallApproval).
/// </summary>
public record ToolCallApproval
{
    public required bool Required { get; init; }
    public string? Decision { get; init; } // allow|deny
    public string? DecidedBy { get; init; }
    public long? DecidedAt { get; init; }
    public string? Note { get; init; }
    public JsonElement? Meta { get; init; }
}

/// <summary>
/// Tool call audit entry (aligned with TS ToolCallAuditEntry).
/// </summary>
public record ToolCallAuditEntry
{
    [JsonConverter(typeof(ToolCallStateJsonConverter))]
    public required ToolCallState State { get; init; }
    public required long Timestamp { get; init; }
    public string? Note { get; init; }
}

/// <summary>
/// Tool call snapshot for events (aligned with TS ToolCallSnapshot).
/// </summary>
public record ToolCallSnapshot
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    [JsonConverter(typeof(ToolCallStateJsonConverter))]
    public required ToolCallState State { get; init; }

    public required ToolCallApproval Approval { get; init; }
    public object? Result { get; init; }
    public string? Error { get; init; }
    public bool? IsError { get; init; }
    public long? DurationMs { get; init; }
    public long? StartedAt { get; init; }
    public long? CompletedAt { get; init; }
    public object? InputPreview { get; init; }
    public IReadOnlyList<ToolCallAuditEntry>? AuditTrail { get; init; }
}

// Note: TodoSnapshot, TodoItem, TodoStatus are defined in Kode.Agent.Sdk.Core.Todo namespace

// Note: SkillsState is defined in Kode.Agent.Sdk.Core.Skills (aligned with TS skills/types.ts).
