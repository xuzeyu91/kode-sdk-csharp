namespace Kode.Agent.Sdk.Core;

/// <summary>
/// Custom exceptions for the agent runtime.
/// </summary>
public class AgentException : Exception
{
    public string? ErrorCode { get; }

    public AgentException(string message, string? errorCode = null)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public AgentException(string message, Exception innerException, string? errorCode = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Exception thrown when agent state is invalid for the requested operation.
/// </summary>
public class InvalidAgentStateException : AgentException
{
    public Abstractions.AgentRuntimeState CurrentState { get; }
    public Abstractions.AgentRuntimeState? ExpectedState { get; }

    public InvalidAgentStateException(
        Abstractions.AgentRuntimeState currentState,
        Abstractions.AgentRuntimeState? expectedState = null,
        string? message = null)
        : base(message ?? $"Invalid agent state: {currentState}" + (expectedState.HasValue ? $", expected: {expectedState}" : ""),
               "INVALID_STATE")
    {
        CurrentState = currentState;
        ExpectedState = expectedState;
    }
}

/// <summary>
/// Exception thrown when a tool execution fails.
/// </summary>
public class ToolExecutionException : AgentException
{
    public string ToolName { get; }
    public string CallId { get; }

    public ToolExecutionException(string toolName, string callId, string message, Exception? innerException = null)
        : base(message, innerException!, "TOOL_EXECUTION_ERROR")
    {
        ToolName = toolName;
        CallId = callId;
    }
}

/// <summary>
/// Exception thrown when a tool is not found.
/// </summary>
public class ToolNotFoundException : AgentException
{
    public string ToolName { get; }

    public ToolNotFoundException(string toolName)
        : base($"Tool not found: {toolName}", "TOOL_NOT_FOUND")
    {
        ToolName = toolName;
    }
}

/// <summary>
/// Exception thrown when tool permission is denied.
/// </summary>
public class ToolPermissionDeniedException : AgentException
{
    public string ToolName { get; }
    public string CallId { get; }
    public string? Reason { get; }

    public ToolPermissionDeniedException(string toolName, string callId, string? reason = null)
        : base($"Permission denied for tool: {toolName}" + (reason != null ? $" ({reason})" : ""),
               "PERMISSION_DENIED")
    {
        ToolName = toolName;
        CallId = callId;
        Reason = reason;
    }
}

/// <summary>
/// Exception thrown when model call fails.
/// </summary>
public class ModelException : AgentException
{
    public string? Model { get; }
    public int? StatusCode { get; }

    public ModelException(string message, string? model = null, int? statusCode = null, Exception? innerException = null)
        : base(message, innerException!, "MODEL_ERROR")
    {
        Model = model;
        StatusCode = statusCode;
    }
}

/// <summary>
/// Exception thrown when checkpoint operations fail.
/// </summary>
public class CheckpointException : AgentException
{
    public string? CheckpointId { get; }

    public CheckpointException(string message, string? checkpointId = null, Exception? innerException = null)
        : base(message, innerException!, "CHECKPOINT_ERROR")
    {
        CheckpointId = checkpointId;
    }
}

/// <summary>
/// Exception thrown when max iterations is reached.
/// </summary>
public class MaxIterationsException : AgentException
{
    public int MaxIterations { get; }

    public MaxIterationsException(int maxIterations)
        : base($"Maximum iterations reached: {maxIterations}", "MAX_ITERATIONS")
    {
        MaxIterations = maxIterations;
    }
}
