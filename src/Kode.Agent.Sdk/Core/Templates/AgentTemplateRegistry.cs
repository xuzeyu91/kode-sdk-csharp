namespace Kode.Agent.Sdk.Core.Templates;

/// <summary>
/// Registry for agent templates.
/// </summary>
public class AgentTemplateRegistry
{
    private readonly Dictionary<string, AgentTemplateDefinition> _templates = new();

    /// <summary>
    /// Register a single template.
    /// </summary>
    public void Register(AgentTemplateDefinition template)
    {
        ArgumentNullException.ThrowIfNull(template);
        
        if (string.IsNullOrWhiteSpace(template.Id))
        {
            throw new ArgumentException("Template id is required", nameof(template));
        }
        
        if (string.IsNullOrWhiteSpace(template.SystemPrompt))
        {
            throw new ArgumentException($"Template {template.Id} must provide a non-empty systemPrompt", nameof(template));
        }
        
        _templates[template.Id] = template;
    }

    /// <summary>
    /// Register multiple templates.
    /// </summary>
    public void BulkRegister(IEnumerable<AgentTemplateDefinition> templates)
    {
        foreach (var template in templates)
        {
            Register(template);
        }
    }

    /// <summary>
    /// Check if a template exists.
    /// </summary>
    public bool Has(string id) => _templates.ContainsKey(id);

    /// <summary>
    /// Get a template by ID.
    /// </summary>
    public AgentTemplateDefinition Get(string id)
    {
        if (!_templates.TryGetValue(id, out var template))
        {
            throw new KeyNotFoundException($"Template not found: {id}");
        }
        return template;
    }

    /// <summary>
    /// Try to get a template by ID.
    /// </summary>
    public bool TryGet(string id, out AgentTemplateDefinition? template)
    {
        return _templates.TryGetValue(id, out template);
    }

    /// <summary>
    /// List all registered templates.
    /// </summary>
    public IReadOnlyList<AgentTemplateDefinition> List() => _templates.Values.ToList();

    /// <summary>
    /// Remove a template.
    /// </summary>
    public bool Remove(string id) => _templates.Remove(id);

    /// <summary>
    /// Clear all templates.
    /// </summary>
    public void Clear() => _templates.Clear();
}
