namespace Kode.Agent.Sdk.Core.Hooks;

/// <summary>
/// Hooks interface for intercepting agent lifecycle events.
/// </summary>
public interface IHooks
{
    /// <summary>
    /// Called before a tool is executed.
    /// </summary>
    Func<ToolCall, ToolContext, Task<HookDecision?>>? PreToolUse { get; }
    
    /// <summary>
    /// Called after a tool is executed.
    /// </summary>
    Func<ToolOutcome, ToolContext, Task<PostHookResult?>>? PostToolUse { get; }
    
    /// <summary>
    /// Called before sending a request to the model.
    /// </summary>
    Func<Abstractions.ModelRequest, Task>? PreModel { get; }
    
    /// <summary>
    /// Called after receiving a response from the model.
    /// </summary>
    Func<Abstractions.ModelResponse, Task>? PostModel { get; }
    
    /// <summary>
    /// Called when the message history changes.
    /// </summary>
    Func<IReadOnlyList<Message>, Task>? MessagesChanged { get; }
}

/// <summary>
/// Default hooks implementation with optional delegates.
/// </summary>
public class Hooks : IHooks
{
    public Func<ToolCall, ToolContext, Task<HookDecision?>>? PreToolUse { get; set; }
    public Func<ToolOutcome, ToolContext, Task<PostHookResult?>>? PostToolUse { get; set; }
    public Func<Abstractions.ModelRequest, Task>? PreModel { get; set; }
    public Func<Abstractions.ModelResponse, Task>? PostModel { get; set; }
    public Func<IReadOnlyList<Message>, Task>? MessagesChanged { get; set; }
}
