using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace BF2WebAdmin.Common.Entities.Game;

/// <summary>
/// A player as the game server reports it (admin channel): set only by WebAdmin's state applier, from events, snapshots
/// and the player stream. Modules read it and keep their own per-player state (cooldowns, statistics) themselves.
/// </summary>
public class Player : Entity
{
    public string Name { get; set; }
    public int Index { get; set; }
    public IPAddress IpAddress { get; set; }
    /// <summary>
    /// The player key: the CD-key hash when the server has one, else (test servers only) <see cref="NameKeyPrefix"/> + name.
    /// Empty when the player has no key.
    /// </summary>
    public string Hash { get; set; }
    public bool IsBot { get; set; }
    public int Squad { get; set; }
    public Country Country { get; } = new Country();
    public Rank Rank { get; set; }
    public Vehicle Vehicle { get; set; }
    public Vehicle PreviousVehicle { get; set; }
    public string SubVehicleTemplate { get; set; }
    public Team Team { get; set; }
    public bool IsAlive { get; set; }
    public Score Score { get; } = new Score();

    public string DisplayName => Name?.Trim();
    public string ShortName
    {
        get
        {
            var parts = DisplayName?.Split(" ");
            return (parts?.Length ?? 0) > 1 ? parts!.Last() : DisplayName;
        }
    }

    public const string NameKeyPrefix = "name:";

    public bool HasNameKey => Hash?.StartsWith(NameKeyPrefix) ?? false;

    public override string ToString() => $"{Name} ({Hash})";
}

public class Score
{
    public int Total { get; set; }
    public int Team { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int TeamKills { get; set; }
    public int Ping { get; set; }
}

/// <summary>The country of the player's address (GeoIP), looked up when the player is added.</summary>
public class Country
{
    public string Name { get; set; }
    public string Code { get; set; }
}