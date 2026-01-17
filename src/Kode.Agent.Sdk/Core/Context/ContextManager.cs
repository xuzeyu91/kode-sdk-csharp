using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Kode.Agent.Sdk.Core.Context;

/// <summary>
/// Context usage analysis result.
/// </summary>
public record ContextUsage(
    int TotalTokens,
    int MessageCount,
    bool ShouldCompress
);

/// <summary>
/// Compression result.
/// </summary>
public record CompressionResult(
    Message Summary,
    IReadOnlyList<Message> RemovedMessages,
    IReadOnlyList<Message> RetainedMessages,
    string WindowId,
    string CompressionId,
    double Ratio
);

/// <summary>
/// Context manager options.
/// </summary>
public record ContextManagerOptions
{
    /// <summary>
    /// Maximum tokens before triggering compression.
    /// </summary>
    public int MaxTokens { get; init; } = 50_000;
    
    /// <summary>
    /// Target tokens after compression.
    /// </summary>
    public int CompressToTokens { get; init; } = 30_000;
    
    /// <summary>
    /// Model to use for compression summary.
    /// </summary>
    public string CompressionModel { get; init; } = "claude-3-haiku";
    
    /// <summary>
    /// Prompt for compression summary.
    /// </summary>
    public string CompressionPrompt { get; init; } = "Summarize the conversation history concisely";
}

/// <summary>
/// History window for storing compressed context.
/// </summary>
public record HistoryWindow
{
    public required string Id { get; init; }
    public required IReadOnlyList<Message> Messages { get; init; }
    public IReadOnlyList<Timeline> Events { get; init; } = Array.Empty<Timeline>();
    public required HistoryWindowStats Stats { get; init; }
    public required long Timestamp { get; init; }
}

/// <summary>
/// History window statistics.
/// </summary>
public record HistoryWindowStats(
    int MessageCount,
    int TokenCount,
    int EventCount = 0
);

/// <summary>
/// Compression record for auditing.
/// </summary>
public record CompressionRecord
{
    public required string Id { get; init; }
    public required string WindowId { get; init; }
    public required CompressionConfig Config { get; init; }
    public required string Summary { get; init; }
    public required double Ratio { get; init; }
    public IReadOnlyList<string>? RecoveredFiles { get; init; }
    public required long Timestamp { get; init; }
}

/// <summary>
/// Compression configuration.
/// </summary>
public record CompressionConfig(
    string Model,
    string Prompt,
    int Threshold
);

/// <summary>
/// Recovered file snapshot (used by store/history).
/// </summary>
public record RecoveredFile
{
    public required string Path { get; init; }
    public required string Content { get; init; }
    public long Mtime { get; init; }
    public required long Timestamp { get; init; }
}

/// <summary>
/// Manages context window and compression.
/// </summary>
public class ContextManager
{
    private readonly IAgentStore _store;
    private readonly string _agentId;
    private readonly ContextManagerOptions _options;
    private readonly ILogger<ContextManager>? _logger;

    public ContextManager(
        IAgentStore store,
        string agentId,
        ContextManagerOptions? options = null,
        ILogger<ContextManager>? logger = null)
    {
        _store = store;
        _agentId = agentId;
        _options = options ?? new ContextManagerOptions();
        _logger = logger;
    }

    /// <summary>
    /// Analyze context usage with rough token estimation.
    /// </summary>
    public ContextUsage Analyze(IReadOnlyList<Message> messages)
    {
        var totalTokens = 0;
        
        foreach (var message in messages)
        {
            foreach (var block in message.Content)
            {
                var text = block switch
                {
                    TextContent t => t.Text,
                    ToolUseContent tu => JsonSerializer.Serialize(tu.Input),
                    ToolResultContent tr => tr.Content?.ToString() ?? "",
                    _ => ""
                };
                
                // Rough estimation: 4 characters = 1 token
                totalTokens += (text.Length + 3) / 4;
            }
        }

        return new ContextUsage(
            TotalTokens: totalTokens,
            MessageCount: messages.Count,
            ShouldCompress: totalTokens > _options.MaxTokens
        );
    }

