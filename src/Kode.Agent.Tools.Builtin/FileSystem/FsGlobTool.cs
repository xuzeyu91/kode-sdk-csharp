using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Tools;

namespace Kode.Agent.Tools.Builtin.FileSystem;

/// <summary>
/// Tool for searching files with glob patterns.
/// </summary>
[Tool("fs_glob")]
[ToolAttributes(ReadOnly = true, NoEffect = true)]
public sealed class FsGlobTool : ToolBase<FsGlobArgs>
{
    public override string Name => "fs_glob";

    public override string Description =>
        "Find files matching a glob pattern. Returns a list of matching file paths.";

    public override object InputSchema => JsonSchemaBuilder.BuildSchema<FsGlobArgs>();

    public override ToolAttributes Attributes => new()
    {
        ReadOnly = true,
        NoEffect = true
    };

    protected override async Task<ToolResult> ExecuteAsync(
        FsGlobArgs args,
        ToolContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var files = await context.Sandbox.GlobAsync(args.Pattern, cancellationToken);

            var result = files.AsEnumerable();
            if (args.MaxResults.HasValue)
            {
                result = result.Take(args.MaxResults.Value);
            }

            var fileList = result.ToList();

            return ToolResult.Ok(new
            {
                pattern = args.Pattern,
                files = fileList,
                count = fileList.Count,
                truncated = args.MaxResults.HasValue && files.Count > args.MaxResults.Value
            });
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Failed to search files: {ex.Message}");
        }
    }
}

/// <summary>
/// Arguments for fs_glob tool.
/// </summary>
[GenerateToolSchema]
public class FsGlobArgs
{
    /// <summary>
    /// The glob pattern to match.
    /// </summary>
    [ToolParameter(Description = "Glob pattern to match files (e.g., **/*.cs, src/**/*.json)")]
    public required string Pattern { get; init; }

    /// <summary>
    /// Maximum number of results to return.
    /// </summary>
    [ToolParameter(Description = "Maximum number of files to return", Required = false)]
    public int? MaxResults { get; init; }
}
