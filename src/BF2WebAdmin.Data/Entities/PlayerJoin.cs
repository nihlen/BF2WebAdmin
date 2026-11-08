using System;

namespace BF2WebAdmin.Data.Entities;

/// <summary>
/// A row of dbo.JoinHistory, the BF2v2 site's table of player joins (country flags from the newest IP per name, aliases
/// by the same Guid or IP). WebAdmin writes one row per human player connect; the columns are BF2v2's
/// (BF2v2Core.Core PlayerRepository Insert, Entities/PlayerJoin.cs).
/// </summary>
public class PlayerJoin
{
    public const int NameLength = 100, GuidLength = 32, IpAddressLength = 32;

    /// <summary>bigint on Postgres (player_join.id); int on SQL Server (dbo.JoinHistory.Id) and SQLite.</summary>
    public long Id { get; set; }

    /// <summary>The player name, clan tag included.</summary>
    public string Name { get; set; }

    /// <summary>The CD-key hash (before OpenBF2: the PunkBuster GUID).</summary>
    public string Guid { get; set; }

    /// <summary>The player's IP, without the port.</summary>
    public string IpAddress { get; set; }

    /// <summary>The game server's configured IP (Server.IpAddress).</summary>
    public string ServerIpAddress { get; set; }

    /// <summary>UTC.</summary>
    public DateTime JoinDate { get; set; }

    /// <summary>Set by BF2v2's spoof check; WebAdmin leaves it to the column default (0). Not in the Postgres table player_join.</summary>
    public bool IsLikelySpoof { get; set; }
}
