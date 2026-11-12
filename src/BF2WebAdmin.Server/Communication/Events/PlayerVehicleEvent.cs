using BF2WebAdmin.Server.Communication.DTOs;

namespace BF2WebAdmin.Server.Communication.Events;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
//[MessagePackObject(keyAsPropertyName: true)]
public class PlayerVehicleEvent
{
    public int PlayerId { get; set; }
    public VehicleDto Vehicle { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}