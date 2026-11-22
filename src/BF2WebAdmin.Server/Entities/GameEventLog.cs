using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Communication.DTOs;

namespace BF2WebAdmin.Server.Entities;

/// <summary>A line of a server's event log: the text, when, what kind of event, and the players it names as they were then.</summary>
public sealed record GameEventLog(string Message, DateTimeOffset Time, EventLogKind Kind, IReadOnlyList<EventLogPlayerDto> Players)
{
    public EventLogDto ToDto() => new() { Message = Message, Timestamp = Time, Kind = Kind, Players = Players.ToList() };

    /// <summary>The players as they are now, without the nulls; the hash as the player has it (a test server's "name:" key too).</summary>
    public static IReadOnlyList<EventLogPlayerDto> Named(IEnumerable<Player?> players) =>
        players.OfType<Player>()
            .Select(p => new EventLogPlayerDto(p.Index, p.Name, string.IsNullOrEmpty(p.Hash) ? null : p.Hash))
            .ToArray();
}
