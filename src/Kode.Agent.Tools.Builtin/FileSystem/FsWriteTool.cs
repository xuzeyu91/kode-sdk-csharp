using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Tools;

namespace Kode.Agent.Tools.Builtin.FileSystem;

/// <summary>
/// Tool for writing file contents.
/// </summary>
[Tool("fs_write")]
public sealed class FsWriteTool : ToolBase<FsWriteArgs>
{
    public override string Name => "fs_write";

    public override string Description =>
        "Write content to a file. Creates the file if it doesn't exist, " +
        "or overwrites it if it does. Creates parent directories as needed.";

    public override object InputSchema => JsonSchemaBuilder.BuildSchema<FsWriteArgs>();

    public override ToolAttributes Attributes => new()
    {
        ReadOnly = false,
        RequiresApproval = true
    };

    protected override async Task<ToolResult> ExecuteAsync(
        FsWriteArgs args,
        ToolContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await context.Sandbox.WriteFileAsync(args.Path, args.Content, cancellationToken);

            var filePool = ToolContextFilePool.TryGetFilePool(context);
            if (filePool != null)
            {
                await filePool.RecordEditAsync(args.Path, cancellationToken);
            }

            return ToolResult.Ok(new
            {
                path = args.Path,
                bytesWritten = System.Text.Encoding.UTF8.GetByteCount(args.Content),
                success = true
            });
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Failed to write file: {ex.Message}");
        }
    }
}

/// <summary>
/// Arguments for fs_write tool.
/// </summary>
[GenerateToolSchema]
public class FsWriteArgs
{
    /// <summary>
    /// The path to the file to write.
    /// </summary>
    [ToolParameter(Description = "The absolute or relative path to the file to write")]
    public required string Path { get; init; }

    /// <summary>
    /// The content to write to the file.
    /// </summary>
    [ToolParameter(Description = "The content to write to the file")]
    public required string Content { get; init; }
}
