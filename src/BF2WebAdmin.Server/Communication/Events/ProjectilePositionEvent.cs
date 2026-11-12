using BF2WebAdmin.Server.Communication.DTOs;

namespace BF2WebAdmin.Server.Communication.Events;

public class ProjectilePositionEvent
{
    public int ProjectileId { get; set; }
    public string Template { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}