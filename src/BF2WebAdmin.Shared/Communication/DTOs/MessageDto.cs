namespace BF2WebAdmin.Shared.Communication.DTOs;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
//[MessagePackObject(keyAsPropertyName: true)]
public class MessageDto
{
    public int PlayerId { get; set; }
    public string Type { get; set; }
    public string Channel { get; set; }
    public string Flags { get; set; }
    public string Text { get; set; }
}