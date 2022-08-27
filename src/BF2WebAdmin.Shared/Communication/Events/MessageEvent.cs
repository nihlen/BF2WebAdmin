namespace BF2WebAdmin.Shared.Communication.Events;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MessageEvent : IMessage
{
    public string Type { get; private set; } = nameof(MessageEvent);
    public string ServerId { get; private set; }
    public object Payload { get; set; }

    public static MessageEvent Create(string serverId, IMessagePayload payload)
    {
        return new MessageEvent
        {
            Type = payload.GetType().Name,
            ServerId = serverId,
            Payload = payload
        };
    }
}