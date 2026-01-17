using Kode.Agent.Sdk.Tools;
using Kode.Agent.Sdk.Core.Abstractions;

namespace Kode.Agent.Examples;

/// <summary>
/// Example demonstrating how to create and register custom tools.
/// </summary>
public static class CustomToolsExample
{
    public static Task RunAsync()
    {
        Console.WriteLine("=== Custom Tools Example ===\n");

        // Create a tool registry
        var registry = new ToolRegistry();

        // 1. Register a simple tool instance
        Console.WriteLine("1. Registering tool instances:\n");
        
        var calculatorTool = new CalculatorTool();
        registry.Register(calculatorTool);
        Console.WriteLine($"   ✓ Registered: {calculatorTool.Name}");

        var weatherTool = new WeatherTool();
        registry.Register(weatherTool);
        Console.WriteLine($"   ✓ Registered: {weatherTool.Name}");

        // 2. Register using factory pattern
        Console.WriteLine("\n2. Registering tool with factory:\n");
        registry.Register("database_query", ctx =>
        {
            var connectionString = ctx.Config?.GetValueOrDefault("connectionString") as string ?? "default";
            return new DatabaseQueryTool(connectionString);
        });
        Console.WriteLine("   ✓ Registered: database_query (factory)");

        // 3. List all registered tools
        Console.WriteLine("\n3. All registered tools:");
        foreach (var toolName in registry.List())
        {
            Console.WriteLine($"   - {toolName}");
        }

        // 4. Get and describe a tool
        Console.WriteLine("\n4. Tool descriptions:");
        var tool = registry.Get("calculator");
        if (tool != null)
        {
            Console.WriteLine($"   [{tool.Name}]");
            Console.WriteLine($"   Description: {tool.Description}");
            Console.WriteLine($"   Read-only: {tool.Attributes.ReadOnly}");
            Console.WriteLine($"   Parallel: {tool.Attributes.AllowParallel}");
        }

        // 5. Create tool from factory with config
        Console.WriteLine("\n5. Creating tool from factory with config:");
        var dbTool = registry.Create("database_query", new Dictionary<string, object>
        {
            ["connectionString"] = "Server=localhost;Database=mydb"
        });
        Console.WriteLine($"   Created: {dbTool.Name} with custom connection string");

        // 6. Check tool existence
        Console.WriteLine("\n6. Tool existence check:");
        Console.WriteLine($"   'calculator' exists: {registry.Has("calculator")}");
        Console.WriteLine($"   'nonexistent' exists: {registry.Has("nonexistent")}");

        // 7. Tool attributes explanation
        Console.WriteLine("\n7. Tool attributes explained:");
        Console.WriteLine("   - ReadOnly: Tool doesn't modify state");
        Console.WriteLine("   - NoEffect: Tool has no side effects");
        Console.WriteLine("   - RequiresApproval: Needs user confirmation");
        Console.WriteLine("   - AllowParallel: Can run concurrently");
        Console.WriteLine("   - PermissionCategory: Custom permission grouping");

        Console.WriteLine("\n=== Custom Tools Example Complete ===");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Example custom tool: Calculator
/// </summary>
internal class CalculatorTool : ITool
{
    public string Name => "calculator";
    public string Description => "Perform mathematical calculations";
    public ToolAttributes Attributes => new()
    {
        ReadOnly = true,
        NoEffect = true,
        AllowParallel = true
    };
    public object InputSchema => new
    {
        type = "object",
        properties = new
        {
            operation = new { type = "string", @enum = new[] { "add", "subtract", "multiply", "divide" } },
            a = new { type = "number", description = "First operand" },
            b = new { type = "number", description = "Second operand" }
        },
        required = new[] { "operation", "a", "b" }
    };

    public ValueTask<string?> GetPromptAsync(ToolContext context) 
        => ValueTask.FromResult<string?>(null);

    public Task<ToolResult> ExecuteAsync(object arguments, ToolContext context, CancellationToken cancellationToken = default)
    {
        // In real implementation, parse arguments and perform calculation
        return Task.FromResult(ToolResult.Ok(new { result = 42 }));
    }

    public ToolDescriptor ToDescriptor() => new()
    {
        Source = ToolSource.Registered,
        Name = Name
    };
}

/// <summary>
/// Example custom tool: Weather
/// </summary>
internal class WeatherTool : ITool
{
    public string Name => "weather";
    public string Description => "Get current weather for a location";
    public ToolAttributes Attributes => new()
    {
        ReadOnly = true,
        AllowParallel = true
    };
    public object InputSchema => new
    {
        type = "object",
        properties = new
        {
            location = new { type = "string", description = "City name or coordinates" }
        },
        required = new[] { "location" }
    };

    public ValueTask<string?> GetPromptAsync(ToolContext context)
        => ValueTask.FromResult<string?>("You can check weather for any city worldwide.");

    public Task<ToolResult> ExecuteAsync(object arguments, ToolContext context, CancellationToken cancellationToken = default)
    {
        // In real implementation, call weather API
        return Task.FromResult(ToolResult.Ok(new 
        { 
            temperature = 22,
            condition = "Sunny",
            humidity = 45
        }));
    }

    public ToolDescriptor ToDescriptor() => new()
    {
        Source = ToolSource.Registered,
        Name = Name
    };
}

/// <summary>
/// Example custom tool: Database Query (with configuration)
/// </summary>
internal class DatabaseQueryTool : ITool
{
    private readonly string _connectionString;

    public DatabaseQueryTool(string connectionString)
    {
        _connectionString = connectionString;
    }

    public string Name => "database_query";
    public string Description => "Execute read-only database queries";
    public ToolAttributes Attributes => new()
    {
        ReadOnly = true,
        RequiresApproval = true // Database access requires approval
    };
    public object InputSchema => new
    {
        type = "object",
        properties = new
        {
            query = new { type = "string", description = "SQL SELECT query" }
        },
        required = new[] { "query" }
    };

    public ValueTask<string?> GetPromptAsync(ToolContext context)
        => ValueTask.FromResult<string?>($"Connected to: {_connectionString[..Math.Min(20, _connectionString.Length)]}...");

    public Task<ToolResult> ExecuteAsync(object arguments, ToolContext context, CancellationToken cancellationToken = default)
    {
        // In real implementation, execute query
        return Task.FromResult(ToolResult.Ok(new { rows = Array.Empty<object>(), affected = 0 }));
    }

    public ToolDescriptor ToDescriptor() => new()
    {
        Source = ToolSource.Registered,
        Name = Name,
        Config = new Dictionary<string, object> { ["connectionString"] = _connectionString }
    };
}
