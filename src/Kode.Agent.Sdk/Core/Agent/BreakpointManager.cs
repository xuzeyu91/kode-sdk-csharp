namespace Kode.Agent.Sdk.Core.Agent;

/// <summary>
/// Manages breakpoint state for crash recovery.
/// </summary>
public sealed class BreakpointManager
{
    private readonly IEventBus _eventBus;
    private BreakpointState _state = BreakpointState.Ready;
    private readonly object _lock = new();

    public BreakpointManager(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// Gets the current breakpoint state.
    /// </summary>
    public BreakpointState State
    {
        get
        {
            lock (_lock) return _state;
        }
    }

    /// <summary>
    /// Transitions to a new breakpoint state.
    /// </summary>
    public void TransitionTo(BreakpointState newState)
    {
        BreakpointState previous;
        lock (_lock)
        {
            if (_state == newState) return;
            previous = _state;
            _state = newState;
        }

        _eventBus.EmitMonitor(new BreakpointChangedEvent
        {
            Type = "breakpoint_changed",
            Previous = previous,
            Current = newState,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }

    /// <summary>
    /// Resets to ready state.
    /// </summary>
    public void Reset()
    {
        TransitionTo(BreakpointState.Ready);
    }

    /// <summary>
    /// Checks if we can safely create a fork point.
    /// </summary>
    public bool IsSafeForkPoint => State is BreakpointState.Ready or BreakpointState.PostTool;
}
