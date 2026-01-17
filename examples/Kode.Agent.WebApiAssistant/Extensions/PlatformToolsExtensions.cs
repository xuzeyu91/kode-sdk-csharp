using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Tools;
using Kode.Agent.WebApiAssistant.Models;
using Kode.Agent.WebApiAssistant.Services;
using Kode.Agent.WebApiAssistant.Tools.Agent;
using Kode.Agent.WebApiAssistant.Tools.Calendar;
using Kode.Agent.WebApiAssistant.Tools.Email;
using Kode.Agent.WebApiAssistant.Tools.Notify;
using Kode.Agent.WebApiAssistant.Tools.Time;
using Microsoft.Extensions.DependencyInjection;

namespace Kode.Agent.WebApiAssistant.Extensions;

/// <summary>
/// Extension methods for registering platform-specific tools.
/// </summary>
public static class PlatformToolsExtensions
{
    /// <summary>
    /// Registers platform-specific tools (calendar, time).
    /// Note: Email and Notify tools are loaded dynamically per-agent from .config directory.
    /// </summary>
    public static IToolRegistry RegisterPlatformTools(this IToolRegistry registry, IServiceProvider serviceProvider)
    {
        // Time tools
        var timeTool = new TimeTool(serviceProvider.GetRequiredService<ILogger<TimeTool>>());
        registry.Register(new TimeNowTool(timeTool));

        // Calendar tools
        var calendarTool = serviceProvider.GetService<ICalendarTool>();
        if (calendarTool != null)
        {
            registry.Register(new CalendarListTool(calendarTool));
            registry.Register(new CalendarCreateTool(calendarTool));
            registry.Register(new CalendarUpdateTool(calendarTool));
        }

        // Email and Notify tools are now loaded dynamically per-agent by AgentToolsLoader

        return registry;
    }

    /// <summary>
    /// Adds platform-specific tool services to the dependency injection container.
    /// Note: Email and Notify tools are loaded dynamically per-agent from .config directory.
    /// </summary>
    public static IServiceCollection AddPlatformTools(this IServiceCollection services, IConfiguration configuration)
    {
        // Time tool - always available
        services.AddSingleton<TimeTool>();

        // Calendar tool - macOS implementation
        services.AddSingleton<ICalendarTool, MacOSCalendarTool>();

        // Email and Notify tools are loaded dynamically per-agent by AgentToolsLoader
        // Register AgentToolsLoader for dynamic tool loading
        services.AddSingleton<AgentToolsLoader>();

        return services;
    }
}
