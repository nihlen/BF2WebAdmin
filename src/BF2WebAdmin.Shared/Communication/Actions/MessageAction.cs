namespace BF2WebAdmin.Shared.Communication.Actions;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MessageAction : IMessage
{
    public string Type { get; private set; } = nameof(MessageAction);
    public string ServerId { get; private set; }
    public object Payload { get; set; }

    public static MessageAction Create(string serverId, IMessagePayload payload)
    {
        return new MessageAction
        {
            Type = payload.GetType().Name,
            ServerId = serverId,
            Payload = payload
        };
    }
}