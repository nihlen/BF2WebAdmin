using BF2WebAdmin.Shared.Communication.DTOs;

namespace BF2WebAdmin.Shared.Communication.Events;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
//[MessagePackObject(keyAsPropertyName: true)]
public class PlayerJoinEvent : IMessagePayload
{
    public PlayerDto Player { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}