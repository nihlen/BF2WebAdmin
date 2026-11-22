namespace BF2WebAdmin.Server.Communication.DTOs;

/// <summary>A line of a server's event log (the snapshot's EventLog), with what kind of event it is and the players it names.</summary>
public class EventLogDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Message { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public EventLogKind Kind { get; set; }

    /// <summary>The players the event names, as they were then (an index is reused once its player leaves); the acting player first.</summary>
    public List<EventLogPlayerDto> Players { get; set; } = [];
}

/// <summary>A player named by an event log line. Hash is null for a player without one (a bot, a LAN server).</summary>
public record EventLogPlayerDto(int Index, string Name, string? Hash);

public enum EventLogKind
{
    Other,
    /// <summary>A kill: the killer, then the victim (only the victim when there is no killer).</summary>
    Kill,
    Death,
    Join,
    Leave,
    Spawn,
    Team,
    Score,
    Vehicle,
    VehicleDestroyed,
    Revive,
    Kick,
    Ban,
    Bot,
    RoundStart,
    RoundEnd,
    /// <summary>The map changed, or the map list did.</summary>
    Map,
    ControlPoint,
    GameState,
    /// <summary>The admin channel connected or disconnected.</summary>
    Connection,
}
