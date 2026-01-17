namespace Kode.Agent.Examples.Shared;

/// <summary>
/// Loads environment variables from .env file.
/// </summary>
public static class EnvLoader
{
    private static bool _loaded;

    /// <summary>
    /// Loads environment variables from .env file if not already loaded.
    /// </summary>
    public static void Load()
    {
        if (_loaded) return;

        // Try to find .env file in current directory or parent directories
        var directory = Directory.GetCurrentDirectory();
        while (directory != null)
        {
            var envPath = Path.Combine(directory, ".env");
            if (File.Exists(envPath))
            {
                DotNetEnv.Env.Load(envPath);
                Console.WriteLine($"[env] Loaded from {envPath}");
                _loaded = true;
                return;
            }
            directory = Directory.GetParent(directory)?.FullName;
        }

        Console.WriteLine("[env] No .env file found, using system environment variables");
        _loaded = true;
    }

    /// <summary>
    /// Gets an environment variable value.
    /// </summary>
    public static string? Get(string key) => Environment.GetEnvironmentVariable(key);

    /// <summary>
    /// Gets an environment variable value with a default.
    /// </summary>
    public static string Get(string key, string defaultValue) =>
        Environment.GetEnvironmentVariable(key) ?? defaultValue;
}
