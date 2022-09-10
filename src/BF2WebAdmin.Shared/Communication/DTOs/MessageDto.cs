namespace BF2WebAdmin.Shared.Communication.DTOs;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
//[MessagePackObject(keyAsPropertyName: true)]
public class MessageDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int? PlayerId { get; set; }
    public string? PlayerName { get; set; }
    public string? TeamName { get; set; }
    public string Type { get; set; }
    public string Channel { get; set; }
    public string Flags { get; set; }
    public string Text { get; set; }
}