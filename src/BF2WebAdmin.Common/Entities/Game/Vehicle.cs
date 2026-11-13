using System.Collections.Generic;

namespace BF2WebAdmin.Common.Entities.Game;

/// <summary>
/// A vehicle as the game server reports it (admin channel): set only by WebAdmin's state applier. Modules keep their
/// own per-vehicle state (noclip, freeze) themselves.
/// </summary>
public class Vehicle : Entity
{
    public IList<Player> Players { get; set; }

    /// <summary>The root object's networkable id (0: none).</summary>
    public int RootVehicleId { get; set; }

    /// <summary>The root object's console (grid) id, the N in object.active idN (-1: none).</summary>
    public long ConsoleId { get; set; } = -1;

    public string RootVehicleTemplate { get; set; }
    public int Team { get; set; }

    /// <summary>0..1, -1 without an armour component.</summary>
    public double Health { get; set; } = -1;

    public bool IsDestroyed { get; set; }

    public Vehicle()
    {
        Players = new List<Player>();
    }
}