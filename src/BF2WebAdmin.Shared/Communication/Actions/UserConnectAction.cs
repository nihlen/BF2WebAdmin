namespace BF2WebAdmin.Shared.Communication.Actions;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
//[MessagePackObject(keyAsPropertyName: true)]
public class UserConnectAction : IMessagePayload
{
    public string Id { get; set; }
}