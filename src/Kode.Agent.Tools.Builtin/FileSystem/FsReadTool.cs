using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Tools;

namespace Kode.Agent.Tools.Builtin.FileSystem;

/// <summary>
/// Tool for reading file contents.
/// </summary>
[Tool("fs_read")]
[ToolAttributes(ReadOnly = true, NoEffect = true)]
public sealed class FsReadTool : ToolBase<FsReadArgs>
{
    public override string Name => "fs_read";

    public override string Description =>
        "Read the contents of a file. Returns the file content as text. " +
        "Supports optional line range selection.";

    public override object InputSchema => JsonSchemaBuilder.BuildSchema<FsReadArgs>();

    public override ToolAttributes Attributes => new()
    {
        ReadOnly = true,
        NoEffect = true
    };

    public override ValueTask<string?> GetPromptAsync(ToolContext context)
    {
        return ValueTask.FromResult<string?>(
            "When reading files, prefer reading larger sections over multiple small reads. " +
            "Use startLine and endLine to read specific sections efficiently.");
    }

    protected override async Task<ToolResult> ExecuteAsync(
        FsReadArgs args,
        ToolContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await context.Sandbox.FileExistsAsync(args.Path, cancellationToken))
            {
                return ToolResult.Fail($"File not found: {args.Path}");
            }

            var content = await context.Sandbox.ReadFileAsync(args.Path, cancellationToken);

            // Apply line range if specified
            if (args.StartLine.HasValue || args.EndLine.HasValue)
            {
                var lines = content.Split('\n');
                var start = Math.Max(0, (args.StartLine ?? 1) - 1);
                var end = Math.Min(lines.Length, args.EndLine ?? lines.Length);

                content = string.Join('\n', lines.Skip(start).Take(end - start));
            }

            var filePool = ToolContextFilePool.TryGetFilePool(context);
            if (filePool != null)
            {
                await filePool.RecordReadAsync(args.Path, cancellationToken);
            }

            return ToolResult.Ok(new
            {
                path = args.Path,
                content,
                lines = content.Split('\n').Length
            });
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Failed to read file: {ex.Message}");
        }
    }
}

/// <summary>
/// Arguments for fs_read tool.
/// </summary>
[GenerateToolSchema]
public class FsReadArgs
{
    /// <summary>
    /// The path to the file to read.
    /// </summary>
    [ToolParameter(Description = "The absolute or relative path to the file to read")]
    public required string Path { get; init; }

    /// <summary>
    /// Optional starting line number (1-based).
    /// </summary>
    [ToolParameter(Description = "The line number to start reading from (1-based)", Required = false)]
    public int? StartLine { get; init; }

    /// <summary>
    /// Optional ending line number (1-based, inclusive).
    /// </summary>
    [ToolParameter(Description = "The line number to stop reading at (1-based, inclusive)", Required = false)]
    public int? EndLine { get; init; }
}
