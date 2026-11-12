using BF2WebAdmin.Server.Communication.DTOs;

namespace BF2WebAdmin.Server.Communication.Events;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
//[MessagePackObject(keyAsPropertyName: true)]
public class MapChangeEvent
{
    public string Map { get; set; }
    public int Size { get; set; }
    public int Index { get; set; }
    public IEnumerable<TeamDto> Teams { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}