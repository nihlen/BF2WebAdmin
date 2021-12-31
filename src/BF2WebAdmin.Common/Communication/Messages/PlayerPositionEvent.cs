using BF2WebAdmin.Common.Abstractions;
using BF2WebAdmin.Common.Communication.DTOs;
using MessagePack;
using ProtoBuf;

namespace BF2WebAdmin.Common.Communication.Messages
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [MessagePackObject(keyAsPropertyName: true)]
    public class PlayerPositionEvent : IMessagePayload
    {
        public int PlayerId { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
        public int Ping { get; set; }
    }
}