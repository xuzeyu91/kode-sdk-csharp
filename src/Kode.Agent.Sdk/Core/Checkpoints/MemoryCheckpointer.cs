using System.Collections.Concurrent;
using System.Text.Json;

namespace Kode.Agent.Sdk.Core.Checkpoints;

/// <summary>
/// In-memory checkpointer implementation.
/// </summary>
public class MemoryCheckpointer : ICheckpointer
{
    private readonly ConcurrentDictionary<string, Checkpoint> _checkpoints = new();

    public Task<string> SaveAsync(Checkpoint checkpoint, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        // Deep clone to avoid reference issues
        var json = JsonSerializer.Serialize(checkpoint);
        var cloned = JsonSerializer.Deserialize<Checkpoint>(json)!;
        
        _checkpoints[checkpoint.Id] = cloned;
        return Task.FromResult(checkpoint.Id);
    }

    public Task<Checkpoint?> LoadAsync(string checkpointId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        if (_checkpoints.TryGetValue(checkpointId, out var checkpoint))
        {
            // Deep clone to avoid reference issues
            var json = JsonSerializer.Serialize(checkpoint);
            var cloned = JsonSerializer.Deserialize<Checkpoint>(json);
            return Task.FromResult(cloned);
        }
        
        return Task.FromResult<Checkpoint?>(null);
    }

    public Task<IReadOnlyList<CheckpointListItem>> ListAsync(
        string agentId,
        CheckpointListOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var query = _checkpoints.Values
            .Where(cp => cp.AgentId == agentId);

        if (options?.SessionId != null)
        {
            query = query.Where(cp => cp.SessionId == options.SessionId);
        }

        var sorted = query
            .OrderByDescending(cp => cp.Timestamp)
            .ToList();

        var start = options?.Offset ?? 0;
        var count = options?.Limit ?? sorted.Count;
        
        var result = sorted
            .Skip(start)
            .Take(count)
            .Select(cp => new CheckpointListItem
            {
                Id = cp.Id,
                AgentId = cp.AgentId,
                SessionId = cp.SessionId,
                Timestamp = cp.Timestamp,
                IsForkPoint = cp.Metadata.IsForkPoint,
                Tags = cp.Metadata.Tags
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<CheckpointListItem>>(result);
    }

    public Task DeleteAsync(string checkpointId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _checkpoints.TryRemove(checkpointId, out _);
        return Task.CompletedTask;
    }

    public async Task<string> ForkAsync(string checkpointId, string newAgentId, CancellationToken cancellationToken = default)
    {
        var original = await LoadAsync(checkpointId, cancellationToken);
        if (original == null)
        {
            throw new KeyNotFoundException($"Checkpoint not found: {checkpointId}");
        }

        var forkedId = $"{newAgentId}:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var forked = original with
        {
            Id = forkedId,
            AgentId = newAgentId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Metadata = original.Metadata with
            {
                ParentCheckpointId = checkpointId
            }
        };

        return await SaveAsync(forked, cancellationToken);
    }

    /// <summary>
    /// Get the total number of checkpoints.
    /// </summary>
    public int Count => _checkpoints.Count;

    /// <summary>
    /// Clear all checkpoints.
    /// </summary>
    public void Clear() => _checkpoints.Clear();
}
