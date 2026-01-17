using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Tools;

namespace Kode.Agent.Tools.Builtin.Skills;

/// <summary>
/// Tool for loading skill resource files.
/// </summary>
[Tool("skill_resource")]
public sealed class SkillResourceTool : ToolBase<SkillResourceArgs>
{
    public override string Name => "skill_resource";

    public override string Description =>
        "Load a resource file from an activated skill (references, assets).";

    public override object InputSchema => JsonSchemaBuilder.BuildSchema<SkillResourceArgs>();

    public override ToolAttributes Attributes => new()
    {
        ReadOnly = true,
        RequiresApproval = false
    };

    public override ValueTask<string?> GetPromptAsync(ToolContext context)
    {
        return ValueTask.FromResult<string?>(
            "### skill_resource\n\n" +
            "加载已激活 Skill 的资源文件内容（references/、assets/ 目录下的文件）。\n\n" +
            "使用场景：\n" +
            "- 需要读取 Skill 提供的参考文档\n" +
            "- 需要加载 Skill 的模板或资源文件\n" +
            "- 查看 Skill 的示例代码\n\n" +
            "注意：\n" +
            "- 只能加载已激活 Skill 的资源\n" +
            "- resourcePath 是相对于 Skill 目录的路径\n" +
            "- 支持 references/、assets/ 目录下的文件");
    }

    protected override async Task<ToolResult> ExecuteAsync(
        SkillResourceArgs args,
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

        var manager = skillsAware.SkillsManager;
        if (manager == null)
        {
            return ToolResult.Ok(new
            {
                ok = false,
                error = "Skills manager not available"
            });
        }

        var skill = manager.Get(args.SkillName);
        if (skill == null)
        {
            return ToolResult.Ok(new
            {
                ok = false,
                error = $"Skill not found: {args.SkillName}",
                recommendations = new[]
                {
                    "使用 skill_list 查看可用的 Skills",
                    "确认 Skill 名称拼写正确"
                }
            });
        }

        if (!manager.IsActivated(args.SkillName))
        {
            return ToolResult.Ok(new
            {
                ok = false,
                error = $"Skill \"{args.SkillName}\" is not activated",
                recommendations = new[]
                {
                    $"使用 skill_activate 激活 \"{args.SkillName}\""
                }
            });
        }

        try
        {
            var content = await manager.LoadResourceAsync(args.SkillName, args.ResourcePath, cancellationToken);

            return ToolResult.Ok(new
            {
                ok = true,
                skillName = args.SkillName,
                resourcePath = args.ResourcePath,
                content
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
                    "检查资源路径是否正确",
                    "确认文件存在于 Skill 目录中",
                    $"可用资源: {System.Text.Json.JsonSerializer.Serialize(skill.Resources ?? new())}"
                }
            });
        }
    }
}

/// <summary>
/// Arguments for skill_resource tool.
/// </summary>
[GenerateToolSchema]
public class SkillResourceArgs
{
    /// <summary>
    /// Name of the skill.
    /// </summary>
    [ToolParameter(Description = "Name of the skill")]
    public required string SkillName { get; init; }

    /// <summary>
    /// Path relative to skill directory.
    /// </summary>
    [ToolParameter(Description = "Path relative to skill directory (e.g., \"references/guide.md\")")]
    public required string ResourcePath { get; init; }
}
