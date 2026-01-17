using Microsoft.Extensions.Logging;

namespace Kode.Agent.Sdk.Core.Skills;

/// <summary>
/// Manages skill discovery, activation, and state.
/// </summary>
public class SkillsManager
{
    private readonly Dictionary<string, Skill> _discovered = new();
    private readonly Dictionary<string, SkillActivation> _activated = new();
    private readonly SkillsLoader _loader;
    private readonly SkillsConfig _config;
    private readonly IAgentStore? _store;
    private readonly string? _agentId;
    private readonly ILogger<SkillsManager>? _logger;
    private long _lastDiscoveryAt;

    public SkillsManager(
        SkillsConfig config,
        ISandbox sandbox,
        IAgentStore? store = null,
        string? agentId = null,
        ILogger<SkillsManager>? logger = null)
    {
        _config = config;
        _store = store;
        _agentId = agentId;
        _logger = logger;
        _loader = new SkillsLoader(sandbox, logger != null ? 
            Microsoft.Extensions.Logging.LoggerFactoryExtensions.CreateLogger<SkillsLoader>(
                new LoggerFactory(new[] { new LoggerProvider(logger) })) : null);
    }

    /// <summary>
    /// Discover all skills (metadata only).
    /// Progressive Disclosure Phase 1.
    /// </summary>
    public async Task<IReadOnlyList<SkillMetadata>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var skills = await _loader.DiscoverAsync(_config, cancellationToken);

        foreach (var skill in skills)
        {
            _discovered[skill.Name] = skill;
        }

        _lastDiscoveryAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _logger?.LogInformation("Discovered {Count} skills", skills.Count);
        return skills.Select(s => (SkillMetadata)s).ToList();
    }

    /// <summary>
    /// Activate a skill (load full content).
    /// Progressive Disclosure Phase 2.
    /// </summary>
    public async Task<Skill> ActivateAsync(
        string name,
        SkillActivationSource activatedBy = SkillActivationSource.Agent,
        CancellationToken cancellationToken = default)
    {
        if (!_discovered.TryGetValue(name, out var skill))
        {
            var available = string.Join(", ", _discovered.Keys);
            throw new KeyNotFoundException($"Skill not found: {name}. Available skills: {available}");
        }

        // Load full content if not already loaded
        if (skill.Body == null)
        {
            var fullSkill = await _loader.LoadFullAsync(skill.Path, cancellationToken);
            skill = fullSkill;
            _discovered[name] = skill;
        }

        // Record activation
        skill.ActivatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var activation = new SkillActivation
        {
            Name = name,
            ActivatedAt = skill.ActivatedAt.Value,
            ActivatedBy = activatedBy,
            ToolsGranted = skill.AllowedTools
        };
        _activated[name] = activation;

        // Persist state
        await PersistStateAsync(cancellationToken);

        _logger?.LogDebug("Activated skill: {Name}", name);
        return skill;
    }

    /// <summary>
    /// Deactivate a skill.
    /// </summary>
    public async Task DeactivateAsync(string name, CancellationToken cancellationToken = default)
    {
        _activated.Remove(name);
        
        if (_discovered.TryGetValue(name, out var skill))
        {
            skill.ActivatedAt = null;
        }

        await PersistStateAsync(cancellationToken);
        _logger?.LogInformation("Deactivated skill: {Name}", name);
    }

    /// <summary>
    /// Auto-activate multiple skills.
    /// Silently skips missing skills.
    /// </summary>
    public async Task<IReadOnlyList<Skill>> AutoActivateAsync(
        IEnumerable<string> names,
        CancellationToken cancellationToken = default)
    {
        var activated = new List<Skill>();

        foreach (var name in names)
        {
            if (!_discovered.ContainsKey(name))
            {
                _logger?.LogWarning("Skill '{Name}' not found for auto-activation, skipping", name);
                continue;
            }

            if (IsActivated(name))
            {
                if (_discovered.TryGetValue(name, out var skill))
                {
                    activated.Add(skill);
                }
                continue;
            }

            try
            {
                var skill = await ActivateAsync(name, SkillActivationSource.Auto, cancellationToken);
                activated.Add(skill);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to auto-activate skill '{Name}'", name);
            }
        }

        return activated;
    }

    /// <summary>
    /// List all discovered skills.
    /// </summary>
    public IReadOnlyList<Skill> List() => _discovered.Values.ToList();

    /// <summary>
    /// Get a skill by name.
    /// </summary>
    public Skill? Get(string name)
    {
        _discovered.TryGetValue(name, out var skill);
        return skill;
    }

    /// <summary>
    /// Check if a skill is activated.
    /// </summary>
    public bool IsActivated(string name) => _activated.ContainsKey(name);

    /// <summary>
    /// Get all activated skills.
    /// </summary>
    public IReadOnlyList<Skill> GetActivated()
    {
        return _activated.Keys
            .Select(name => _discovered.GetValueOrDefault(name))
            .Where(s => s != null)
            .ToList()!;
    }

    /// <summary>
    /// Get all skill activations.
    /// </summary>
    public IReadOnlyList<SkillActivation> GetActivations() => _activated.Values.ToList();

    /// <summary>
    /// Get all tools granted by activated skills.
    /// </summary>
    public IReadOnlySet<string> GetGrantedTools()
    {
        var tools = new HashSet<string>();
        
        foreach (var activation in _activated.Values)
        {
            if (activation.ToolsGranted != null)
            {
                foreach (var tool in activation.ToolsGranted)
                {
                    tools.Add(tool);
                }
            }
        }

        return tools;
    }

    /// <summary>
    /// Check if a skill is trusted (can execute scripts).
    /// </summary>
    public bool IsTrusted(string name)
    {
        return _config.Trusted?.Contains(name) ?? false;
    }

    /// <summary>
    /// Load a resource file from an activated skill.
    /// </summary>
    /// <returns>The content of the resource, or null if not found.</returns>
    public async Task<string?> LoadResourceAsync(
        string skillName,
        string resourcePath,
        CancellationToken cancellationToken = default)
    {
        if (!_discovered.TryGetValue(skillName, out var skill))
        {
            throw new KeyNotFoundException($"Skill not found: {skillName}");
        }

        if (!IsActivated(skillName))
        {
            throw new InvalidOperationException($"Skill '{skillName}' is not activated");
        }

        return await _loader.LoadResourceAsync(skill.Path, resourcePath, cancellationToken);
    }

    public async Task RestoreStateAsync(CancellationToken cancellationToken = default)
    {
        if (_store == null || _agentId == null) return;

        var state = await _store.LoadSkillsStateAsync(_agentId, cancellationToken);
        if (state == null) return;

        _activated.Clear();
        foreach (var activation in state.Activated)
        {
            _activated[activation.Name] = activation;
        }

        _lastDiscoveryAt = state.LastDiscoveryAt;
    }

    private async Task PersistStateAsync(CancellationToken cancellationToken)
    {
        if (_store == null || _agentId == null) return;

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var state = new SkillsState
        {
            Discovered = _discovered.Keys.ToList(),
            Activated = _activated.Values.ToList(),
            LastDiscoveryAt = _lastDiscoveryAt > 0 ? _lastDiscoveryAt : now
        };

        await _store.SaveSkillsStateAsync(_agentId, state, cancellationToken);
    }

    // Simple logger provider for creating typed logger
    private class LoggerProvider : ILoggerProvider
    {
        private readonly ILogger _logger;
        public LoggerProvider(ILogger logger) => _logger = logger;
        public ILogger CreateLogger(string categoryName) => _logger;
        public void Dispose() { }
    }
}
