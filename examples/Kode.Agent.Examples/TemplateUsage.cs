using Kode.Agent.Sdk.Core.Templates;

namespace Kode.Agent.Examples;

/// <summary>
/// Example demonstrating the Template Registry for managing agent templates.
/// </summary>
public static class TemplateUsage
{
    public static Task RunAsync()
    {
        Console.WriteLine("=== Template Registry Example ===\n");

        // Create a template registry
        var registry = new AgentTemplateRegistry();

        // 1. Register templates
        Console.WriteLine("1. Registering agent templates...\n");

        // Coder template - for software development tasks
        registry.Register(new AgentTemplateDefinition
        {
            Id = "coder",
            Name = "Software Engineer",
            Description = "Expert at writing, reviewing, and debugging code",
            Version = "1.0.0",
            SystemPrompt = """
                You are an expert software engineer. You can:
                - Write clean, maintainable code
                - Debug issues efficiently
                - Suggest best practices
                - Review code for improvements
                
                Always explain your reasoning and consider edge cases.
                """,
            Model = "claude-sonnet-4-20250514",
            Tools = ToolsConfig.Specific("fs_read", "fs_write", "fs_glob", "fs_grep", "bash_run"),
            Permission = new PermissionConfig
            {
                Mode = "auto",
                RequireApprovalTools = ["bash_run"]
            }
        });
        Console.WriteLine("   ✓ Registered 'coder' template");

        // Researcher template - for research and analysis
        registry.Register(new AgentTemplateDefinition
        {
            Id = "researcher",
            Name = "Research Analyst",
            Description = "Specializes in research, analysis, and summarization",
            Version = "1.0.0",
            SystemPrompt = """
                You are a research analyst. Your strengths include:
                - Finding relevant information
                - Analyzing data and trends
                - Summarizing complex topics
                - Creating comprehensive reports
                
                Be thorough and cite your sources when possible.
                """,
            Model = "claude-sonnet-4-20250514",
            Tools = ToolsConfig.Specific("fs_read", "fs_glob", "fs_grep"),
            Permission = new PermissionConfig
            {
                Mode = "readonly"
            }
        });
        Console.WriteLine("   ✓ Registered 'researcher' template");

        // Reviewer template - for code review
        registry.Register(new AgentTemplateDefinition
        {
            Id = "reviewer",
            Name = "Code Reviewer",
            Description = "Expert at reviewing code and suggesting improvements",
            Version = "1.0.0",
            SystemPrompt = """
                You are an expert code reviewer. Focus on:
                - Code quality and readability
                - Performance implications
                - Security concerns
                - Best practices and patterns
                
                Be constructive and explain the reasoning behind suggestions.
                """,
            Model = "claude-sonnet-4-20250514",
            Tools = ToolsConfig.Specific("fs_read", "fs_glob", "fs_grep"),
            Permission = new PermissionConfig
            {
                Mode = "readonly"
            }
        });
        Console.WriteLine("   ✓ Registered 'reviewer' template");

        // 2. List all templates
        Console.WriteLine("\n2. Listing all templates:\n");
        foreach (var template in registry.List())
        {
            Console.WriteLine($"   [{template.Id}]");
            Console.WriteLine($"      Name: {template.Name}");
            Console.WriteLine($"      Description: {template.Description}");
            Console.WriteLine($"      Model: {template.Model}");
            Console.WriteLine($"      Tools: {(template.Tools.AllowAll ? "*" : string.Join(", ", template.Tools.AllowedTools ?? []))}");
            Console.WriteLine();
        }

        // 3. Get a specific template
        Console.WriteLine("3. Getting 'coder' template:");
        var coderTemplate = registry.Get("coder");
        Console.WriteLine($"   Name: {coderTemplate.Name}");
        var preview = coderTemplate.SystemPrompt.Length <= 50 ? coderTemplate.SystemPrompt : coderTemplate.SystemPrompt[..50] + "...";
        Console.WriteLine($"   System Prompt Preview: {preview}");

        // 4. Check template existence
        Console.WriteLine("\n4. Checking template existence:");
        Console.WriteLine($"   'coder' exists: {registry.Has("coder")}");
        Console.WriteLine($"   'nonexistent' exists: {registry.Has("nonexistent")}");

        // 5. TryGet pattern
        Console.WriteLine("\n5. Using TryGet pattern:");
        if (registry.TryGet("researcher", out var researcher))
        {
            Console.WriteLine($"   Found: {researcher!.Name}");
        }
        if (!registry.TryGet("missing", out _))
        {
            Console.WriteLine("   'missing' template not found (as expected)");
        }

        // 6. Bulk registration
        Console.WriteLine("\n6. Bulk registering templates:");
        registry.BulkRegister([
            new AgentTemplateDefinition
            {
                Id = "assistant-basic",
                SystemPrompt = "You are a helpful assistant. Keep answers concise.",
            },
            new AgentTemplateDefinition
            {
                Id = "assistant-strict",
                SystemPrompt = "You are a helpful assistant. Be strict about safety and approvals.",
            }
        ]);
        Console.WriteLine($"   Total templates: {registry.List().Count}");

        // 7. Remove a template
        Console.WriteLine("\n7. Removing a template:");
        var removed = registry.Remove("assistant-basic");
        Console.WriteLine($"   Removed 'assistant-basic': {removed}");
        Console.WriteLine($"   Remaining templates: {registry.List().Count}");

        Console.WriteLine("\n=== Template Registry Example Complete ===");
        return Task.CompletedTask;
    }
}
