using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Kode.Agent.Sdk.Core.Pool;
using Microsoft.Extensions.Logging;

namespace Kode.Agent.Sdk.Core.Collaboration;

/// <summary>
/// Room member information.
/// </summary>
public record RoomMember(string Name, string AgentId);

/// <summary>
/// Message sent in a room.
/// </summary>
public record RoomMessage(
    string From,
    string Text,
    IReadOnlyList<string>? Mentions,
    DateTimeOffset Timestamp
);

/// <summary>
/// Room for multi-agent collaboration.
/// </summary>
public partial class Room
{
    private readonly ConcurrentDictionary<string, string> _members = new();
    private readonly AgentPool _pool;
    private readonly ILogger<Room>? _logger;
    private readonly List<RoomMessage> _history = [];
    private readonly object _historyLock = new();

    public Room(AgentPool pool, ILogger<Room>? logger = null)
    {
        _pool = pool;
        _logger = logger;
    }

    /// <summary>
    /// Room name/identifier.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Join a member to the room.
    /// </summary>
    public void Join(string name, string agentId)
    {
        if (!_members.TryAdd(name, agentId))
        {
            throw new InvalidOperationException($"Member already exists: {name}");
        }
        _logger?.LogInformation("Member {Name} (agent {AgentId}) joined room", name, agentId);
    }

    /// <summary>
    /// Leave the room.
    /// </summary>
    public void Leave(string name)
    {
        if (_members.TryRemove(name, out var agentId))
        {
            _logger?.LogInformation("Member {Name} (agent {AgentId}) left room", name, agentId);
        }
    }

    /// <summary>
    /// Say something in the room.
    /// </summary>
    public async Task SayAsync(
        string from, 
        string text,
        CancellationToken cancellationToken = default)
    {
        var mentions = ExtractMentions(text);
        var message = new RoomMessage(from, text, mentions, DateTimeOffset.UtcNow);
        
        lock (_historyLock)
        {
            _history.Add(message);
        }

        var formattedMessage = $"[from:{from}] {text}";

        if (mentions.Count > 0)
        {
            // Directed message to mentioned members
            foreach (var mention in mentions)
            {
                if (_members.TryGetValue(mention, out var agentId))
                {
                    var agent = _pool.Get(agentId);
                    if (agent != null)
                    {
                        await agent.RunAsync(formattedMessage, cancellationToken);
                        _logger?.LogDebug("Sent directed message from {From} to {To}", from, mention);
                    }
                }
            }
        }
        else
        {
            // Broadcast to all except sender
            var tasks = new List<Task>();
            foreach (var (name, agentId) in _members)
            {
                if (name != from)
                {
                    var agent = _pool.Get(agentId);
                    if (agent != null)
                    {
                        tasks.Add(agent.RunAsync(formattedMessage, cancellationToken));
                    }
                }
            }
            await Task.WhenAll(tasks);
            _logger?.LogDebug("Broadcast message from {From} to {Count} members", from, tasks.Count);
        }
    }

    /// <summary>
    /// Send a direct message to a specific member.
    /// </summary>
    public async Task WhisperAsync(
        string from,
        string to,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (!_members.TryGetValue(to, out var agentId))
        {
            throw new KeyNotFoundException($"Member not found: {to}");
        }

        var agent = _pool.Get(agentId);
        if (agent == null)
        {
            throw new KeyNotFoundException($"Agent not found for member: {to}");
        }

        var formattedMessage = $"[whisper from:{from}] {text}";
        await agent.RunAsync(formattedMessage, cancellationToken);
        _logger?.LogDebug("Whisper from {From} to {To}", from, to);
    }

    /// <summary>
    /// Get all members in the room.
    /// </summary>
    public IReadOnlyList<RoomMember> GetMembers()
    {
        return _members.Select(kvp => new RoomMember(kvp.Key, kvp.Value)).ToList();
    }

    /// <summary>
    /// Check if a member is in the room.
    /// </summary>
    public bool HasMember(string name) => _members.ContainsKey(name);

    /// <summary>
    /// Get the agent ID for a member.
    /// </summary>
    public string? GetAgentId(string name)
    {
        _members.TryGetValue(name, out var agentId);
        return agentId;
    }

    /// <summary>
    /// Get the message history.
    /// </summary>
    public IReadOnlyList<RoomMessage> GetHistory()
    {
        lock (_historyLock)
        {
            return _history.ToList();
        }
    }

    /// <summary>
    /// Clear the message history.
    /// </summary>
    public void ClearHistory()
    {
        lock (_historyLock)
        {
            _history.Clear();
        }
    }

    /// <summary>
    /// Get the number of members.
    /// </summary>
    public int MemberCount => _members.Count;

    private static IReadOnlyList<string> ExtractMentions(string text)
    {
        var matches = MentionRegex().Matches(text);
        return matches.Select(m => m.Groups[1].Value).Distinct().ToList();
    }

    [GeneratedRegex(@"@(\w+)")]
    private static partial Regex MentionRegex();
}
