using BF2WebAdmin.Common.Abstractions;
using BF2WebAdmin.Common.Communication.DTOs;
using MessagePack;
using ProtoBuf;

namespace BF2WebAdmin.Common.Communication.Messages
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [MessagePackObject(keyAsPropertyName: true)]
    public class ProjectilePositionEvent : IMessagePayload
    {
        public int ProjectileId { get; set; }
        public string Template { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
    }
}