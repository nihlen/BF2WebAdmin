namespace BF2WebAdmin.Shared.Communication.Events;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
//[MessagePackObject(keyAsPropertyName: true)]
public class GameStateEvent : IMessagePayload
{
    public GameState State { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}