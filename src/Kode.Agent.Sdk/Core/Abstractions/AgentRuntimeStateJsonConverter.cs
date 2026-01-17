using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kode.Agent.Sdk.Core.Abstractions;

/// <summary>
/// JSON converter for <see cref="AgentRuntimeState"/> that matches TS wire format (UPPER_SNAKE_CASE),
/// while accepting legacy numeric values and common string variants.
/// </summary>
public sealed class AgentRuntimeStateJsonConverter : JsonConverter<AgentRuntimeState>
{
    public override AgentRuntimeState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            var i = reader.GetInt32();
            return (AgentRuntimeState)i;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Unexpected token type for AgentRuntimeState: {reader.TokenType}");
        }

        var raw = reader.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new JsonException("Empty AgentRuntimeState");
        }

        var normalized = Normalize(raw);
        return normalized switch
        {
            "ready" => AgentRuntimeState.Ready,
            "working" => AgentRuntimeState.Working,
            "paused" => AgentRuntimeState.Paused,
            _ => throw new JsonException($"Unknown AgentRuntimeState: {raw}")
        };
    }

    public override void Write(Utf8JsonWriter writer, AgentRuntimeState value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            AgentRuntimeState.Ready => "READY",
            AgentRuntimeState.Working => "WORKING",
            AgentRuntimeState.Paused => "PAUSED",
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

