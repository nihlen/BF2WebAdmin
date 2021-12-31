using BF2WebAdmin.Common.Abstractions;
using BF2WebAdmin.Common.Communication.DTOs;
using MessagePack;
using ProtoBuf;

namespace BF2WebAdmin.Common.Communication.Messages
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [MessagePackObject(keyAsPropertyName: true)]
    public class PlayerKillEvent : IMessagePayload
    {
        public int AttackerId { get; set; }
        public Vector3 AttackerPosition { get; set; }
        public int VictimId { get; set; }
        public Vector3 VictimPosition { get; set; }
        public string Weapon { get; set; }
    }
}