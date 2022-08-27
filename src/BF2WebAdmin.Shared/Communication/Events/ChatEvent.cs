using BF2WebAdmin.Shared.Communication.DTOs;

namespace BF2WebAdmin.Shared.Communication.Events;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
//[MessagePackObject(keyAsPropertyName: true)]
public class ChatEvent : IMessagePayload
{
    public MessageDto Message { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}