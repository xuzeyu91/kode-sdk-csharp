using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace Kode.Agent.Mcp;

/// <summary>
/// Manages MCP client connections to multiple servers.
/// </summary>
public sealed class McpClientManager : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, McpClient> _clients = new();
    private readonly ILogger<McpClientManager>? _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpClientManager"/> class.
    /// </summary>
    public McpClientManager(ILogger<McpClientManager>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Connects to an MCP server.
    /// </summary>
    /// <param name="serverName">The name of the server (for namespacing).</param>
    /// <param name="config">The MCP configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The connected MCP client.</returns>
    public async Task<McpClient> ConnectAsync(
        string serverName,
        McpConfig config,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Return existing client if already connected
        if (_clients.TryGetValue(serverName, out var existingClient))
        {
            return existingClient;
        }

        _logger?.LogDebug("Connecting to MCP server: {ServerName}", serverName);

        McpClient client;
        
        switch (config.Transport)
        {
            case McpTransportType.Stdio:
                if (string.IsNullOrEmpty(config.Command))
                {
                    throw new ArgumentException("Command is required for stdio transport", nameof(config));
                }

                var stdioTransport = new StdioClientTransport(new StdioClientTransportOptions
                {
                    Name = serverName,
                    Command = config.Command,
                    Arguments = config.Args?.ToList() ?? [],
                    EnvironmentVariables = config.Environment?.ToDictionary(
                        kvp => kvp.Key,
                        kvp => (string?)kvp.Value) ?? []
                });

                client = await McpClient.CreateAsync(stdioTransport, cancellationToken: cancellationToken);
                break;

            case McpTransportType.Http:
            case McpTransportType.StreamableHttp:
            case McpTransportType.Sse:
                if (string.IsNullOrEmpty(config.Url))
                {
                    throw new ArgumentException("URL is required for HTTP/StreamableHttp/SSE transport", nameof(config));
                }

                // Build additional headers dictionary
                var additionalHeaders = new Dictionary<string, string>();
                if (config.Headers != null)
                {
                    foreach (var header in config.Headers)
                    {
                        additionalHeaders[header.Key] = header.Value;
                    }
                }

                var httpTransport = new HttpClientTransport(new HttpClientTransportOptions
                {
                    Name = serverName,
                    Endpoint = new Uri(config.Url),
                    AdditionalHeaders = additionalHeaders
                });

                client = await McpClient.CreateAsync(httpTransport, cancellationToken: cancellationToken);
                break;

            default:
                throw new ArgumentException($"Unsupported transport type: {config.Transport}", nameof(config));
        }

        if (!_clients.TryAdd(serverName, client))
        {
            // Another thread connected first, dispose our client and use the existing one
            await client.DisposeAsync();
            return _clients[serverName];
        }

        _logger?.LogInformation("Connected to MCP server: {ServerName}", serverName);
        return client;
    }

    /// <summary>
    /// Disconnects from an MCP server.
    /// </summary>
    /// <param name="serverName">The name of the server.</param>
    public async Task DisconnectAsync(string serverName)
    {
        if (_clients.TryRemove(serverName, out var client))
        {
            _logger?.LogDebug("Disconnecting from MCP server: {ServerName}", serverName);
            await client.DisposeAsync();
            _logger?.LogInformation("Disconnected from MCP server: {ServerName}", serverName);
        }
    }

    /// <summary>
    /// Disconnects from all MCP servers.
    /// </summary>
    public async Task DisconnectAllAsync()
    {
        var serverNames = _clients.Keys.ToList();
        foreach (var serverName in serverNames)
        {
            await DisconnectAsync(serverName);
        }
    }

    /// <summary>
    /// Gets a connected client by server name.
    /// </summary>
    /// <param name="serverName">The name of the server.</param>
    /// <returns>The client if connected; otherwise null.</returns>
    public McpClient? GetClient(string serverName)
    {
        return _clients.TryGetValue(serverName, out var client) ? client : null;
    }

    /// <summary>
    /// Gets all connected server names.
    /// </summary>
    public IReadOnlyList<string> ConnectedServers => _clients.Keys.ToList();

    /// <summary>
    /// Disposes all client connections.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        
        await DisconnectAllAsync();
    }
}
