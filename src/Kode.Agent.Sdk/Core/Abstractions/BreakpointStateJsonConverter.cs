using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kode.Agent.Sdk.Core.Abstractions;

/// <summary>
/// JSON converter for <see cref="BreakpointState"/> that matches TS wire format (UPPER_SNAKE_CASE),
/// while accepting legacy numeric values and common string variants.
/// </summary>
public sealed class BreakpointStateJsonConverter : JsonConverter<BreakpointState>
{
    public override BreakpointState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            var i = reader.GetInt32();
            return (BreakpointState)i;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Unexpected token type for BreakpointState: {reader.TokenType}");
        }

        var raw = reader.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new JsonException("Empty BreakpointState");
        }

        var normalized = Normalize(raw);
        return normalized switch
        {
            "ready" => BreakpointState.Ready,
            "premodel" => BreakpointState.PreModel,
            "streamingmodel" => BreakpointState.StreamingModel,
            "toolpending" => BreakpointState.ToolPending,
            "awaitingapproval" => BreakpointState.AwaitingApproval,
            "pretool" => BreakpointState.PreTool,
            "toolexecuting" => BreakpointState.ToolExecuting,
            "posttool" => BreakpointState.PostTool,
            _ => throw new JsonException($"Unknown BreakpointState: {raw}")
        };
    }

    public override void Write(Utf8JsonWriter writer, BreakpointState value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            BreakpointState.Ready => "READY",
            BreakpointState.PreModel => "PRE_MODEL",
            BreakpointState.StreamingModel => "STREAMING_MODEL",
            BreakpointState.ToolPending => "TOOL_PENDING",
            BreakpointState.AwaitingApproval => "AWAITING_APPROVAL",
            BreakpointState.PreTool => "PRE_TOOL",
            BreakpointState.ToolExecuting => "TOOL_EXECUTING",
            BreakpointState.PostTool => "POST_TOOL",
            _ => value.ToString().ToUpperInvariant()
        });
    }

    private static string Normalize(string value)
    {
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

