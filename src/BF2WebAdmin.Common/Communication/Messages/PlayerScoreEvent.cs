using BF2WebAdmin.Common.Abstractions;
using MessagePack;
using ProtoBuf;

namespace BF2WebAdmin.Common.Communication.Messages
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [MessagePackObject(keyAsPropertyName: true)]
    public class PlayerScoreEvent : IMessagePayload
    {
        public int PlayerId { get; set; }
        public int TeamScore { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int TotalScore { get; set; }
    }
}