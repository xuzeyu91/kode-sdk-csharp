namespace Kode.Agent.Sdk.Core.Abstractions;

/// <summary>
/// Task delegation request (aligned with TS Agent.delegateTask usage by task_run).
/// </summary>
public record DelegateTaskRequest
{
    public required string TemplateId { get; init; }
    public required string Prompt { get; init; }
    public IReadOnlyList<string>? Tools { get; init; }
    public string? Model { get; init; }
    public string? CallId { get; init; }
    public bool? StreamEvents { get; init; }
}

/// <summary>
/// Task delegation result (aligned with TS CompleteResult subset returned by task_run).
/// </summary>
public record DelegateTaskResult
{
    public required string Status { get; init; } // "ok" | "paused"
    public string? Text { get; init; }
    public IReadOnlyList<string>? PermissionIds { get; init; }
    public string? AgentId { get; init; }
}

/// <summary>
/// Optional capability interface for agents that can delegate tasks to sub-agents.
/// </summary>
public interface ITaskDelegatorAgent
{
    Task<DelegateTaskResult> DelegateTaskAsync(DelegateTaskRequest request, CancellationToken cancellationToken = default);
}

