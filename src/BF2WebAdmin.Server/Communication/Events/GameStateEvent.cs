namespace BF2WebAdmin.Server.Communication.Events;

public class GameStateEvent
{
    public GameState State { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}