namespace BF2WebAdmin.Server.Communication.Events;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
//[MessagePackObject(keyAsPropertyName: true)]
public class PlayerTeamEvent
{
    public int PlayerId { get; set; }
    public int TeamId { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}