namespace Kode.Agent.WebApiAssistant.Assistant;

/// <summary>
/// Utility for discovering skills from the file system.
/// </summary>
public static class SkillsDiscovery
{
    /// <summary>
    /// Discover available skills from the specified paths.
    /// A skill directory is valid if it contains a SKILL.md file.
    /// </summary>
    /// <param name="skillsPaths">Paths to search for skills.</param>
    /// <param name="workDir">Base working directory for resolving relative paths.</param>
    /// <returns>List of recommended skills (no auto-activation).</returns>
    public static IReadOnlyList<string> DiscoverSkills(
        IReadOnlyList<string>? skillsPaths,
        string? workDir = null)
    {
        if (skillsPaths == null || skillsPaths.Count == 0)
        {
            return [];
        }

        var baseDir = workDir ?? Directory.GetCurrentDirectory();
        var skills = new List<string>();

        foreach (var skillsPath in skillsPaths)
        {
            var fullPath = Path.IsPathRooted(skillsPath)
                ? skillsPath
                : Path.Combine(baseDir, skillsPath);

            if (!Directory.Exists(fullPath)) continue;

            foreach (var skillDir in Directory.GetDirectories(fullPath))
            {
                var skillName = Path.GetFileName(skillDir);

                // Check if this is a valid skill directory (contains SKILL.md)
                var skillFile = Path.Combine(skillDir, "SKILL.md");
                if (!File.Exists(skillFile)) continue;

                skills.Add(skillName);
            }
        }

        return skills;
    }
}
