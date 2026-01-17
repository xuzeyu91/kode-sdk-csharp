using System.Text;

namespace Kode.Agent.Sdk.Core.Skills;

/// <summary>
/// Skills system prompt / reminder injector (aligned with TS src/skills/injector.ts).
/// </summary>
public static class SkillsInjector
{
    /// <summary>
    /// Generates an <available_skills/> XML snippet for system prompt injection.
    /// </summary>
    public static string ToPromptXml(IEnumerable<SkillMetadata> skills)
    {
        var list = skills.ToList();
        if (list.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("<available_skills>");
        foreach (var skill in list)
        {
            var location = skill is Skill full ? $"{full.Path}/SKILL.md" : null;
            sb.AppendLine("  <skill>");
            sb.AppendLine($"    <name>{EscapeXml(skill.Name)}</name>");
            sb.AppendLine($"    <description>{EscapeXml(skill.Description)}</description>");
            if (!string.IsNullOrWhiteSpace(location))
            {
                sb.AppendLine($"    <location>{EscapeXml(location)}</location>");
            }
            sb.AppendLine("  </skill>");
        }
        sb.AppendLine("</available_skills>");
        sb.AppendLine();
        sb.AppendLine("When a task matches a skill's description, use skill_activate to load its full instructions.");
        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>
    /// Generates a <skill_instructions/> XML snippet for reminder injection.
    /// </summary>
    public static string ToActivatedXml(Skill skill)
    {
        if (skill.Body == null)
        {
            return $"<skill_instructions name=\"{EscapeXml(skill.Name)}\">\n" +
                   "Skill body not loaded. Use skill_activate to load full content.\n" +
                   "</skill_instructions>";
        }

        var resourcesInfo = "";
        if (skill.Resources != null)
        {
            var parts = new List<string>();
            if (skill.Resources.Scripts?.Count > 0)
            {
                parts.Add($"Scripts: {string.Join(", ", skill.Resources.Scripts)}");
            }
            if (skill.Resources.References?.Count > 0)
            {
                parts.Add($"References: {string.Join(", ", skill.Resources.References)}");
            }
            if (skill.Resources.Assets?.Count > 0)
            {
                parts.Add($"Assets: {string.Join(", ", skill.Resources.Assets)}");
            }

            if (parts.Count > 0)
            {
                resourcesInfo = "\n\n[Available Resources]\n" + string.Join("\n", parts);
            }
        }

        return $"<skill_instructions name=\"{EscapeXml(skill.Name)}\">\n" +
               $"{skill.Body}{resourcesInfo}\n" +
               "</skill_instructions>";
    }

    /// <summary>
    /// Generates a short skills status summary (for context compression).
    /// </summary>
    public static string ToSummary(IEnumerable<Skill> skills)
    {
        var list = skills.ToList();
        if (list.Count == 0) return string.Empty;

        var activated = list.Where(s => s.ActivatedAt.HasValue).ToList();
        var available = list.Where(s => !s.ActivatedAt.HasValue).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("[Skills Status]");
        if (activated.Count > 0)
        {
            sb.AppendLine($"Activated: {string.Join(", ", activated.Select(s => s.Name))}");
        }
        if (available.Count > 0)
        {
            sb.AppendLine($"Available: {string.Join(", ", available.Select(s => s.Name))}");
        }
        return sb.ToString();
    }

    private static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}

