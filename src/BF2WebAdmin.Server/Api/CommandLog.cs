using System.Collections.Concurrent;
using BF2WebAdmin.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace BF2WebAdmin.Server.Api;

/// <summary>What a web user sent: chat, a "." command, a "!" admin command or console line (Rcon), or a server action.</summary>
public enum CommandKind
{
    Chat,
    Command,
    Rcon,
    Action,
}

/// <summary>One entry of a server's command log: who sent what, and the answer.</summary>
public record CommandLogEntryDto(string Id, string ServerId, DateTimeOffset Time, string User, CommandKind Kind, string Command, string? Response, bool Success);

/// <summary>
/// The commands web users sent to each server, newest last, and their answers (the web UI's Activity tab). Kept in
/// memory only: a restart of WebAdmin empties it. Every new entry is also sent to the server's hub group.
/// </summary>
public sealed class CommandLog(IHubContext<ServerHub, IServerHubClient> hub)
{
    private const int MaxEntries = 300;
    private readonly ConcurrentDictionary<string, Queue<CommandLogEntryDto>> _servers = new();

    public IReadOnlyList<CommandLogEntryDto> Get(string serverId)
    {
        if (!_servers.TryGetValue(serverId, out var entries))
            return [];

        lock (entries)
            return entries.ToArray();
    }

    public async Task<CommandLogEntryDto> AddAsync(string serverId, string user, CommandKind kind, string command, string? response, bool success)
    {
        var entry = new CommandLogEntryDto(Guid.NewGuid().ToString("N"), serverId, DateTimeOffset.UtcNow, user, kind, command, response, success);
        var entries = _servers.GetOrAdd(serverId, _ => new Queue<CommandLogEntryDto>());
        lock (entries)
        {
            entries.Enqueue(entry);
            while (entries.Count > MaxEntries)
                entries.Dequeue();
        }

        await hub.Clients.Group(serverId).CommandLogEvent(entry);
        return entry;
    }
}