    /// <summary>
    /// Compress context and save history.
    /// </summary>
    public async Task<CompressionResult?> CompressAsync(
        IReadOnlyList<Message> messages,
        IReadOnlyList<Timeline> events,
        IFilePool? filePool = null,
        ISandbox? sandbox = null,
        CancellationToken cancellationToken = default)
    {
        var usage = Analyze(messages);
        if (!usage.ShouldCompress)
        {
            return null;
        }

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowId = $"window-{timestamp}";
        var compressionId = $"comp-{timestamp}";

        // 1. Save history window
        var window = new HistoryWindow
        {
            Id = windowId,
            Messages = messages,
            Events = events,
            Stats = new HistoryWindowStats(messages.Count, usage.TotalTokens, events.Count),
            Timestamp = timestamp
        };
        await SaveHistoryWindowAsync(window, cancellationToken);

        // 2. Execute compression (simplified: keep 60% of messages)
        var targetRatio = (double)_options.CompressToTokens / usage.TotalTokens;
        var keepCount = Math.Max(1, (int)Math.Ceiling(messages.Count * Math.Max(targetRatio, 0.6)));
        
        var retainedMessages = messages.Skip(messages.Count - keepCount).ToList();
        var removedMessages = messages.Take(messages.Count - keepCount).ToList();

        // Sanitize orphan tool results
        retainedMessages = SanitizeOrphanToolResults(retainedMessages);

        // Generate summary
        var summaryText = GenerateSummary(removedMessages);
        var summary = Message.System(
            $"<context-summary timestamp=\"{DateTimeOffset.UtcNow:O}\" window=\"{windowId}\">\n{summaryText}\n</context-summary>"
        );

        // 3. Save compression record
        var ratio = (double)retainedMessages.Count / messages.Count;
        var recoveredPaths = new List<string>();
        if (filePool != null && sandbox != null)
        {
            foreach (var f in filePool.GetAccessedFiles().Take(5))
            {
                recoveredPaths.Add(f.Path);
                try
                {
                    var content = await sandbox.ReadFileAsync(f.Path, cancellationToken);
                    await _store.SaveRecoveredFileAsync(_agentId, new RecoveredFile
                    {
                        Path = f.Path,
                        Content = content,
                        Mtime = f.ModifiedTime,
                        Timestamp = timestamp
                    }, cancellationToken);
                }
                catch (Exception ex)
                {
                    await _store.SaveRecoveredFileAsync(_agentId, new RecoveredFile
                    {
                        Path = f.Path,
                        Content = $"// Failed to read file: {ex.Message}",
                        Mtime = f.ModifiedTime,
                        Timestamp = timestamp
                    }, cancellationToken);
                }
            }
        }

        var record = new CompressionRecord
        {
            Id = compressionId,
            WindowId = windowId,
            Config = new CompressionConfig(
                _options.CompressionModel,
                _options.CompressionPrompt,
                _options.MaxTokens
            ),
            Summary = summaryText.Length > 500 ? summaryText[..500] : summaryText,
            Ratio = ratio,
            RecoveredFiles = recoveredPaths,
            Timestamp = timestamp
        };
        await SaveCompressionRecordAsync(record, cancellationToken);

        _logger?.LogInformation(
            "Compressed context: {Removed} messages removed, {Retained} retained, ratio {Ratio:P}",
            removedMessages.Count, retainedMessages.Count, ratio);

        return new CompressionResult(
            Summary: summary,
            RemovedMessages: removedMessages,
            RetainedMessages: retainedMessages,
            WindowId: windowId,
            CompressionId: compressionId,
            Ratio: ratio
        );
    }

    /// <summary>
    /// Loads history windows for auditing/debugging (aligned with TS loadHistory).
    /// </summary>
    public Task<IReadOnlyList<HistoryWindow>> LoadHistoryAsync(CancellationToken cancellationToken = default)
        => _store.LoadHistoryWindowsAsync(_agentId, cancellationToken);

    /// <summary>
    /// Loads compression records (aligned with TS loadCompressions).
    /// </summary>
    public Task<IReadOnlyList<CompressionRecord>> LoadCompressionsAsync(CancellationToken cancellationToken = default)
        => _store.LoadCompressionRecordsAsync(_agentId, cancellationToken);

