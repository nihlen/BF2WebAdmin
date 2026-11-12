namespace BF2WebAdmin.Server.Communication.Events;

public class PlayerTeamEvent
{
    public int PlayerId { get; set; }
    public int TeamId { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}