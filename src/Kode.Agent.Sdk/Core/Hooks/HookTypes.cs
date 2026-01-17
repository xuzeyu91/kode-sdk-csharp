using System.Text.Json;

namespace Kode.Agent.Sdk.Core.Hooks;

/// <summary>
/// Tool call information for hooks.
/// </summary>
public record ToolCall(
    string Id,
    string Name,
    JsonElement Input
);

/// <summary>
/// Tool execution outcome.
/// </summary>
public record ToolOutcome(
    string Id,
    string Name,
    JsonElement Input,
    ToolResult Result,
    bool IsError,
    TimeSpan Duration
);

/// <summary>
/// Hook decision for pre-tool execution.
/// </summary>
public abstract record HookDecision
{
    /// <summary>
    /// Allow the tool to execute normally.
    /// </summary>
    public static HookDecision Allow() => new AllowDecision();
    
    /// <summary>
    /// Deny the tool execution with a reason.
    /// </summary>
    public static HookDecision Deny(string reason) => new DenyDecision(reason);
    
    /// <summary>
    /// Skip the tool and provide a mock result.
    /// </summary>
    public static HookDecision Skip(string mockResult) => new SkipDecision(mockResult);
    
    /// <summary>
    /// Require approval before executing.
    /// </summary>
    public static HookDecision RequireApproval(string? reason = null) => new RequireApprovalDecision(reason);
}

public sealed record AllowDecision : HookDecision;
public sealed record DenyDecision(string Reason) : HookDecision;
public sealed record SkipDecision(string MockResult) : HookDecision;
public sealed record RequireApprovalDecision(string? Reason) : HookDecision;

/// <summary>
/// Post-hook result for modifying tool outcomes.
/// </summary>
public abstract record PostHookResult
{
    /// <summary>
    /// Keep the outcome unchanged.
    /// </summary>
    public static PostHookResult Pass() => new PassResult();
    
    /// <summary>
    /// Replace the entire outcome.
    /// </summary>
    public static PostHookResult Replace(ToolOutcome outcome) => new ReplaceResult(outcome);
    
    /// <summary>
    /// Update specific fields of the outcome.
    /// </summary>
    public static PostHookResult Update(ToolResult? result = null, bool? isError = null) 
        => new UpdateResult(result, isError);
}

public sealed record PassResult : PostHookResult;
public sealed record ReplaceResult(ToolOutcome Outcome) : PostHookResult;
public sealed record UpdateResult(ToolResult? Result, bool? IsError) : PostHookResult;

/// <summary>
/// Hook origin type.
/// </summary>
public enum HookOrigin
{
    Agent,
    ToolTune,
    Plugin
}

/// <summary>
/// Registered hook information.
/// </summary>
public record RegisteredHook(
    HookOrigin Origin,
    IReadOnlyList<string> Names
);
