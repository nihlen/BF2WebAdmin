namespace BF2WebAdmin.Shared.Communication.Events;

public class ServerRemoveEvent : IMessagePayload
{
    public string ServerId { get; set; }
}
