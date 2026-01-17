namespace Kode.Agent.Sdk.Core.Hooks;

/// <summary>
/// Manages hook registration and execution.
/// </summary>
public class HookManager
{
    private readonly List<(IHooks Hooks, HookOrigin Origin)> _hooks = [];

    /// <summary>
    /// Register hooks with a specified origin.
    /// </summary>
    public void Register(IHooks hooks, HookOrigin origin = HookOrigin.Agent)
    {
        _hooks.Add((hooks, origin));
    }

    /// <summary>
    /// Unregister hooks.
    /// </summary>
    public void Unregister(IHooks hooks)
    {
        _hooks.RemoveAll(h => h.Hooks == hooks);
    }

    /// <summary>
    /// Get all registered hooks information.
    /// </summary>
    public IReadOnlyList<RegisteredHook> GetRegistered()
    {
        return _hooks.Select(h =>
        {
            var names = new List<string>();
            if (h.Hooks.PreToolUse != null) names.Add("preToolUse");
            if (h.Hooks.PostToolUse != null) names.Add("postToolUse");
            if (h.Hooks.PreModel != null) names.Add("preModel");
            if (h.Hooks.PostModel != null) names.Add("postModel");
            if (h.Hooks.MessagesChanged != null) names.Add("messagesChanged");
            
            return new RegisteredHook(h.Origin, names);
        }).ToList();
    }

    /// <summary>
    /// Run pre-tool-use hooks.
    /// </summary>
    public async Task<HookDecision?> RunPreToolUseAsync(
        ToolCall call, 
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        foreach (var (hooks, _) in _hooks)
        {
            if (hooks.PreToolUse != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await hooks.PreToolUse(call, context);
                if (result != null)
                {
                    return result;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Run post-tool-use hooks.
    /// </summary>
    public async Task<ToolOutcome> RunPostToolUseAsync(
        ToolOutcome outcome,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        var current = outcome;

        foreach (var (hooks, _) in _hooks)
        {
            if (hooks.PostToolUse != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await hooks.PostToolUse(current, context);
                
                if (result != null)
                {
                    current = result switch
                    {
                        ReplaceResult replace => replace.Outcome,
                        UpdateResult update => current with
                        {
                            Result = update.Result ?? current.Result,
                            IsError = update.IsError ?? current.IsError
                        },
                        _ => current
                    };
                }
            }
        }

        return current;
    }

    /// <summary>
    /// Run pre-model hooks.
    /// </summary>
    public async Task RunPreModelAsync(
        Abstractions.ModelRequest request,
        CancellationToken cancellationToken = default)
    {
        foreach (var (hooks, _) in _hooks)
        {
            if (hooks.PreModel != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await hooks.PreModel(request);
            }
        }
    }

    /// <summary>
    /// Run post-model hooks.
    /// </summary>
    public async Task RunPostModelAsync(
        Abstractions.ModelResponse response,
        CancellationToken cancellationToken = default)
    {
        foreach (var (hooks, _) in _hooks)
        {
            if (hooks.PostModel != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await hooks.PostModel(response);
            }
        }
    }

    /// <summary>
    /// Run messages-changed hooks.
    /// </summary>
    public async Task RunMessagesChangedAsync(
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default)
    {
        foreach (var (hooks, _) in _hooks)
        {
            if (hooks.MessagesChanged != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await hooks.MessagesChanged(messages);
            }
        }
    }

    /// <summary>
    /// Clear all registered hooks.
    /// </summary>
    public void Clear()
    {
        _hooks.Clear();
    }
}
