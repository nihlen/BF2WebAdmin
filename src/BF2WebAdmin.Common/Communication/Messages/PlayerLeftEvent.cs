using BF2WebAdmin.Common.Abstractions;
using MessagePack;
using ProtoBuf;

namespace BF2WebAdmin.Common.Communication.Messages
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [MessagePackObject(keyAsPropertyName: true)]
    public class PlayerLeftEvent : IMessagePayload
    {
        public int PlayerId { get; set; }
    }
}