using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kode.Agent.Mcp;

/// <summary>
/// Extension methods for adding MCP services to the service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds MCP client manager to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMcpClientManager(this IServiceCollection services)
    {
        services.TryAddSingleton<McpClientManager>();
        return services;
    }
}
