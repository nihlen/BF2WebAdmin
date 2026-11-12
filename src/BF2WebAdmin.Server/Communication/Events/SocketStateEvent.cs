namespace BF2WebAdmin.Server.Communication.Events;

public class SocketStateEvent
{
    public SocketState State { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}