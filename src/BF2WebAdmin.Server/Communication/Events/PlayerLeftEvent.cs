namespace BF2WebAdmin.Server.Communication.Events;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
//[MessagePackObject(keyAsPropertyName: true)]
public class PlayerLeftEvent
{
    public int PlayerId { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}