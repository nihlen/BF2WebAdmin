using BF2WebAdmin.Shared.Communication.DTOs;

namespace BF2WebAdmin.Shared.Communication.Events;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
//[MessagePackObject(keyAsPropertyName: true)]
public class PlayerKillEvent : IMessagePayload
{
    public int AttackerId { get; set; }
    public Vector3 AttackerPosition { get; set; }
    public int VictimId { get; set; }
    public Vector3 VictimPosition { get; set; }
    public string Weapon { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}