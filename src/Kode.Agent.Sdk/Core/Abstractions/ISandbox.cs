namespace Kode.Agent.Sdk.Core.Abstractions;

/// <summary>
/// Sandbox interface for isolated code/command execution.
/// </summary>
public interface ISandbox : IAsyncDisposable
{
    /// <summary>
    /// Gets the sandbox identifier.
    /// </summary>
    string SandboxId { get; }

    /// <summary>
    /// Gets the working directory path within the sandbox.
    /// </summary>
    string WorkingDirectory { get; }

    /// <summary>
    /// Executes a shell command in the sandbox.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="options">Execution options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Command execution result.</returns>
    Task<CommandResult> ExecuteCommandAsync(
        string command,
        CommandOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a file from the sandbox.
    /// </summary>
    /// <param name="path">Relative or absolute path within sandbox.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>File content as string.</returns>
    Task<string> ReadFileAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes content to a file in the sandbox.
    /// </summary>
    /// <param name="path">Relative or absolute path within sandbox.</param>
    /// <param name="content">Content to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteFileAsync(string path, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file exists in the sandbox.
    /// </summary>
    Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a directory exists in the sandbox.
    /// </summary>
    Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a directory in the sandbox.
    /// </summary>
    Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from the sandbox.
    /// </summary>
    Task DeleteFileAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a directory from the sandbox.
    /// </summary>
    /// <param name="path">Directory path.</param>
    /// <param name="recursive">Whether to delete recursively.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteDirectoryAsync(string path, bool recursive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists files matching a glob pattern.
    /// </summary>
    /// <param name="pattern">Glob pattern.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching file paths.</returns>
    Task<IReadOnlyList<string>> GlobAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists directory contents.
    /// </summary>
    /// <param name="path">Directory path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of entries in the directory.</returns>
    Task<IReadOnlyList<DirectoryEntry>> ListDirectoryAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for text in files using grep-like functionality.
    /// </summary>
    /// <param name="pattern">Search pattern (regex or literal).</param>
    /// <param name="options">Search options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results.</returns>
    Task<IReadOnlyList<GrepResult>> GrepAsync(
        string pattern,
        GrepOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about a background process.
    /// </summary>
    /// <param name="processId">The process ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Process information, or null if not found.</returns>
    Task<ProcessInfo?> GetProcessAsync(int processId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kills a background process.
    /// </summary>
    /// <param name="processId">The process ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the process was killed, false if not found.</returns>
    Task<bool> KillProcessAsync(int processId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all running background processes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of running processes.</returns>
    Task<IReadOnlyList<ProcessInfo>> ListProcessesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Factory for creating sandbox instances.
/// </summary>
public interface ISandboxFactory
{
    /// <summary>
    /// Creates a new sandbox instance.
    /// </summary>
    /// <param name="options">Sandbox options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A new sandbox instance.</returns>
    Task<ISandbox> CreateAsync(SandboxOptions? options = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for creating a sandbox.
/// </summary>
public record SandboxOptions
{
    /// <summary>
    /// The working directory path.
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Whether to enforce sandbox boundary checks.
    /// When enabled, file operations (and optional working directories) are restricted to the sandbox working directory
    /// and any explicitly allowed paths.
    /// </summary>
    public bool EnforceBoundary { get; init; } = true;

    /// <summary>
    /// Additional absolute paths that are allowed when boundary checks are enabled.
    /// </summary>
    public IReadOnlyList<string>? AllowPaths { get; init; }

    /// <summary>
    /// Environment variables to set.
    /// </summary>
    public Dictionary<string, string>? EnvironmentVariables { get; init; }

    /// <summary>
    /// Timeout for sandbox operations.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Whether file watching is enabled (implementation-dependent).
    /// </summary>
    public bool WatchFiles { get; init; } = true;

    /// <summary>
    /// Whether to use Docker isolation.
    /// </summary>
    public bool UseDocker { get; init; }

    /// <summary>
    /// Docker image to use (if UseDocker is true).
    /// </summary>
    public string? DockerImage { get; init; }
}

/// <summary>
/// Command execution options.
/// </summary>
public record CommandOptions
{
    /// <summary>
    /// Working directory for the command.
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Environment variables.
    /// </summary>
    public Dictionary<string, string>? EnvironmentVariables { get; init; }

    /// <summary>
    /// Timeout for the command.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Whether to run in background.
    /// </summary>
    public bool Background { get; init; }
}

/// <summary>
/// Result of command execution.
/// </summary>
public record CommandResult
{
    /// <summary>
    /// Exit code of the command.
    /// </summary>
    public required int ExitCode { get; init; }

    /// <summary>
    /// Standard output.
    /// </summary>
    public required string Stdout { get; init; }

    /// <summary>
    /// Standard error.
    /// </summary>
    public required string Stderr { get; init; }

    /// <summary>
    /// Whether the command succeeded (exit code 0).
    /// </summary>
    public bool Success => ExitCode == 0;

    /// <summary>
    /// Process ID (for background commands).
    /// </summary>
    public int? ProcessId { get; init; }
}

/// <summary>
/// Options for grep search.
/// </summary>
public record GrepOptions
{
    /// <summary>
    /// File pattern to search in.
    /// </summary>
    public string? FilePattern { get; init; }

    /// <summary>
    /// Whether pattern is regex.
    /// </summary>
    public bool IsRegex { get; init; }

    /// <summary>
    /// Whether search is case-sensitive.
    /// </summary>
    public bool CaseSensitive { get; init; }

    /// <summary>
    /// Maximum number of results.
    /// </summary>
    public int? MaxResults { get; init; }

    /// <summary>
    /// Number of context lines before match.
    /// </summary>
    public int? ContextBefore { get; init; }

    /// <summary>
    /// Number of context lines after match.
    /// </summary>
    public int? ContextAfter { get; init; }
}

/// <summary>
/// Result of a grep search.
/// </summary>
public record GrepResult
{
    /// <summary>
    /// File path containing the match.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Line number of the match.
    /// </summary>
    public required int LineNumber { get; init; }

    /// <summary>
    /// The matching line content.
    /// </summary>
    public required string Line { get; init; }

    /// <summary>
    /// Context lines before the match.
    /// </summary>
    public IReadOnlyList<string>? ContextBefore { get; init; }

    /// <summary>
    /// Context lines after the match.
    /// </summary>
    public IReadOnlyList<string>? ContextAfter { get; init; }
}

/// <summary>
/// Entry in a directory listing.
/// </summary>
public record DirectoryEntry
{
    /// <summary>
    /// Name of the entry.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Full path of the entry.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Whether this is a directory.
    /// </summary>
    public required bool IsDirectory { get; init; }

    /// <summary>
    /// File size in bytes (null for directories).
    /// </summary>
    public long? Size { get; init; }

    /// <summary>
    /// Last modified time.
    /// </summary>
    public DateTime? LastModified { get; init; }
}

/// <summary>
/// Information about a background process.
/// </summary>
public record ProcessInfo
{
    /// <summary>
    /// The process ID.
    /// </summary>
    public required int ProcessId { get; init; }

    /// <summary>
    /// The command that was executed.
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    /// Whether the process is still running.
    /// </summary>
    public required bool IsRunning { get; init; }

    /// <summary>
    /// Exit code (null if still running).
    /// </summary>
    public int? ExitCode { get; init; }

    /// <summary>
    /// Standard output collected so far.
    /// </summary>
    public string? Stdout { get; init; }

    /// <summary>
    /// Standard error collected so far.
    /// </summary>
    public string? Stderr { get; init; }

    /// <summary>
    /// When the process was started.
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// When the process ended (null if still running).
    /// </summary>
    public DateTime? EndedAt { get; init; }
}
