using BF2WebAdmin.Common.Abstractions;
using MessagePack;
using ProtoBuf;

namespace BF2WebAdmin.Common.Communication.Messages
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [MessagePackObject(keyAsPropertyName: true)]
    public class ServerUpdateEvent : IMessagePayload
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string IpAddress { get; set; }
        public int GamePort { get; set; }
        public int QueryPort { get; set; }
        public string Map { get; set; }
        public int Players { get; set; }
        public int MaxPlayers { get; set; }
    }
}