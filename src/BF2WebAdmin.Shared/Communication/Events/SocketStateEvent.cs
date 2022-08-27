namespace BF2WebAdmin.Shared.Communication.Events;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
//[MessagePackObject(keyAsPropertyName: true)]
public class SocketStateEvent : IMessagePayload
{
    public SocketState State { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}