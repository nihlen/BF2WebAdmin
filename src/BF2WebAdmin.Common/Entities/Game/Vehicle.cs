using System.Collections.Generic;

namespace BF2WebAdmin.Common.Entities.Game;

public class Vehicle : Entity
{
    public IList<Player> Players { get; set; }

    public int RootVehicleId { get; set; }
    public string RootVehicleTemplate { get; set; }

    public bool HasCollision { get; set; }
    public bool IsDisabled { get; set; }
        
    public Vehicle()
    {
        Players = new List<Player>();
        HasCollision = true;
        IsDisabled = false;
    }
}