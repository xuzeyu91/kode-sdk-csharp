using System.Text.Json;
using Kode.Agent.Sdk.Core.Abstractions;

namespace Kode.Agent.Mcp;

/// <summary>
/// A dynamically created tool instance that can be configured at runtime.
/// Used for MCP tools and other dynamically discovered tools.
/// </summary>
public sealed class DynamicTool : ITool
{
    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the tool description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets or sets the input schema.
    /// </summary>
    public required object InputSchema { get; init; }

    /// <summary>
    /// Gets or sets the tool attributes.
    /// </summary>
    public ToolAttributes Attributes { get; init; } = new();

    /// <summary>
    /// Gets or sets the executor function.
    /// </summary>
    public required Func<JsonElement, ToolContext, CancellationToken, Task<JsonElement>> Executor { get; init; }

    /// <summary>
    /// Gets or sets optional metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }

    /// <summary>
    /// Gets or sets an optional prompt hint.
    /// </summary>
    public string? PromptHint { get; init; }

    /// <summary>
    /// Gets or sets the tool source.
    /// </summary>
    public ToolSource Source { get; init; } = ToolSource.Mcp;

    /// <inheritdoc />
    public ValueTask<string?> GetPromptAsync(ToolContext context)
    {
        return ValueTask.FromResult(PromptHint);
    }

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(
        object arguments,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        JsonElement argsElement;
        
        if (arguments is JsonElement je)
        {
            argsElement = je;
        }
        else
        {
            var json = JsonSerializer.Serialize(arguments);
            argsElement = JsonSerializer.Deserialize<JsonElement>(json);
        }

        try
        {
            var result = await Executor(argsElement, context, cancellationToken);
            return new ToolResult
            {
                Success = true,
                Value = result
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public ToolDescriptor ToDescriptor()
    {
        return new ToolDescriptor
        {
            Source = Source,
            Name = Name,
            RegistryId = Name,
            Metadata = Metadata?.Where(kvp => kvp.Value != null)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!)
        };
    }
}
