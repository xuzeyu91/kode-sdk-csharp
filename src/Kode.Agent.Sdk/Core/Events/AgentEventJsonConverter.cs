using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kode.Agent.Sdk.Core.Events;

/// <summary>
/// Polymorphic JSON converter for AgentEvent based on the "type" field (aligned with TS event envelopes).
/// </summary>
public sealed class AgentEventJsonConverter : JsonConverter<AgentEvent>
{
    public override AgentEvent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        if (!doc.RootElement.TryGetProperty("type", out var typeProp) || typeProp.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("Missing event type discriminator field: type");
        }

        var type = typeProp.GetString();
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new JsonException("Empty event type discriminator field: type");
        }

        return type switch
        {
            "text_chunk" => doc.RootElement.Deserialize<TextChunkEvent>(options)!,
            "text_chunk_start" => doc.RootElement.Deserialize<TextChunkStartEvent>(options)!,
            "text_chunk_end" => doc.RootElement.Deserialize<TextChunkEndEvent>(options)!,
            "think_chunk" => doc.RootElement.Deserialize<ThinkChunkEvent>(options)!,
            "think_chunk_start" => doc.RootElement.Deserialize<ThinkChunkStartEvent>(options)!,
            "think_chunk_end" => doc.RootElement.Deserialize<ThinkChunkEndEvent>(options)!,
            "tool:start" => doc.RootElement.Deserialize<ToolStartEvent>(options)!,
            "tool:end" => doc.RootElement.Deserialize<ToolEndEvent>(options)!,
            "tool:error" => doc.RootElement.Deserialize<ToolErrorEvent>(options)!,
            "done" => doc.RootElement.Deserialize<DoneEvent>(options)!,
            "permission_required" => doc.RootElement.Deserialize<PermissionRequiredEvent>(options)!,
            "permission_decided" => doc.RootElement.Deserialize<PermissionDecidedEvent>(options)!,
            "state_changed" => doc.RootElement.Deserialize<StateChangedEvent>(options)!,
            "breakpoint_changed" => doc.RootElement.Deserialize<BreakpointChangedEvent>(options)!,
            "token_usage" => doc.RootElement.Deserialize<TokenUsageEvent>(options)!,
            "todo_changed" => doc.RootElement.Deserialize<TodoChangedEvent>(options)!,
            "todo_reminder" => doc.RootElement.Deserialize<TodoReminderEvent>(options)!,
            "file_changed" => doc.RootElement.Deserialize<FileChangedEvent>(options)!,
            "tool_manual_updated" => doc.RootElement.Deserialize<ToolManualUpdatedEvent>(options)!,
            "tool_custom_event" => doc.RootElement.Deserialize<ToolCustomEvent>(options)!,
            "error" => doc.RootElement.Deserialize<ErrorEvent>(options)!,
            "storage_failure" => doc.RootElement.Deserialize<StorageFailureEvent>(options)!,
            "context_repair" => doc.RootElement.Deserialize<ContextRepairEvent>(options)!,
            "agent_resumed" => doc.RootElement.Deserialize<AgentResumedEvent>(options)!,
            "agent_recovered" => doc.RootElement.Deserialize<AgentRecoveredEvent>(options)!,
            "tool_executed" => doc.RootElement.Deserialize<ToolExecutedEvent>(options)!,
            "context_compression" => doc.RootElement.Deserialize<ContextCompressionEvent>(options)!,
            "scheduler_triggered" => doc.RootElement.Deserialize<SchedulerTriggeredEvent>(options)!,
            "step_complete" => doc.RootElement.Deserialize<StepCompleteEvent>(options)!,
            "reminder_sent" => doc.RootElement.Deserialize<ReminderSentEvent>(options)!,
            "skill_discovered" => doc.RootElement.Deserialize<SkillDiscoveredEvent>(options)!,
            "skill_activated" => doc.RootElement.Deserialize<SkillActivatedEvent>(options)!,
            "skill_deactivated" => doc.RootElement.Deserialize<SkillDeactivatedEvent>(options)!,
            "subagent.created" => doc.RootElement.Deserialize<SubAgentCreatedEvent>(options)!,
            "subagent.delta" => doc.RootElement.Deserialize<SubAgentDeltaEvent>(options)!,
            "subagent.thinking" => doc.RootElement.Deserialize<SubAgentThinkingEvent>(options)!,
            "subagent.tool_start" => doc.RootElement.Deserialize<SubAgentToolStartEvent>(options)!,
            "subagent.tool_end" => doc.RootElement.Deserialize<SubAgentToolEndEvent>(options)!,
            "subagent.permission_required" => doc.RootElement.Deserialize<SubAgentPermissionRequiredEvent>(options)!,
            _ => doc.RootElement.Deserialize<UnknownEvent>(options)!
        };
    }

    public override void Write(Utf8JsonWriter writer, AgentEvent value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        JsonSerializer.Serialize(writer, (object)value, value.GetType(), options);
    }
}
