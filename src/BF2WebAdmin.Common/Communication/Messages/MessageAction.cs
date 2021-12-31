using BF2WebAdmin.Common.Abstractions;
using ProtoBuf;

namespace BF2WebAdmin.Common.Communication.Messages
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class MessageAction : IMessage
    {
        public string Type { get; private set; } = nameof(MessageAction);
        public string ServerId { get; private set; }
        public IMessagePayload Payload { get; set; }

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
}