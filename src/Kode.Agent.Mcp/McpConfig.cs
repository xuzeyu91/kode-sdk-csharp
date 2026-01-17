namespace Kode.Agent.Mcp;

/// <summary>
/// MCP Transport type.
/// </summary>
public enum McpTransportType
{
    /// <summary>
    /// Standard I/O transport (subprocess).
    /// </summary>
    Stdio,

    /// <summary>
    /// HTTP transport (SSE-based for server-client communication).
    /// </summary>
    Http,

    /// <summary>
    /// Streamable HTTP transport (bidirectional HTTP streaming).
    /// </summary>
    StreamableHttp,

    /// <summary>
    /// Server-Sent Events transport.
    /// </summary>
    Sse
}

/// <summary>
/// Configuration for connecting to an MCP server.
/// </summary>
public sealed class McpConfig
{
    /// <summary>
    /// Gets or sets the transport type.
    /// </summary>
    public required McpTransportType Transport { get; init; }

    /// <summary>
    /// Gets or sets the command for stdio transport.
    /// </summary>
    public string? Command { get; init; }

    /// <summary>
    /// Gets or sets the arguments for stdio transport.
    /// </summary>
    public IReadOnlyList<string>? Args { get; init; }

    /// <summary>
    /// Gets or sets environment variables for stdio transport.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    /// <summary>
    /// Gets or sets the URL for HTTP/SSE transport.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Gets or sets HTTP headers for transport.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>
    /// Gets or sets the server name (used for namespacing).
    /// </summary>
    public string? ServerName { get; init; }

    /// <summary>
    /// Gets or sets the list of tool names to include (whitelist).
    /// If null, all tools are included.
    /// </summary>
    public IReadOnlyList<string>? Include { get; init; }

    /// <summary>
    /// Gets or sets the list of tool names to exclude (blacklist).
    /// </summary>
    public IReadOnlyList<string>? Exclude { get; init; }
}
