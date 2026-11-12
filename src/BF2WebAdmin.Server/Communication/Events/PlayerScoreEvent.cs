namespace BF2WebAdmin.Server.Communication.Events;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
//[MessagePackObject(keyAsPropertyName: true)]
public class PlayerScoreEvent
{
    public int PlayerId { get; set; }
    public int TeamScore { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int TotalScore { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}