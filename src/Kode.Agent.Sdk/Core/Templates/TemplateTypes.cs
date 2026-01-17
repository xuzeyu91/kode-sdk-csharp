using System.Text.Json;
using Kode.Agent.Sdk.Core.Hooks;

namespace Kode.Agent.Sdk.Core.Templates;

/// <summary>
/// Permission configuration for agents.
/// </summary>
public record PermissionConfig
{
    /// <summary>
    /// TS-aligned permission mode name (auto | approval | readonly | custom).
    /// </summary>
    public string Mode { get; init; } = "auto";
    
    /// <summary>
     /// Tools that require explicit approval.
     /// </summary>
    public IReadOnlyList<string>? RequireApprovalTools { get; init; }
    
    /// <summary>
    /// Tools that are always allowed.
    /// </summary>
    public IReadOnlyList<string>? AllowTools { get; init; }
    
    /// <summary>
    /// Tools that are always denied.
    /// </summary>
    public IReadOnlyList<string>? DenyTools { get; init; }
    
    /// <summary>
     /// Additional metadata.
     /// </summary>
    public IReadOnlyDictionary<string, JsonElement>? Metadata { get; init; }
}

/// <summary>
/// Sub-agent configuration.
/// </summary>
public record SubAgentConfig
{
    /// <summary>
    /// Allowed template IDs for sub-agents.
    /// </summary>
    public IReadOnlyList<string>? Templates { get; init; }
    
    /// <summary>
    /// Maximum depth of sub-agent nesting.
    /// </summary>
    public int Depth { get; init; } = 1;
    
    /// <summary>
    /// Whether sub-agents inherit parent configuration.
    /// </summary>
    public bool InheritConfig { get; init; } = true;
    
    /// <summary>
    /// Configuration overrides for sub-agents.
    /// </summary>
    public SubAgentOverrides? Overrides { get; init; }
}

/// <summary>
/// Sub-agent configuration overrides.
/// </summary>
public record SubAgentOverrides
{
    public PermissionConfig? Permission { get; init; }
    public TodoConfig? Todo { get; init; }
}

/// <summary>
/// Todo feature configuration.
/// </summary>
public record TodoConfig
{
    /// <summary>
    /// Whether todo feature is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;
    
    /// <summary>
    /// Interval in steps between reminders.
    /// </summary>
    public int? RemindIntervalSteps { get; init; }
    
    /// <summary>
    /// Storage path for todo items.
    /// </summary>
    public string? StoragePath { get; init; }
    
    /// <summary>
    /// Whether to show reminder on start.
    /// </summary>
    public bool ReminderOnStart { get; init; } = true;
}

/// <summary>
/// Skills auto-activation configuration.
/// </summary>
public record TemplateSkillsConfig
{
    /// <summary>
    /// Skills to auto-activate on startup.
    /// </summary>
    public IReadOnlyList<string>? AutoActivate { get; init; }
    
    /// <summary>
    /// Recommended skills to show in system prompt.
    /// </summary>
    public IReadOnlyList<string>? Recommend { get; init; }
}

/// <summary>
/// Template runtime configuration.
/// </summary>
public record TemplateRuntimeConfig
{
    /// <summary>
    /// Whether to expose thinking/reasoning content.
    /// </summary>
    public bool ExposeThinking { get; init; }
    
    /// <summary>
    /// Todo feature configuration.
    /// </summary>
    public TodoConfig? Todo { get; init; }
    
    /// <summary>
    /// Sub-agent configuration.
    /// </summary>
    public SubAgentConfig? SubAgents { get; init; }
    
    /// <summary>
    /// Skills configuration.
    /// </summary>
    public TemplateSkillsConfig? Skills { get; init; }
    
    /// <summary>
    /// Additional metadata.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement>? Metadata { get; init; }
}

/// <summary>
/// Agent template definition.
/// </summary>
public record AgentTemplateDefinition
{
    /// <summary>
    /// Unique template identifier.
    /// </summary>
    public required string Id { get; init; }
    
    /// <summary>
    /// Human-readable template name.
    /// </summary>
    public string? Name { get; init; }
    
    /// <summary>
    /// Template description.
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Template version.
    /// </summary>
    public string? Version { get; init; }
    
    /// <summary>
    /// System prompt for the agent.
    /// </summary>
    public required string SystemPrompt { get; init; }
    
    /// <summary>
    /// Default model to use.
    /// </summary>
    public string? Model { get; init; }
    
    /// <summary>
    /// Sandbox configuration.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement>? Sandbox { get; init; }
    
    /// <summary>
    /// Allowed tools. Use "*" for all tools, or a list of tool names.
    /// </summary>
    public ToolsConfig Tools { get; init; } = ToolsConfig.All();
    
    /// <summary>
    /// Permission configuration.
    /// </summary>
    public PermissionConfig? Permission { get; init; }
    
    /// <summary>
    /// Runtime configuration.
    /// </summary>
    public TemplateRuntimeConfig? Runtime { get; init; }
    
    /// <summary>
    /// Hooks for this template.
    /// </summary>
    public IHooks? Hooks { get; init; }
    
    /// <summary>
    /// Additional metadata.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement>? Metadata { get; init; }
}

/// <summary>
/// Tools configuration - either all tools or a specific list.
/// </summary>
public record ToolsConfig
{
    /// <summary>
    /// Whether all tools are allowed.
    /// </summary>
    public bool AllowAll { get; init; }
    
    /// <summary>
    /// Specific list of allowed tools.
    /// </summary>
    public IReadOnlyList<string>? AllowedTools { get; init; }
    
    /// <summary>
    /// Create a config that allows all tools.
    /// </summary>
    public static ToolsConfig All() => new() { AllowAll = true };
    
    /// <summary>
    /// Create a config that allows specific tools.
    /// </summary>
    public static ToolsConfig Specific(params string[] tools) => 
        new() { AllowAll = false, AllowedTools = tools };
    
    /// <summary>
    /// Create a config that allows specific tools.
    /// </summary>
    public static ToolsConfig Specific(IEnumerable<string> tools) => 
        new() { AllowAll = false, AllowedTools = tools.ToList() };
}
