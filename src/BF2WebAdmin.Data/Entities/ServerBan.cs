using System;

namespace BF2WebAdmin.Data.Entities;

/// <summary>
/// What WebAdmin knows about a ban on one game server: who was banned, by whom and why. The ban itself is the engine's
/// (its ban list, settings/banlist.con on the server), which also has its length; this row only describes it. Keyed by
/// the server and the banned identifier, as the engine's list is.
/// </summary>
public class ServerBan
{
    public const int ServerIdLength = 50, IdentifierLength = 64, NameLength = 100, BannedByLength = 100, ReasonLength = 200;

    /// <summary>The server's id, ip:port.</summary>
    public string ServerId { get; set; }

    /// <summary>The banned CD-key hash (<see cref="ByCdKey"/>) or IP address, as in the engine's ban list.</summary>
    public string Identifier { get; set; }

    public bool ByCdKey { get; set; }

    /// <summary>The player's name when banned.</summary>
    public string Name { get; set; }

    /// <summary>The web user, Discord user or player who banned.</summary>
    public string BannedBy { get; set; }

    public string Reason { get; set; }

    /// <summary>UTC.</summary>
    public DateTime BannedAt { get; set; }
}
