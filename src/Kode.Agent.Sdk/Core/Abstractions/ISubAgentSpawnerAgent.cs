namespace Kode.Agent.Sdk.Core.Abstractions;

/// <summary>
/// Runtime recursion state for spawning sub-agents (aligned with TS SubAgentRuntime).
/// </summary>
public record SubAgentRuntime
{
    public int DepthRemaining { get; init; }
}

/// <summary>
/// Optional capability interface for agents that can spawn sub-agents based on SubAgents config.
/// </summary>
public interface ISubAgentSpawnerAgent
{
    Task<DelegateTaskResult> SpawnSubAgentAsync(
        string templateId,
        string prompt,
        SubAgentRuntime? runtime = null,
        CancellationToken cancellationToken = default);
}

