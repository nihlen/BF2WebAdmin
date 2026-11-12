using BF2WebAdmin.Server.Communication.DTOs;

namespace BF2WebAdmin.Server.Communication.Events;

public class PlayerVehicleEvent
{
    public int PlayerId { get; set; }
    public VehicleDto Vehicle { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}