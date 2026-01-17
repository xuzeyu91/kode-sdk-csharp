using Kode.Agent.Sdk.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Kode.Agent.Store.Json;

/// <summary>
/// Extension methods for registering JSON store services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the JSON file-based agent store.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseDir">The base directory for storage.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddJsonAgentStore(this IServiceCollection services, string baseDir)
    {
        services.AddSingleton<IAgentStore>(new JsonAgentStore(baseDir));
        return services;
    }

    /// <summary>
    /// Adds the JSON file-based agent store with configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddJsonAgentStore(this IServiceCollection services, Action<JsonStoreOptions> configure)
    {
        var options = new JsonStoreOptions();
        configure(options);

        services.AddSingleton<IAgentStore>(new JsonAgentStore(options.BaseDirectory));

        return services;
    }
}

/// <summary>
/// Options for JSON store configuration.
/// </summary>
public class JsonStoreOptions
{
    /// <summary>
    /// The base directory for storage.
    /// </summary>
    public string BaseDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kode.Agent", "data");
}
