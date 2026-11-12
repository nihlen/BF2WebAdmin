using BF2WebAdmin.Server.Communication.DTOs;

namespace BF2WebAdmin.Server.Communication.Events;

public class PlayerPositionEvent
{
    public int PlayerId { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public int Ping { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}