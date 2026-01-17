using System.Collections.Concurrent;
using System.Diagnostics;

namespace Kode.Agent.Sdk.Infrastructure.Sandbox;

/// <summary>
/// Local file system sandbox implementation.
/// </summary>
public sealed class LocalSandbox : ISandbox
{
    private readonly string _workingDirectory;
    private readonly Dictionary<string, string> _environmentVariables;
    private readonly TimeSpan _defaultTimeout;
    private readonly ConcurrentDictionary<int, BackgroundProcess> _backgroundProcesses = new();
    private readonly bool _enforceBoundary;
    private readonly IReadOnlyList<string> _allowPaths;

    public string SandboxId { get; }
    public string WorkingDirectory => _workingDirectory;

    public LocalSandbox(SandboxOptions? options = null)
    {
        SandboxId = Guid.NewGuid().ToString("N");
        _workingDirectory = ToFullPath(options?.WorkingDirectory ?? Environment.CurrentDirectory);
        _environmentVariables = options?.EnvironmentVariables ?? [];
        _defaultTimeout = options?.Timeout ?? TimeSpan.FromMinutes(5);
        _enforceBoundary = options?.EnforceBoundary ?? true;
        _allowPaths = (options?.AllowPaths ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(ToFullPath)
            .Distinct(GetPathComparer())
            .ToArray();
    }

    /// <summary>
    /// Tracks background process info.
    /// </summary>
    private sealed class BackgroundProcess
    {
        public Process? Process { get; init; }
        public string Command { get; init; } = "";
        public DateTime StartedAt { get; init; }
        public DateTime? EndedAt { get; set; }
        public int? ExitCode { get; set; }
        public System.Text.StringBuilder Stdout { get; } = new();
        public System.Text.StringBuilder Stderr { get; } = new();
        public bool IsRunning => Process?.HasExited == false;
    }

    public async Task<CommandResult> ExecuteCommandAsync(
        string command,
        CommandOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var workDir = options?.WorkingDirectory;
        var resolvedWorkDir = string.IsNullOrWhiteSpace(workDir)
            ? _workingDirectory
            : Path.IsPathRooted(workDir)
                ? ToFullPath(workDir)
                : ToFullPath(Path.Combine(_workingDirectory, workDir));

        if (!IsInside(resolvedWorkDir))
        {
            return new CommandResult
            {
                ExitCode = 1,
                Stdout = "",
                Stderr = $"Working directory outside sandbox: {workDir}"
            };
        }
        var timeout = options?.Timeout ?? _defaultTimeout;

        // Dangerous command patterns (best-effort), aligned with TypeScript sandbox behavior
        if (LooksDangerousCommand(command))
        {
            return new CommandResult
            {
                ExitCode = 1,
                Stdout = "",
                Stderr = $"Dangerous command blocked for security: {Truncate(command, 120)}"
            };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = GetShell(),
            Arguments = GetShellArgs(command),
            WorkingDirectory = resolvedWorkDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Add environment variables
        foreach (var (key, value) in _environmentVariables)
        {
            startInfo.Environment[key] = value;
        }

        if (options?.EnvironmentVariables != null)
        {
            foreach (var (key, value) in options.EnvironmentVariables)
            {
                startInfo.Environment[key] = value;
            }
        }

        using var process = new Process { StartInfo = startInfo };
        var stdout = new System.Text.StringBuilder();
        var stderr = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) stderr.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (options?.Background == true)
        {
            var bgProcess = new BackgroundProcess
            {
                Process = process,
                Command = command,
                StartedAt = DateTime.UtcNow
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) bgProcess.Stdout.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) bgProcess.Stderr.AppendLine(e.Data);
            };
            process.Exited += (_, _) =>
            {
                bgProcess.EndedAt = DateTime.UtcNow;
                bgProcess.ExitCode = process.ExitCode;
            };
            process.EnableRaisingEvents = true;

            _backgroundProcesses.TryAdd(process.Id, bgProcess);

            return new CommandResult
            {
                ExitCode = 0,
                Stdout = "",
                Stderr = "",
                ProcessId = process.Id
            };
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(true);
            throw;
        }

