using BF2WebAdmin.Server.Communication.DTOs;

namespace BF2WebAdmin.Server.Communication.Events;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
//[MessagePackObject(keyAsPropertyName: true)]
public class PlayerDeathEvent
{
    public int PlayerId { get; set; }
    public Vector3 Position { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}