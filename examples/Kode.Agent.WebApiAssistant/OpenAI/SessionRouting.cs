namespace Kode.Agent.WebApiAssistant.OpenAI;

/// <summary>
/// Session routing helpers for OpenAI-compatible endpoints.
/// Ported from TypeScript openai.ts implementation.
/// </summary>
public static class SessionRouting
{
    /// <summary>
    /// Reserved values that indicate "auto session selection" (not bound to a specific session).
    /// </summary>
    private static readonly HashSet<string> ReservedAutoValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "auto", "_", "default"
    };

    /// <summary>
    /// Normalize a session ID value.
    /// Returns null for empty, whitespace-only, or reserved auto-selection values.
    /// </summary>
    public static string? NormalizeSessionId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        // Reserved values indicate "auto session selection"
        if (ReservedAutoValues.Contains(trimmed))
        {
            return null;
        }

        return trimmed;
    }

    /// <summary>
    /// Normalize a thread key value (OpenAI user field).
    /// Truncates to 256 characters maximum.
    /// </summary>
    public static string? NormalizeThreadKey(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > 256 ? trimmed[..256] : trimmed;
    }

    /// <summary>
    /// Determine if the messages array looks like a new conversation's first turn.
    /// A new conversation typically has:
    /// - Zero or more system messages
    /// - Exactly one user message at the end
    /// - No assistant or tool messages
    /// </summary>
    public static bool IsLikelyNewConversationFromMessages(IReadOnlyList<OpenAiChatMessage> messages)
    {
        if (messages.Count == 0)
        {
            return false;
        }

        var userCount = 0;
        foreach (var m in messages)
        {
            var role = m.Role?.ToLowerInvariant();
            if (role == "system")
            {
                continue;
            }

            if (role == "user")
            {
                userCount++;
                continue;
            }

            // Any assistant or tool message means this is not a new conversation
            return false;
        }

        if (userCount != 1)
        {
            return false;
        }

        // Check that the last message is from the user
        var last = messages[^1];
        return string.Equals(last.Role, "user", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Classify the request based on its messages to determine routing behavior.
    /// </summary>
    public static AutoRequestClassification ClassifyAutoRequestFromMessages(IReadOnlyList<OpenAiChatMessage> messages)
    {
        if (messages.Count == 0)
        {
            return new AutoRequestClassification
            {
                Mode = AutoRequestMode.Unknown,
                IsNewConversation = false
            };
        }

        var isNewConversation = IsLikelyNewConversationFromMessages(messages);
        if (isNewConversation)
        {
            return new AutoRequestClassification
            {
                Mode = AutoRequestMode.Single,
                IsNewConversation = true
            };
        }

        // Count roles to determine if this is a history-mode request
        var userCount = 0;
        foreach (var m in messages)
        {
            var role = m.Role?.ToLowerInvariant();
            if (role == "user")
            {
                userCount++;
            }

            // If there's any assistant or tool message, it's a history request
            if (role == "assistant" || role == "tool")
            {
                return new AutoRequestClassification
                {
                    Mode = AutoRequestMode.History,
                    IsNewConversation = false
                };
            }
        }

        // Multiple user messages without assistant responses also indicates history
        if (userCount >= 2)
        {
            return new AutoRequestClassification
            {
                Mode = AutoRequestMode.History,
                IsNewConversation = false
            };
        }

        return new AutoRequestClassification
        {
            Mode = AutoRequestMode.Unknown,
            IsNewConversation = false
        };
    }

    /// <summary>
    /// Get session ID from HTTP request (path parameter + headers).
    /// </summary>
    public static string? GetSessionIdFromRequest(HttpContext httpContext)
    {
        // First, try path parameter
        if (httpContext.Request.RouteValues.TryGetValue("sessionId", out var pathValue))
        {
            var fromPath = NormalizeSessionId(pathValue?.ToString());
            if (fromPath != null)
            {
                return fromPath;
            }
        }

        // Then, try X-Session-Id header
        if (httpContext.Request.Headers.TryGetValue("X-Session-Id", out var sessionIdHeader))
        {
            var fromHeader = NormalizeSessionId(sessionIdHeader.ToString());
            if (fromHeader != null)
            {
                return fromHeader;
            }
        }

        // Finally, try X-Kode-Agent-Id header (backward compatibility)
        if (httpContext.Request.Headers.TryGetValue("X-Kode-Agent-Id", out var agentIdHeader))
        {
            var fromAgentIdHeader = NormalizeSessionId(agentIdHeader.ToString());
            if (fromAgentIdHeader != null)
            {
                return fromAgentIdHeader;
            }
        }

        return null;
    }

    /// <summary>
    /// Convert AutoRequestMode enum to string for storage.
    /// </summary>
    public static string ModeToString(AutoRequestMode mode)
    {
        return mode switch
        {
            AutoRequestMode.Single => "single",
            AutoRequestMode.History => "history",
            _ => "unknown"
        };
    }

    /// <summary>
    /// Parse AutoRequestMode from stored string.
    /// </summary>
    public static AutoRequestMode ParseMode(string? modeStr)
    {
        return modeStr?.ToLowerInvariant() switch
        {
            "single" => AutoRequestMode.Single,
            "history" => AutoRequestMode.History,
            _ => AutoRequestMode.Unknown
        };
    }
}
