using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Tools;

namespace Kode.Agent.Tools.Builtin.Skills;

/// <summary>
/// Tool for activating a skill.
/// </summary>
[Tool("skill_activate")]
public sealed class SkillActivateTool : ToolBase<SkillActivateArgs>
{
    public override string Name => "skill_activate";

    public override string Description =>
        "Activate a skill and load its full instructions into context.";

    public override object InputSchema => JsonSchemaBuilder.BuildSchema<SkillActivateArgs>();

    public override ToolAttributes Attributes => new()
    {
        ReadOnly = false,
        RequiresApproval = false
    };

    public override ValueTask<string?> GetPromptAsync(ToolContext context)
    {
        return ValueTask.FromResult<string?>(
            "### skill_activate\n\n" +
            "激活一个 Skill，将其完整指令加载到上下文中。\n\n" +
            "使用场景：\n" +
            "- 当任务需要特定 Skill 的专业知识时\n" +
            "- 在 skill_list 查看可用 Skills 后选择激活\n" +
            "- 需要遵循特定开发规范或流程时\n\n" +
            "注意：\n" +
            "- 激活后 Skill 的完整指令会注入到上下文\n" +
            "- 如果 Skill 包含 scripts/，可以使用 skill_resource 加载\n" +
            "- 激活会持久化，Resume 时自动恢复");
    }

    protected override async Task<ToolResult> ExecuteAsync(
        SkillActivateArgs args,
        ToolContext context,
        CancellationToken cancellationToken)
    {
        // Check if agent has skills manager
        if (context.Agent is not ISkillsAwareAgent skillsAware)
        {
            return ToolResult.Ok(new
            {
                ok = false,
                error = "Skills not configured for this agent",
                recommendations = new[] { "确认 Agent 配置中启用了 skills" }
            });
        }

        try
        {
            var skill = await skillsAware.ActivateSkillAsync(args.Name, cancellationToken);

            return ToolResult.Ok(new
            {
                ok = true,
                message = $"Skill \"{skill.Name}\" activated",
                description = skill.Description,
                hasScripts = (skill.Resources?.Scripts?.Count ?? 0) > 0,
                hasReferences = (skill.Resources?.References?.Count ?? 0) > 0,
                hasAssets = (skill.Resources?.Assets?.Count ?? 0) > 0,
                resources = skill.Resources
            });
        }
        catch (Exception ex)
        {
            return ToolResult.Ok(new
            {
                ok = false,
                error = ex.Message,
                recommendations = new[]
                {
                    "使用 skill_list 查看可用的 Skills",
                    "确认 Skill 名称拼写正确"
                }
            });
        }
    }
}

/// <summary>
/// Arguments for skill_activate tool.
/// </summary>
[GenerateToolSchema]
public class SkillActivateArgs
{
    /// <summary>
    /// Name of the skill to activate.
    /// </summary>
    [ToolParameter(Description = "Name of the skill to activate")]
    public required string Name { get; init; }
}
