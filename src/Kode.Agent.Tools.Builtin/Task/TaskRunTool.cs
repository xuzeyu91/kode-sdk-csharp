using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Tools;

namespace Kode.Agent.Tools.Builtin.Task;

/// <summary>
/// Agent template for task delegation.
/// </summary>
public record AgentTemplate
{
    /// <summary>
    /// Unique identifier for the template.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// System prompt for the sub-agent.
    /// </summary>
    public string? System { get; init; }

    /// <summary>
    /// List of tool names available to the sub-agent.
    /// </summary>
    public IReadOnlyList<string>? Tools { get; init; }

    /// <summary>
    /// Description of when to use this template.
    /// </summary>
    public string? WhenToUse { get; init; }
}

/// <summary>
/// Factory for creating task_run tool with specific templates.
/// </summary>
public static class TaskRunToolFactory
{
    /// <summary>
    /// Creates a task_run tool configured with the given templates.
    /// </summary>
    public static TaskRunTool Create(IReadOnlyList<AgentTemplate> templates)
    {
        if (templates.Count == 0)
            throw new ArgumentException("Cannot create task_run tool: no agent templates provided");

        return new TaskRunTool(templates);
    }
}

/// <summary>
/// Tool for delegating tasks to sub-agents.
/// </summary>
[Tool("task_run")]
public sealed class TaskRunTool : ToolBase<TaskRunArgs>
{
    private readonly IReadOnlyList<AgentTemplate> _templates;

    public TaskRunTool(IReadOnlyList<AgentTemplate> templates)
    {
        _templates = templates;
    }

    public override string Name => "task_run";

    public override string Description =>
        "Delegate a task to a specialized sub-agent. " +
        "The sub-agent will execute the task autonomously and return results.";

    public override object InputSchema => JsonSchemaBuilder.BuildSchema<TaskRunArgs>();

    public override ToolAttributes Attributes => new()
    {
        ReadOnly = false,
        RequiresApproval = false
    };

    public override ValueTask<string?> GetPromptAsync(ToolContext context)
    {
        var templateList = string.Join("\n",
            _templates.Select(t => $"  - {t.Id}: {t.WhenToUse ?? "General purpose agent"}"));

        return ValueTask.FromResult<string?>(
            $"Use task_run to delegate complex or specialized tasks to sub-agents.\n\n" +
            $"Available agent templates:\n{templateList}\n\n" +
            "Provide a clear description and detailed prompt for the sub-agent.");
    }

    protected override async Task<ToolResult> ExecuteAsync(
        TaskRunArgs args,
        ToolContext context,
        CancellationToken cancellationToken)
    {
        var template = _templates.FirstOrDefault(t => t.Id == args.AgentTemplateId);

        if (template == null)
        {
            var availableTemplates = string.Join("\n",
                _templates.Select(t => $"  - {t.Id}: {t.WhenToUse ?? "General purpose agent"}"));

            return ToolResult.Fail(
                $"Agent template '{args.AgentTemplateId}' not found.\n\n" +
                $"Available templates:\n{availableTemplates}\n\n" +
                "Please choose one of the available template IDs.");
        }

        // Build detailed prompt
        var detailedPrompt = $"# Task: {args.Description}\n\n{args.Prompt}";
        if (!string.IsNullOrEmpty(args.Context))
        {
            detailedPrompt += $"\n\n# Additional Context\n{args.Context}";
        }

        // Check if agent supports task delegation
        if (context.Agent is not ITaskDelegatorAgent delegator)
        {
            return ToolResult.Fail("Task delegation not supported by this agent version");
        }

        try
        {
            var request = new DelegateTaskRequest
            {
                TemplateId = template.Id,
                Prompt = detailedPrompt,
                Tools = template.Tools,
                CallId = context.CallId
            };

            var result = await delegator.DelegateTaskAsync(request, cancellationToken);

            return ToolResult.Ok(new
            {
                status = result.Status,
                template = template.Id,
                text = result.Text,
                permissionIds = result.PermissionIds,
                agentId = result.AgentId
            });
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Task delegation failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Arguments for task_run tool.
/// </summary>
[GenerateToolSchema]
public class TaskRunArgs
{
    /// <summary>
    /// Short description of the task (3-5 words).
    /// </summary>
    [ToolParameter(Description = "Short description of the task (3-5 words)")]
    public required string Description { get; init; }

    /// <summary>
    /// Detailed instructions for the sub-agent.
    /// </summary>
    [ToolParameter(Description = "Detailed instructions for the sub-agent")]
    public required string Prompt { get; init; }

    /// <summary>
    /// Agent template ID to use for this task.
    /// </summary>
    [ToolParameter(Description = "Agent template ID to use for this task")]
    public required string AgentTemplateId { get; init; }

    /// <summary>
    /// Additional context to append.
    /// </summary>
    [ToolParameter(Description = "Additional context to append", Required = false)]
    public string? Context { get; init; }
}
