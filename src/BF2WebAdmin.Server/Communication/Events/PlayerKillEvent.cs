using BF2WebAdmin.Server.Communication.DTOs;

namespace BF2WebAdmin.Server.Communication.Events;

public class PlayerKillEvent
{
    public int AttackerId { get; set; }
    public Vector3 AttackerPosition { get; set; }
    public int VictimId { get; set; }
    public Vector3 VictimPosition { get; set; }
    public string Weapon { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}