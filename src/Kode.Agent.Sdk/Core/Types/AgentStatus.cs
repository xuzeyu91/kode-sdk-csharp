namespace Kode.Agent.Sdk.Core.Types;

/// <summary>
/// Lightweight runtime status snapshot (aligned with TS Agent.status()).
/// </summary>
public record AgentStatus
{
    public required string AgentId { get; init; }
    public required AgentRuntimeState State { get; init; }
    public required int StepCount { get; init; }
    public required int LastSfpIndex { get; init; }
    public Bookmark? LastBookmark { get; init; }
    public long Cursor { get; init; }
    public required BreakpointState Breakpoint { get; init; }
}

