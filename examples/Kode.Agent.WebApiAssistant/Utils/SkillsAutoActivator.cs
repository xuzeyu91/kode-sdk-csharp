using System.Runtime.InteropServices;
using Kode.Agent.Sdk.Core.Abstractions;
using AgentImpl = Kode.Agent.Sdk.Core.Agent.Agent;

namespace Kode.Agent.WebApiAssistant.Utils;

/// <summary>
/// Skills 自动激活器
/// </summary>
public static class SkillsAutoActivator
{
    /// <summary>
    /// 默认自动激活的技能（所有平台）
    /// </summary>
    private static readonly string[] DefaultAutoActivate = new[]
    {
        "memory",
        "knowledge",
        "email"
    };

    /// <summary>
    /// macOS 平台额外激活的技能
    /// </summary>
    private static readonly string[] MacOsAutoActivate = new[]
    {
        "calendar"
    };

    /// <summary>
    /// 获取应该自动激活的技能列表
    /// </summary>
    public static string[] GetAutoActivateSkills()
    {
        var skills = new List<string>(DefaultAutoActivate);

        // macOS 平台额外激活 calendar
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            skills.AddRange(MacOsAutoActivate);
        }

        return skills.ToArray();
    }

    /// <summary>
    /// 自动激活技能
    /// </summary>
    public static async Task ActivateSkillsAsync(
        AgentImpl agent,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var skillsToActivate = GetAutoActivateSkills();

        foreach (var skill in skillsToActivate)
        {
            try
            {
                if (agent is ISkillsAwareAgent skillsAwareAgent)
                {
                    await skillsAwareAgent.ActivateSkillAsync(skill, cancellationToken);
                    logger?.LogDebug("Activated skill: {Skill}", skill);
                }
            }
            catch (Exception ex)
            {
                // 技能激活失败不应阻止 Agent 启动
                logger?.LogWarning(ex, "Failed to activate skill: {Skill}", skill);
            }
        }
    }
}
