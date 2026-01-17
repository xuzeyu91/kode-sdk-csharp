using Kode.Agent.Sdk.Core.Skills;

namespace Kode.Agent.Sdk.Core.Abstractions;

/// <summary>
/// Optional capability interface for agents that support Skills (aligned with TS Agent.getSkillsManager/activateSkill).
/// </summary>
public interface ISkillsAwareAgent
{
    SkillsManager? SkillsManager { get; }
    Task<Skill> ActivateSkillAsync(string name, CancellationToken cancellationToken = default);
}

