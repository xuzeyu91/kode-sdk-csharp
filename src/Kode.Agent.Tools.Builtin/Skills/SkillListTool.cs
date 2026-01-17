using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Tools;

namespace Kode.Agent.Tools.Builtin.Skills;

/// <summary>
/// Tool for listing available skills.
/// </summary>
[Tool("skill_list")]
public sealed class SkillListTool : ToolBase<SkillListArgs>
{
    public override string Name => "skill_list";

    public override string Description =>
        "List available skills with their names, descriptions, and activation status.";

    public override object InputSchema => JsonSchemaBuilder.BuildSchema<SkillListArgs>();

    public override ToolAttributes Attributes => new()
    {
        ReadOnly = true,
        RequiresApproval = false
    };

    public override ValueTask<string?> GetPromptAsync(ToolContext context)
    {
        return ValueTask.FromResult<string?>(
            "### skill_list\n\n" +
            "列出当前可用的 Skills。返回每个 Skill 的名称、描述和激活状态。\n\n" +
            "使用场景：\n" +
            "- 当需要了解有哪些 Skills 可用时\n" +
            "- 在选择激活哪个 Skill 之前\n" +
            "- 检查 Skill 的激活状态");
    }

    protected override Task<ToolResult> ExecuteAsync(
        SkillListArgs args,
        ToolContext context,
        CancellationToken cancellationToken)
    {
        // Check if agent has skills manager
        if (context.Agent is not ISkillsAwareAgent skillsAware)
        {
            return System.Threading.Tasks.Task.FromResult(ToolResult.Ok(new
            {
                ok = false,
                error = "Skills not configured for this agent",
                recommendations = new[] { "确认 Agent 配置中启用了 skills" }
            }));
        }

        var manager = skillsAware.SkillsManager;
        if (manager == null)
        {
            return System.Threading.Tasks.Task.FromResult(ToolResult.Ok(new
            {
                ok = false,
                error = "Skills manager not available",
                recommendations = new[] { "确认 Agent 配置中启用了 skills" }
            }));
        }

        var skills = manager.List();
        var skillInfos = skills.Select(s => new
        {
            name = s.Name,
            description = s.Description,
            activated = manager.IsActivated(s.Name),
            hasScripts = (s.Resources?.Scripts?.Count ?? 0) > 0,
            hasReferences = (s.Resources?.References?.Count ?? 0) > 0,
            hasAssets = (s.Resources?.Assets?.Count ?? 0) > 0
        }).ToList();

        return System.Threading.Tasks.Task.FromResult(ToolResult.Ok(new
        {
            ok = true,
            skills = skillInfos,
            count = skills.Count,
            activatedCount = skills.Count(s => manager.IsActivated(s.Name))
        }));
    }
}

/// <summary>
/// Arguments for skill_list tool (no parameters required).
/// </summary>
[GenerateToolSchema]
public class SkillListArgs
{
    // No parameters required
}

// Note: ISkillsAwareAgent is defined in Kode.Agent.Sdk.Core.Abstractions.
