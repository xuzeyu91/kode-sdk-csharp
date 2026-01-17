using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Tools;

namespace Kode.Agent.Tools.Builtin.Shell;

/// <summary>
/// Tool for executing shell commands.
/// </summary>
[Tool("bash_run")]
public sealed class BashRunTool : ToolBase<BashRunArgs>
{
    public override string Name => "bash_run";

    public override string Description =>
        "Execute a shell command in the sandbox environment. " +
        "Returns stdout, stderr, and exit code.";

    public override object InputSchema => JsonSchemaBuilder.BuildSchema<BashRunArgs>();

    public override ToolAttributes Attributes => new()
    {
        ReadOnly = false,
        RequiresApproval = true
    };

    public override ValueTask<string?> GetPromptAsync(ToolContext context)
    {
        return ValueTask.FromResult<string?>(
            "Use bash_run for executing shell commands. Commands are executed in a shell context. " +
            "For long-running commands, consider using background mode. " +
            "Always check exit codes to determine success.");
    }

    protected override async Task<ToolResult> ExecuteAsync(
        BashRunArgs args,
        ToolContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = new CommandOptions
            {
                WorkingDirectory = args.WorkingDirectory,
                Timeout = args.TimeoutSeconds.HasValue
                    ? TimeSpan.FromSeconds(args.TimeoutSeconds.Value)
                    : null,
                Background = args.Background
            };

            var result = await context.Sandbox.ExecuteCommandAsync(args.Command, options, cancellationToken);

            if (args.Background)
            {
                return ToolResult.Ok(new
                {
                    processId = result.ProcessId,
                    background = true,
                    message = "Command started in background"
                });
            }

            return ToolResult.Ok(new
            {
                exitCode = result.ExitCode,
                stdout = result.Stdout,
                stderr = result.Stderr,
                success = result.Success
            });
        }
        catch (OperationCanceledException)
        {
            return ToolResult.Fail("Command timed out");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Failed to execute command: {ex.Message}");
        }
    }
}

/// <summary>
/// Arguments for bash_run tool.
/// </summary>
[GenerateToolSchema]
public class BashRunArgs
{
    /// <summary>
    /// The command to execute.
    /// </summary>
    [ToolParameter(Description = "The shell command to execute")]
    public required string Command { get; init; }

    /// <summary>
    /// Optional working directory.
    /// </summary>
    [ToolParameter(Description = "Working directory for the command", Required = false)]
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Optional timeout in seconds.
    /// </summary>
    [ToolParameter(Description = "Timeout in seconds (default: 300)", Required = false)]
    public int? TimeoutSeconds { get; init; }

    /// <summary>
    /// Whether to run in background.
    /// </summary>
    [ToolParameter(Description = "Run command in background", Required = false)]
    public bool Background { get; init; }
}
