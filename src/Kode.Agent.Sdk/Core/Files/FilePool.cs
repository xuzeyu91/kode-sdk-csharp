using Kode.Agent.Sdk.Core.Context;
using Microsoft.Extensions.Logging;

namespace Kode.Agent.Sdk.Core.Files;

/// <summary>
/// File access record.
/// </summary>
public record FileRecord
{
    public required string Path { get; init; }
    public long? LastRead { get; set; }
    public long? LastEdit { get; set; }
    public long? LastReadMtime { get; set; }
    public long? LastEditMtime { get; set; }
    public long? LastKnownMtime { get; set; }
}

/// <summary>
/// File freshness check result.
/// </summary>
public record FileFreshness(
    bool IsFresh,
    long? LastRead = null,
    long? LastEdit = null,
    long? CurrentMtime = null
);

/// <summary>
/// File pool options.
/// </summary>
public record FilePoolOptions
{
    /// <summary>
    /// Enable file watching.
    /// </summary>
    public bool Watch { get; init; } = true;
    
    /// <summary>
    /// Callback when a file changes.
    /// </summary>
    public Action<FileChangeEvent>? OnChange { get; init; }
}

/// <summary>
/// File change event.
/// </summary>
public record FileChangeEvent(string Path, long Mtime);

/// <summary>
/// Tracks file access for context management and freshness validation.
/// </summary>
public class FilePool : IFilePool, IDisposable
{
    private readonly Dictionary<string, FileRecord> _records = new();
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new();
    private readonly ISandbox _sandbox;
    private readonly bool _watchEnabled;
    private readonly Action<FileChangeEvent>? _onChange;
    private readonly ILogger<FilePool>? _logger;
    private readonly object _lock = new();

    public FilePool(
        ISandbox sandbox,
        FilePoolOptions? options = null,
        ILogger<FilePool>? logger = null)
    {
        _sandbox = sandbox;
        _watchEnabled = options?.Watch ?? true;
        _onChange = options?.OnChange;
        _logger = logger;
    }

    /// <summary>
    /// Record a file read operation.
    /// </summary>
    public async Task RecordReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var resolved = ResolvePath(path);
        var mtime = await GetMtimeAsync(resolved, cancellationToken);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        lock (_lock)
        {
            if (!_records.TryGetValue(resolved, out var record))
            {
                record = new FileRecord { Path = resolved };
                _records[resolved] = record;
            }

            record.LastRead = now;
            record.LastReadMtime = mtime;
            record.LastKnownMtime = mtime;
        }