    /// <summary>
    /// Loads recovered files (aligned with TS loadRecoveredFiles).
    /// </summary>
    public Task<IReadOnlyList<RecoveredFile>> LoadRecoveredFilesAsync(CancellationToken cancellationToken = default)
        => _store.LoadRecoveredFilesAsync(_agentId, cancellationToken);

    /// <summary>
    /// Sanitize orphan tool results that lost their paired tool_use.
    /// </summary>
    private static List<Message> SanitizeOrphanToolResults(List<Message> messages)
    {
        // Collect all tool_use IDs
        var toolUseIds = new HashSet<string>();
        foreach (var msg in messages)
        {
            foreach (var block in msg.Content.OfType<ToolUseContent>())
            {
                toolUseIds.Add(block.Id);
            }
        }

        // Convert orphan tool_result blocks to text
        var result = new List<Message>();
        foreach (var msg in messages)
        {
            var newContent = new List<ContentBlock>();
            var modified = false;

            foreach (var block in msg.Content)
            {
                if (block is ToolResultContent tr && !toolUseIds.Contains(tr.ToolUseId))
                {
                    // Convert to text
                    newContent.Add(new TextContent
                    {
                        Text = $"[Previous tool result: {tr.Content?.ToString() ?? "(empty)"}]"
                    });
                    modified = true;
                }
                else
                {
                    newContent.Add(block);
                }
            }

            result.Add(modified ? msg with { Content = newContent } : msg);
        }

        return result;
    }

    /// <summary>
    /// Generate a summary of removed messages.
    /// </summary>
    private static string GenerateSummary(IReadOnlyList<Message> removedMessages)
    {
        var lines = new List<string>();
        lines.Add($"Compressed {removedMessages.Count} messages from conversation history.");

        var userMessages = 0;
        var assistantMessages = 0;
        var toolCalls = 0;

        foreach (var msg in removedMessages)
        {
            if (msg.Role == MessageRole.User) userMessages++;
            else if (msg.Role == MessageRole.Assistant) assistantMessages++;

            toolCalls += msg.Content.OfType<ToolUseContent>().Count();
        }

        lines.Add($"Summary: {userMessages} user messages, {assistantMessages} assistant responses, {toolCalls} tool calls.");

        // Extract key topics from first and last user messages
        var firstUser = removedMessages.FirstOrDefault(m => m.Role == MessageRole.User);
        var lastUser = removedMessages.LastOrDefault(m => m.Role == MessageRole.User);

        if (firstUser != null)
        {
            var text = string.Join(" ", firstUser.Content.OfType<TextContent>().Select(t => t.Text));
            if (text.Length > 0)
            {
                lines.Add($"Initial topic: {Preview(text, 200)}");
            }
        }

        if (lastUser != null && lastUser != firstUser)
        {
            var text = string.Join(" ", lastUser.Content.OfType<TextContent>().Select(t => t.Text));
            if (text.Length > 0)
            {
                lines.Add($"Last topic: {Preview(text, 200)}");
            }
        }

        return string.Join("\n", lines);
    }

    private static string Preview(string text, int limit)
    {
        return text.Length > limit ? text[..limit] + "…" : text;
    }

    private async Task SaveHistoryWindowAsync(HistoryWindow window, CancellationToken ct)
    {
        await _store.SaveHistoryWindowAsync(_agentId, window, ct);
        _logger?.LogDebug("Saved history window {WindowId}", window.Id);
    }

    private async Task SaveCompressionRecordAsync(CompressionRecord record, CancellationToken ct)
    {
        await _store.SaveCompressionRecordAsync(_agentId, record, ct);
        _logger?.LogDebug("Saved compression record {RecordId}", record.Id);
    }
}

/// <summary>
/// Interface for file access tracking.
/// </summary>
public interface IFilePool
{
    /// <summary>
    /// Get recently accessed files.
    /// </summary>
    IReadOnlyList<AccessedFile> GetAccessedFiles();
}

/// <summary>
/// Accessed file information.
/// </summary>
public record AccessedFile(string Path, long ModifiedTime);