        return new CommandResult
        {
            ExitCode = process.ExitCode,
            Stdout = stdout.ToString(),
            Stderr = stderr.ToString()
        };
    }

    public async Task<string> ReadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        return await File.ReadAllTextAsync(fullPath, cancellationToken);
    }

    public async Task WriteFileAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        await File.WriteAllTextAsync(fullPath, content, cancellationToken);
    }

    public Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        return Task.FromResult(File.Exists(fullPath));
    }

    public Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        return Task.FromResult(Directory.Exists(fullPath));
    }

    public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        Directory.CreateDirectory(fullPath);
        return Task.CompletedTask;
    }

    public Task DeleteFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        return Task.CompletedTask;
    }

    public Task DeleteDirectoryAsync(string path, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GlobAsync(string pattern, CancellationToken cancellationToken = default)
    {
        // Extract just the filename pattern from the input
        // This handles both relative patterns (e.g., "*.cs", "**/*.cs")
        // and absolute patterns (e.g., "/path/to/dir/**") by only using the filename part
        var fileNamePattern = Path.GetFileName(pattern);

        // If pattern doesn't have a filename part (e.g., ends with "/" or "**"), use "*"
        if (string.IsNullOrEmpty(fileNamePattern) || fileNamePattern == "**" || fileNamePattern == ".")
        {
            fileNamePattern = "*";
        }

        var searchPattern = fileNamePattern.Replace("**", "*");
        var searchOption = pattern.Contains("**") ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        try
        {
            var files = Directory.GetFiles(_workingDirectory, searchPattern, searchOption)
                .Select(f => Path.GetRelativePath(_workingDirectory, f))
                .ToList();

            return Task.FromResult<IReadOnlyList<string>>(files);
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            // Return empty list on any IO errors
            return Task.FromResult<IReadOnlyList<string>>([]);
        }
    }

    public async Task<IReadOnlyList<GrepResult>> GrepAsync(
        string pattern,
        GrepOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<GrepResult>();
        var filePattern = options?.FilePattern ?? "*";
        var isRegex = options?.IsRegex ?? false;
        var caseSensitive = options?.CaseSensitive ?? false;
        var maxResults = options?.MaxResults ?? 100;

        var files = await GlobAsync(filePattern, cancellationToken);

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        System.Text.RegularExpressions.Regex? regex = null;
        if (isRegex)
        {
            var regexOptions = caseSensitive
                ? System.Text.RegularExpressions.RegexOptions.None
                : System.Text.RegularExpressions.RegexOptions.IgnoreCase;
            regex = new System.Text.RegularExpressions.Regex(pattern, regexOptions);
        }

        foreach (var file in files)
        {
            if (results.Count >= maxResults) break;

            var fullPath = GetFullPath(file);
            if (!File.Exists(fullPath)) continue;

            var lines = await File.ReadAllLinesAsync(fullPath, cancellationToken);
            for (var i = 0; i < lines.Length && results.Count < maxResults; i++)
            {
                var line = lines[i];
                var matches = isRegex
                    ? regex!.IsMatch(line)
                    : line.Contains(pattern, comparison);

                if (matches)
                {
                    var contextBefore = options?.ContextBefore ?? 0;
                    var contextAfter = options?.ContextAfter ?? 0;

                    results.Add(new GrepResult
                    {
                        FilePath = file,
                        LineNumber = i + 1,
                        Line = line,
                        ContextBefore = contextBefore > 0
                            ? lines.Skip(Math.Max(0, i - contextBefore)).Take(Math.Min(contextBefore, i)).ToList()
                            : null,
                        ContextAfter = contextAfter > 0
                            ? lines.Skip(i + 1).Take(contextAfter).ToList()
                            : null
                    });
                }
            }
        }

        return results;
    }

    public Task<IReadOnlyList<DirectoryEntry>> ListDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        var results = new List<DirectoryEntry>();

        if (!Directory.Exists(fullPath))
        {
            return Task.FromResult<IReadOnlyList<DirectoryEntry>>(results);
        }

        // Add directories
        foreach (var dir in Directory.GetDirectories(fullPath))
        {
            var dirInfo = new DirectoryInfo(dir);
            results.Add(new DirectoryEntry
            {
                Name = dirInfo.Name,
                Path = Path.GetRelativePath(_workingDirectory, dir),
                IsDirectory = true,
                LastModified = dirInfo.LastWriteTimeUtc
            });
        }

        // Add files
        foreach (var file in Directory.GetFiles(fullPath))
        {
            var fileInfo = new FileInfo(file);
            results.Add(new DirectoryEntry
            {
                Name = fileInfo.Name,
                Path = Path.GetRelativePath(_workingDirectory, file),
                IsDirectory = false,
                Size = fileInfo.Length,
                LastModified = fileInfo.LastWriteTimeUtc
            });
        }

        return Task.FromResult<IReadOnlyList<DirectoryEntry>>(results);
    }

    public Task<ProcessInfo?> GetProcessAsync(int processId, CancellationToken cancellationToken = default)
    {
        if (!_backgroundProcesses.TryGetValue(processId, out var bgProcess))
        {
            return Task.FromResult<ProcessInfo?>(null);
        }

        return Task.FromResult<ProcessInfo?>(new ProcessInfo
        {
            ProcessId = processId,
            Command = bgProcess.Command,
            IsRunning = bgProcess.IsRunning,
            ExitCode = bgProcess.ExitCode,
            Stdout = bgProcess.Stdout.ToString(),
            Stderr = bgProcess.Stderr.ToString(),
            StartedAt = bgProcess.StartedAt,
            EndedAt = bgProcess.EndedAt
        });
    }

    public Task<bool> KillProcessAsync(int processId, CancellationToken cancellationToken = default)
    {
        if (!_backgroundProcesses.TryRemove(processId, out var bgProcess))
        {
            return Task.FromResult(false);
        }

        try
        {
            if (bgProcess.Process is { HasExited: false })
            {
                bgProcess.Process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Process may have already exited
        }

        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<ProcessInfo>> ListProcessesAsync(CancellationToken cancellationToken = default)
    {
        var results = _backgroundProcesses.Select(kvp => new ProcessInfo
        {
            ProcessId = kvp.Key,
            Command = kvp.Value.Command,
            IsRunning = kvp.Value.IsRunning,
            ExitCode = kvp.Value.ExitCode,
            Stdout = kvp.Value.Stdout.ToString(),
            Stderr = kvp.Value.Stderr.ToString(),
            StartedAt = kvp.Value.StartedAt,
            EndedAt = kvp.Value.EndedAt
        }).ToList();

        return Task.FromResult<IReadOnlyList<ProcessInfo>>(results);
    }

    public ValueTask DisposeAsync()
    {
        // Kill all background processes
        foreach (var kvp in _backgroundProcesses)
        {
            try
            {
                if (kvp.Value.Process is { HasExited: false })
                {
                    kvp.Value.Process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
        _backgroundProcesses.Clear();

        return ValueTask.CompletedTask;
    }

    private string GetFullPath(string path)
    {
        var resolved = Path.IsPathRooted(path)
            ? ToFullPath(path)
            : ToFullPath(Path.Combine(_workingDirectory, path));

        if (!IsInside(resolved))
        {
            throw new UnauthorizedAccessException($"Path outside sandbox: {path}");
        }

        return resolved;
    }

    private bool IsInside(string fullPath)
    {
        // 1) Always allow anything under sandbox working directory
        var relativeToWork = Path.GetRelativePath(_workingDirectory, fullPath);
        if (!relativeToWork.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relativeToWork))
        {
            return true;
        }

        // 2) If boundary checks are disabled, allow all paths
        if (!_enforceBoundary)
        {
            return true;
        }

        // 3) Allow whitelist paths
        foreach (var allowed in _allowPaths)
        {
            var relative = Path.GetRelativePath(allowed, fullPath);
            if (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative))
            {
                return true;
            }
        }

        return false;
    }

    private static string ToFullPath(string path)
    {
        return Path.GetFullPath(path);
    }

    private static IEqualityComparer<string> GetPathComparer()
    {
        return OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }

    private static bool LooksDangerousCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;
        var c = command;

        // Keep this list small and conservative; it is a best-effort guardrail, not a security boundary.
        var patterns = new[]
        {
            @"rm\s+-rf\s+\/($|\s)",
            @"\bsudo\s+",
            @"\bshutdown\b",
            @"\breboot\b",
            @"\bmkfs\.",
            @"\bdd\s+.*\bof=",
            @":\(\)\{\s*:\|\:&\s*\};:",
            @"\bchmod\s+777\s+\/",
            @"\bcurl\s+.*\|\s*(bash|sh)\b",
            @"\bwget\s+.*\|\s*(bash|sh)\b",
            @">\s*\/dev\/sda",
            @"\bmkswap\b",
            @"\bswapon\b"
        };

        foreach (var p in patterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(c, p, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string Truncate(string value, int max)
    {
        if (value.Length <= max) return value;
        return value[..max];
    }

    private static string GetShell()
    {
        return OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash";
    }

    private static string GetShellArgs(string command)
    {
        return OperatingSystem.IsWindows() ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"";
    }
}

/// <summary>
/// Factory for creating local sandbox instances.
/// </summary>
public sealed class LocalSandboxFactory : ISandboxFactory
{
    public Task<ISandbox> CreateAsync(SandboxOptions? options = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ISandbox>(new LocalSandbox(options));
    }
}
