using Microsoft.Extensions.DependencyInjection;
using Kode.Agent.Sdk.Core.Abstractions;
using StackExchange.Redis;

namespace Kode.Agent.Store.Redis;

/// <summary>
/// Extension methods for registering Redis store services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Redis-based agent store.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">Redis connection string.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddRedisAgentStore(
        this IServiceCollection services,
        string connectionString,
        Action<RedisStoreOptions>? configure = null)
    {
        var options = new RedisStoreOptions { ConnectionString = connectionString };
        configure?.Invoke(options);

        // Register the connection multiplexer as singleton
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            return ConnectionMultiplexer.Connect(connectionString);
        });

        services.AddSingleton(options);
        services.AddSingleton<RedisAgentStore>();
        services.AddSingleton<IAgentStore>(sp => sp.GetRequiredService<RedisAgentStore>());

        return services;
    }

    /// <summary>
    /// Adds Redis-based agent store with existing connection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionMultiplexer">Existing Redis connection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddRedisAgentStore(
        this IServiceCollection services,
        IConnectionMultiplexer connectionMultiplexer,
        Action<RedisStoreOptions>? configure = null)
    {
        var options = new RedisStoreOptions();
        configure?.Invoke(options);

        services.AddSingleton(connectionMultiplexer);
        services.AddSingleton(options);
        services.AddSingleton<RedisAgentStore>();
        services.AddSingleton<IAgentStore>(sp => sp.GetRequiredService<RedisAgentStore>());

        return services;
    }
}
