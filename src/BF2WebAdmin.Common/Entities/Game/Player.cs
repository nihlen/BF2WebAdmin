using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace BF2WebAdmin.Common.Entities.Game;

public class Player : Entity
{
    public string Name { get; set; }
    public int Index { get; set; }
    public IPAddress IpAddress { get; set; }
    public string Hash { get; set; }
    public Country Country { get; } = new Country();
    public Rank Rank { get; set; }
    public Vehicle Vehicle { get; set; }
    public Vehicle PreviousVehicle { get; set; }
    public string SubVehicleTemplate { get; set; }
    public Team Team { get; set; }
    public bool IsAlive { get; set; }
    public Score Score { get; } = new Score();

    // Custom/Aggregate properties from modules
    public IDictionary<int, int> PingHistory { get; } = new Dictionary<int, int>();
    public DateTime LastLeaveNotification { get; set; }
    public DateTime LastAdminCall { get; set; }

    public string DisplayName => Name?.Trim();
    public string ShortName
    {
        get
        {
            var parts = DisplayName?.Split(" ");
            return (parts?.Length ?? 0) > 1 ? parts!.Last() : DisplayName;
        }
    }

    public override string ToString() => $"{Name} ({Hash})";
}

public class Score
{
    public int Total { get; set; }
    public int Team { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Ping { get; set; }
}

public class Country
{
    public string Name { get; set; }
    public string Code { get; set; }
}