        EnsureWatch(resolved);
    }

    /// <summary>
    /// Record a file edit operation.
    /// </summary>
    public async Task RecordEditAsync(string path, CancellationToken cancellationToken = default)
    {
        var resolved = ResolvePath(path);
        var mtime = await GetMtimeAsync(resolved, cancellationToken);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        lock (_lock)
        {
            if (!_records.TryGetValue(resolved, out var record))
            {
                record = new FileRecord { Path = resolved };
                _records[resolved] = record;
            }

            record.LastEdit = now;
            record.LastEditMtime = mtime;
            record.LastKnownMtime = mtime;
        }

        EnsureWatch(resolved);
    }

    /// <summary>
    /// Record a file delete operation.
    /// </summary>
    public void RecordDelete(string path)
    {
        var resolved = ResolvePath(path);

        lock (_lock)
        {
            _records.Remove(resolved);

            if (_watchers.TryGetValue(resolved, out var watcher))
            {
                watcher.Dispose();
                _watchers.Remove(resolved);
            }
        }
    }

    /// <summary>
    /// Validate if a write operation is safe (file hasn't been externally modified).
    /// </summary>
    public async Task<FileFreshness> ValidateWriteAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var resolved = ResolvePath(path);
        var currentMtime = await GetMtimeAsync(resolved, cancellationToken);

        lock (_lock)
        {
            if (!_records.TryGetValue(resolved, out var record))
            {
                return new FileFreshness(IsFresh: true, CurrentMtime: currentMtime);
            }

            // Accept writes if the file hasn't changed since either the last read OR the last edit
            var matchesLastRead = record.LastReadMtime.HasValue && currentMtime == record.LastReadMtime;
            var matchesLastEdit = record.LastEditMtime.HasValue && currentMtime == record.LastEditMtime;
            var isFresh = !currentMtime.HasValue || matchesLastRead || matchesLastEdit;

            return new FileFreshness(
                IsFresh: isFresh,
                LastRead: record.LastRead,
                LastEdit: record.LastEdit,
                CurrentMtime: currentMtime
            );
        }
    }

    /// <summary>
    /// Check if a file is fresh (hasn't been modified since last read).
    /// </summary>
    public async Task<FileFreshness> CheckFreshnessAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var resolved = ResolvePath(path);
        var currentMtime = await GetMtimeAsync(resolved, cancellationToken);

        lock (_lock)
        {
            if (!_records.TryGetValue(resolved, out var record))
            {
                return new FileFreshness(IsFresh: false, CurrentMtime: currentMtime);
            }

            var isFresh = record.LastRead.HasValue &&
                (!currentMtime.HasValue || !record.LastKnownMtime.HasValue || currentMtime == record.LastKnownMtime);

            return new FileFreshness(
                IsFresh: isFresh,
                LastRead: record.LastRead,
                LastEdit: record.LastEdit,
                CurrentMtime: currentMtime
            );
        }
    }

    /// <summary>
    /// Get all tracked file paths.
    /// </summary>
    public IReadOnlyList<string> GetTrackedFiles()
    {
        lock (_lock)
        {
            return _records.Keys.ToList();
        }
    }

    /// <summary>
    /// Get all accessed files with their modification times.
    /// </summary>
    public IReadOnlyList<AccessedFile> GetAccessedFiles()
    {
        lock (_lock)
        {
            return _records.Values
                .Where(r => r.LastKnownMtime.HasValue)
                .Select(r => new AccessedFile(r.Path, r.LastKnownMtime!.Value))
                .ToList();
        }
    }

    /// <summary>
    /// Clear all tracked files.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            foreach (var watcher in _watchers.Values)
            {
                watcher.Dispose();
            }
            _watchers.Clear();
            _records.Clear();
        }
    }

    private string ResolvePath(string path)
    {
        return Path.GetFullPath(path, _sandbox.WorkingDirectory);
    }

    private async Task<long?> GetMtimeAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Exists)
            {
                return new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeMilliseconds();
            }
        }
        catch
        {
            // File doesn't exist or can't be accessed
        }

        await Task.CompletedTask;
        return null;
    }

    private void EnsureWatch(string path)
    {
        if (!_watchEnabled) return;

        lock (_lock)
        {
            if (_watchers.ContainsKey(path)) return;

            try
            {
                var directory = Path.GetDirectoryName(path);
                var fileName = Path.GetFileName(path);

                if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
                    return;

                if (!Directory.Exists(directory))
                    return;

                var watcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                };

                watcher.Changed += (sender, e) =>
                {
                    var mtime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    try
                    {
                        var info = new FileInfo(e.FullPath);
                        if (info.Exists)
                        {
                            mtime = new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeMilliseconds();
                        }
                    }
                    catch { }

                    lock (_lock)
                    {
                        if (_records.TryGetValue(path, out var record))
                        {
                            record.LastKnownMtime = mtime;
                        }
                    }

                    _onChange?.Invoke(new FileChangeEvent(path, mtime));
                };

                watcher.EnableRaisingEvents = true;
                _watchers[path] = watcher;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to watch file: {Path}", path);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var watcher in _watchers.Values)
            {
                watcher.Dispose();
            }
            _watchers.Clear();
        }
    }
}
