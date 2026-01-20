using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kode.Agent.WebApiAssistant.OpenAI;

/// <summary>
/// Represents the auto-request mode classification.
/// </summary>
public enum AutoRequestMode
{
    /// <summary>Single message (likely new conversation)</summary>
    Single,
    /// <summary>History mode (multi-turn with previous messages)</summary>
    History,
    /// <summary>Unknown/unclassified</summary>
    Unknown
}

/// <summary>
/// Result of classifying an OpenAI request's messages.
/// </summary>
public sealed record AutoRequestClassification
{
    /// <summary>The detected request mode</summary>
    public required AutoRequestMode Mode { get; init; }
    /// <summary>Whether this appears to be a new conversation</summary>
    public required bool IsNewConversation { get; init; }
}

/// <summary>
/// OpenAI routing state for managing session selection.
/// Mirrors the TypeScript OpenAIRoutingState structure.
/// </summary>
public sealed record OpenAiRoutingState
{
    /// <summary>
    /// Default agent ID for auto-routing mode.
    /// </summary>
    [JsonPropertyName("autoDefaultAgentId")]
    public string? AutoDefaultAgentId { get; init; }

    /// <summary>
    /// Last request mode (single/history/unknown) for auto-routing decisions.
    /// </summary>
    [JsonPropertyName("autoLastRequestMode")]
    public string? AutoLastRequestMode { get; init; }

    /// <summary>
    /// Timestamp of last auto-mode request.
    /// </summary>
    [JsonPropertyName("autoLastRequestAt")]
    public string? AutoLastRequestAt { get; init; }

    /// <summary>
    /// Mapping from threadKey (OpenAI user field) to agentId.
    /// </summary>
    [JsonPropertyName("threadKeyToAgentId")]
    public Dictionary<string, string>? ThreadKeyToAgentId { get; init; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public string? UpdatedAt { get; init; }
}

/// <summary>
/// Manages persistence of OpenAI routing state.
/// </summary>
public sealed class OpenAiRoutingStateManager
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _workDir;
    private readonly ILogger<OpenAiRoutingStateManager>? _logger;

    public OpenAiRoutingStateManager(string workDir, ILogger<OpenAiRoutingStateManager>? logger = null)
    {
        _workDir = workDir;
        _logger = logger;
    }

    /// <summary>
    /// Get the routing state file path for a given user ID.
    /// If userId is null or empty, uses a global routing state.
    /// </summary>
    private string GetRoutingStatePath(string? userId)
    {
        string userDataDir;
        if (string.IsNullOrWhiteSpace(userId))
        {
            userDataDir = Path.Combine(_workDir, "data");
        }
        else
        {
            userDataDir = Path.Combine(_workDir, ".users", userId);
        }

        var configDir = Path.Combine(userDataDir, ".config");
        Directory.CreateDirectory(configDir);
        return Path.Combine(configDir, "openai-routing.json");
    }

    /// <summary>
    /// Load routing state for a user.
    /// </summary>
    public OpenAiRoutingState LoadRoutingState(string? userId)
    {
        var path = GetRoutingStatePath(userId);
        try
        {
            if (!File.Exists(path))
            {
                return new OpenAiRoutingState();
            }

            var json = File.ReadAllText(path);
            var state = JsonSerializer.Deserialize<OpenAiRoutingState>(json, JsonOptions);
            return state ?? new OpenAiRoutingState();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load routing state from {Path}", path);
            return new OpenAiRoutingState();
        }
    }

    /// <summary>
    /// Save routing state for a user.
    /// </summary>
    public void SaveRoutingState(string? userId, OpenAiRoutingState state)
    {
        var path = GetRoutingStatePath(userId);
        try
        {
            var nextState = state with
            {
                UpdatedAt = DateTimeOffset.UtcNow.ToString("O")
            };

            var json = JsonSerializer.Serialize(nextState, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save routing state to {Path}", path);
        }
    }

    /// <summary>
    /// Check if a session directory exists.
    /// </summary>
    public bool SessionExists(string storeDir, string agentId)
    {
        var agentDir = Path.Combine(storeDir, agentId);
        return Directory.Exists(agentDir);
    }
}
