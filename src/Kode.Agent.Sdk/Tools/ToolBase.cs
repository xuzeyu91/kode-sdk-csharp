using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kode.Agent.Sdk.Tools;

/// <summary>
/// Base class for implementing tools.
/// </summary>
public abstract class ToolBase : ITool
{
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public abstract object InputSchema { get; }

    /// <inheritdoc />
    public virtual ToolAttributes Attributes => new();

    /// <inheritdoc />
    public virtual ValueTask<string?> GetPromptAsync(ToolContext context) => ValueTask.FromResult<string?>(null);

    /// <inheritdoc />
    public abstract Task<ToolResult> ExecuteAsync(
        object arguments,
        ToolContext context,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public virtual ToolDescriptor ToDescriptor() => new()
    {
        Source = ToolSource.Registered,
        Name = Name
    };

    /// <summary>
    /// Helper to emit a custom event from tool execution.
    /// </summary>
    protected void Emit(ToolContext context, string eventType, object? data = null)
    {
        context.Emit?.Invoke(eventType, data);
    }

    /// <summary>
    /// Helper to deserialize arguments.
    /// </summary>
    protected T DeserializeArgs<T>(object arguments)
    {
        if (arguments is T typed)
        {
            return typed;
        }

        if (arguments is JsonElement element)
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText(), CamelCaseOptions)
                ?? throw new ArgumentException($"Failed to deserialize arguments to {typeof(T).Name}");
        }

        var json = JsonSerializer.Serialize(arguments);
        return JsonSerializer.Deserialize<T>(json, CamelCaseOptions)
            ?? throw new ArgumentException($"Failed to deserialize arguments to {typeof(T).Name}");
    }
}

/// <summary>
/// Generic base class for tools with typed arguments.
/// </summary>
/// <typeparam name="TArgs">The arguments type.</typeparam>
public abstract class ToolBase<TArgs> : ToolBase where TArgs : class
{
    /// <inheritdoc />
    public sealed override async Task<ToolResult> ExecuteAsync(
        object arguments,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var args = DeserializeArgs<TArgs>(arguments);
            return await ExecuteAsync(args, context, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ToolResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Executes the tool with typed arguments.
    /// </summary>
    protected abstract Task<ToolResult> ExecuteAsync(
        TArgs arguments,
        ToolContext context,
        CancellationToken cancellationToken);
}
