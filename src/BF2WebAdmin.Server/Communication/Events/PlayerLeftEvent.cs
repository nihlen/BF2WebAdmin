namespace BF2WebAdmin.Server.Communication.Events;

public class PlayerLeftEvent
{
    public int PlayerId { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}