namespace BF2WebAdmin.Server.Communication.Events;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
//[MessagePackObject(keyAsPropertyName: true)]
public class GameStateEvent
{
    public GameState State { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}