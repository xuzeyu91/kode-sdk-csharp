using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kode.Agent.Sdk.Core.Abstractions;

/// <summary>
/// JSON converter for <see cref="ToolCallState"/> that matches TS wire format (UPPER_SNAKE_CASE),
/// while accepting legacy numeric values and common string variants.
/// </summary>
public sealed class ToolCallStateJsonConverter : JsonConverter<ToolCallState>
{
    public override ToolCallState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            var i = reader.GetInt32();
            return (ToolCallState)i;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Unexpected token type for ToolCallState: {reader.TokenType}");
        }

        var raw = reader.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new JsonException("Empty ToolCallState");
        }

        var normalized = Normalize(raw);
        return normalized switch
        {
            "pending" => ToolCallState.Pending,
            "approvalrequired" => ToolCallState.ApprovalRequired,
            "approved" => ToolCallState.Approved,
            "executing" => ToolCallState.Executing,
            "completed" => ToolCallState.Completed,
            "failed" => ToolCallState.Failed,
            "denied" => ToolCallState.Denied,
            "sealed" => ToolCallState.Sealed,
            _ => throw new JsonException($"Unknown ToolCallState: {raw}")
        };
    }

    public override void Write(Utf8JsonWriter writer, ToolCallState value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            ToolCallState.Pending => "PENDING",
            ToolCallState.ApprovalRequired => "APPROVAL_REQUIRED",
            ToolCallState.Approved => "APPROVED",
            ToolCallState.Executing => "EXECUTING",
            ToolCallState.Completed => "COMPLETED",
            ToolCallState.Failed => "FAILED",
            ToolCallState.Denied => "DENIED",
            ToolCallState.Sealed => "SEALED",
            _ => value.ToString().ToUpperInvariant()
        });
    }

    private static string Normalize(string value)
    {
        // Accept: PENDING, pending, Pending, approval_required, approvalRequired, etc.
        Span<char> buffer = stackalloc char[value.Length];
        var idx = 0;
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer[idx++] = char.ToLowerInvariant(ch);
            }
        }
        return new string(buffer[..idx]);
    }
}

