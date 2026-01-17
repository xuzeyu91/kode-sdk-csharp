using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Kode.Agent.Sdk.Core.Pool;

/// <summary>
/// Options for creating an agent pool.
/// </summary>
public record AgentPoolOptions
{
    /// <summary>
    /// Dependencies for creating agents.
    /// </summary>
    public required AgentDependencies Dependencies { get; init; }
    
    /// <summary>
    /// Maximum number of agents in the pool.
    /// </summary>
    public int MaxAgents { get; init; } = 50;
}

/// <summary>
/// Pool for managing multiple agent instances.
/// </summary>
public class AgentPool : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, IAgent> _agents = new();
    private readonly AgentDependencies _deps;
    private readonly int _maxAgents;
    private readonly ILogger<AgentPool>? _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public AgentPool(AgentPoolOptions options, ILogger<AgentPool>? logger = null)
    {
        _deps = options.Dependencies;
        _maxAgents = options.MaxAgents;
        _logger = logger;
    }

    /// <summary>
    /// Create a new agent in the pool.
    /// </summary>
    public async Task<IAgent> CreateAsync(
        string agentId, 
        AgentConfig config,
        CancellationToken cancellationToken = default)
    {
        if (_agents.ContainsKey(agentId))
        {
            throw new InvalidOperationException($"Agent already exists: {agentId}");
        }

        if (_agents.Count >= _maxAgents)
        {
            throw new InvalidOperationException($"Pool is full (max {_maxAgents} agents)");
        }

        var agent = await Agent.Agent.CreateAsync(agentId, config, _deps, cancellationToken);
        
        if (!_agents.TryAdd(agentId, agent))
        {
            await agent.DisposeAsync();
            throw new InvalidOperationException($"Agent already exists: {agentId}");
        }

        _logger?.LogInformation("Created agent {AgentId} in pool", agentId);
        return agent;
    }

    /// <summary>
    /// Get an agent by ID.
    /// </summary>
    public IAgent? Get(string agentId)
    {
        _agents.TryGetValue(agentId, out var agent);
        return agent;
    }

    /// <summary>
    /// List all agent IDs in the pool.
    /// </summary>
    public IReadOnlyList<string> List(string? prefix = null)
    {
        var ids = _agents.Keys.ToList();
        if (prefix != null)
        {
            ids = ids.Where(id => id.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        }
        return ids;
    }

    /// <summary>
    /// Get the status of an agent.
    /// </summary>
    public AgentRuntimeState? Status(string agentId)
    {
        if (_agents.TryGetValue(agentId, out var agent))
        {
            return agent.RuntimeState;
        }
        return null;
    }

    /// <summary>
    /// Resume an agent from storage.
    /// </summary>
    public async Task<IAgent> ResumeAsync(
        string agentId,
        AgentConfig config,
        ResumeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Check if already in pool
        if (_agents.TryGetValue(agentId, out var existing))
        {
            return existing;
        }

        // 2. Check pool capacity
        if (_agents.Count >= _maxAgents)
        {
            throw new InvalidOperationException($"Pool is full (max {_maxAgents} agents)");
        }

        // 3. Verify session exists
        var exists = await _deps.Store.ExistsAsync(agentId, cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException($"Agent not found in store: {agentId}");
        }

        // 4. Use Agent.ResumeFromStoreAsync() to restore
        var agent = await Agent.Agent.ResumeFromStoreAsync(agentId, config, _deps, options, cancellationToken);

        // 5. Add to pool
        if (!_agents.TryAdd(agentId, agent))
        {
            await agent.DisposeAsync();
            // Another thread added the agent first, return that one
            return _agents[agentId];
        }

        _logger?.LogInformation("Resumed agent {AgentId} in pool", agentId);
        return agent;
    }

    /// <summary>
    /// Resume all agents from storage.
    /// </summary>
    public async Task<IReadOnlyList<IAgent>> ResumeAllAsync(
        Func<string, AgentConfig> configFactory,
        ResumeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var agentIds = await _deps.Store.ListAsync(cancellationToken);
        var resumed = new List<IAgent>();

        foreach (var agentId in agentIds)
        {
            if (_agents.Count >= _maxAgents) break;
            if (_agents.ContainsKey(agentId)) continue;

            try
            {
                var config = configFactory(agentId);
                var agent = await ResumeAsync(agentId, config, options, cancellationToken);
                resumed.Add(agent);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to resume agent {AgentId}", agentId);
            }
        }

        return resumed;
    }

    /// <summary>
    /// Fork an existing agent.
    /// </summary>
    public async Task<IAgent> ForkAsync(
        string agentId,
        string? newAgentId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_agents.TryGetValue(agentId, out var agent))
        {
            throw new KeyNotFoundException($"Agent not found: {agentId}");
        }

        if (_agents.Count >= _maxAgents)
        {
            throw new InvalidOperationException($"Pool is full (max {_maxAgents} agents)");
        }

        var targetId = newAgentId ?? $"{agentId}_fork_{Guid.NewGuid():N}";
        var forkedAgent = await agent.ForkAsync(targetId, cancellationToken);
        
        if (!_agents.TryAdd(forkedAgent.Id, forkedAgent))
        {
            await forkedAgent.DisposeAsync();
            throw new InvalidOperationException($"Agent already exists: {forkedAgent.Id}");
        }

        _logger?.LogInformation("Forked agent {SourceId} to {TargetId}", agentId, forkedAgent.Id);
        return forkedAgent;
    }

    /// <summary>
    /// Delete an agent from the pool and storage.
    /// </summary>
    public async Task DeleteAsync(string agentId, CancellationToken cancellationToken = default)
    {
        if (_agents.TryRemove(agentId, out var agent))
        {
            await agent.DisposeAsync();
        }

        await _deps.Store.DeleteAsync(agentId, cancellationToken);
        _logger?.LogInformation("Deleted agent {AgentId}", agentId);
    }

    /// <summary>
    /// Remove an agent from the pool without deleting from storage.
    /// </summary>
    public async Task<bool> RemoveAsync(string agentId)
    {
        if (_agents.TryRemove(agentId, out var agent))
        {
            await agent.DisposeAsync();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Get the number of agents in the pool.
    /// </summary>
    public int Count => _agents.Count;

    /// <summary>
    /// Check if the pool contains an agent.
    /// </summary>
    public bool Contains(string agentId) => _agents.ContainsKey(agentId);

    public async ValueTask DisposeAsync()
    {
        foreach (var agent in _agents.Values)
        {
            await agent.DisposeAsync();
        }
        _agents.Clear();
        _lock.Dispose();
    }
}
