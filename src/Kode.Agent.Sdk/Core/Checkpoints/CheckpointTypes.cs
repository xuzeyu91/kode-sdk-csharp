using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kode.Agent.Sdk.Core.Checkpoints;

/// <summary>
/// Agent state stored inside a checkpoint (aligned with TS checkpointer.AgentState).
/// </summary>
public record CheckpointAgentState
{
    /// <summary>
    /// TS-aligned: "ready" | "working" | "paused" | "completed" | "failed".
    /// </summary>
    public required string Status { get; init; }

    public required int StepCount { get; init; }
    public required int LastSfpIndex { get; init; }
    public Bookmark? LastBookmark { get; init; }
}

/// <summary>
/// Checkpoint data structure (aligned with TS Checkpoint).
/// </summary>
public record Checkpoint
{
    public required string Id { get; init; }
    public required string AgentId { get; init; }
    public string? SessionId { get; init; }
    public required long Timestamp { get; init; }
    public required string Version { get; init; }

    public required CheckpointAgentState State { get; init; }
    public required IReadOnlyList<Message> Messages { get; init; }
    public required IReadOnlyList<ToolCallRecord> ToolRecords { get; init; }
    public required IReadOnlyList<ToolDescriptor> Tools { get; init; }

    public required CheckpointConfig Config { get; init; }
    public required CheckpointMetadata Metadata { get; init; }
}

/// <summary>
/// Checkpoint configuration snapshot (aligned with TS Checkpoint.config).
/// </summary>
public record CheckpointConfig
{
    public required string Model { get; init; }
    public string? SystemPrompt { get; init; }
    public string? TemplateId { get; init; }
}

/// <summary>
/// Checkpoint metadata (aligned with TS Checkpoint.metadata; supports arbitrary extra keys).
/// </summary>
public record CheckpointMetadata
{
    public bool? IsForkPoint { get; init; }
    public string? ParentCheckpointId { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extra { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Checkpoint metadata for listing (aligned with TS CheckpointMetadata).
/// </summary>
public record CheckpointListItem
{
    public required string Id { get; init; }
    public required string AgentId { get; init; }
    public string? SessionId { get; init; }
    public required long Timestamp { get; init; }
    public bool? IsForkPoint { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
}

public record CheckpointListOptions
{
    public string? SessionId { get; init; }
    public int? Limit { get; init; }
    public int? Offset { get; init; }
}

