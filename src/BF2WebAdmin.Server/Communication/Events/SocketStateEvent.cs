namespace BF2WebAdmin.Server.Communication.Events;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
//[MessagePackObject(keyAsPropertyName: true)]
public class SocketStateEvent
{
    public SocketState State { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}