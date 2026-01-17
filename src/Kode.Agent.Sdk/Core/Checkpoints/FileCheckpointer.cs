using System.Text.Json;

namespace Kode.Agent.Sdk.Core.Checkpoints;

/// <summary>
/// File-based checkpointer implementation.
/// </summary>
public class FileCheckpointer : ICheckpointer
{
    private readonly string _basePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileCheckpointer(string basePath)
    {
        _basePath = basePath;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveAsync(Checkpoint checkpoint, CancellationToken cancellationToken = default)
    {
        var agentDir = Path.Combine(_basePath, checkpoint.AgentId, "checkpoints");
        Directory.CreateDirectory(agentDir);
        
        var filePath = Path.Combine(agentDir, $"{checkpoint.Id}.json");
        var json = JsonSerializer.Serialize(checkpoint, _jsonOptions);
        
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
        
        return checkpoint.Id;
    }

    public async Task<Checkpoint?> LoadAsync(string checkpointId, CancellationToken cancellationToken = default)
    {
        // Search for the checkpoint in all agent directories
        var agentDirs = Directory.GetDirectories(_basePath);
        
        foreach (var agentDir in agentDirs)
        {
            var checkpointsDir = Path.Combine(agentDir, "checkpoints");
            if (!Directory.Exists(checkpointsDir)) continue;
            
            var filePath = Path.Combine(checkpointsDir, $"{checkpointId}.json");
            if (File.Exists(filePath))
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                return JsonSerializer.Deserialize<Checkpoint>(json, _jsonOptions);
            }
        }
        
        return null;
    }

    public async Task<IReadOnlyList<CheckpointListItem>> ListAsync(
        string agentId,
        CheckpointListOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var checkpointsDir = Path.Combine(_basePath, agentId, "checkpoints");
        
        if (!Directory.Exists(checkpointsDir))
        {
            return [];
        }

        var files = Directory.GetFiles(checkpointsDir, "*.json");
        var checkpoints = new List<Checkpoint>();

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var checkpoint = JsonSerializer.Deserialize<Checkpoint>(json, _jsonOptions);
                if (checkpoint != null)
                {
                    checkpoints.Add(checkpoint);
                }
            }
            catch
            {
                // Skip invalid files
            }
        }

        var query = checkpoints.AsEnumerable();
        
        if (options?.SessionId != null)
        {
            query = query.Where(cp => cp.SessionId == options.SessionId);
        }

        var sorted = query
            .OrderByDescending(cp => cp.Timestamp)
            .ToList();

        var start = options?.Offset ?? 0;
        var count = options?.Limit ?? sorted.Count;
        
        return sorted
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
    }

    public Task DeleteAsync(string checkpointId, CancellationToken cancellationToken = default)
    {
        var agentDirs = Directory.GetDirectories(_basePath);
        
        foreach (var agentDir in agentDirs)
        {
            var checkpointsDir = Path.Combine(agentDir, "checkpoints");
            if (!Directory.Exists(checkpointsDir)) continue;
            
            var filePath = Path.Combine(checkpointsDir, $"{checkpointId}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                break;
            }
        }
        
        return Task.CompletedTask;
    }

    public async Task<string> ForkAsync(string checkpointId, string newAgentId, CancellationToken cancellationToken = default)
    {
        var original = await LoadAsync(checkpointId, cancellationToken);
        if (original == null)
        {
            throw new KeyNotFoundException($"Checkpoint not found: {checkpointId}");
        }

        var forkedId = $"{newAgentId}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var forked = original with
        {
            Id = forkedId,
            AgentId = newAgentId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Metadata = original.Metadata with
            {
                ParentCheckpointId = checkpointId,
                IsForkPoint = true
            }
        };

        return await SaveAsync(forked, cancellationToken);
    }
}
