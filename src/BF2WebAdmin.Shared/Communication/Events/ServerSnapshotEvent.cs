using BF2WebAdmin.Shared.Communication.DTOs;

namespace BF2WebAdmin.Shared.Communication.Events;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
//[MessagePackObject(keyAsPropertyName: true)]
public class ServerSnapshotEvent : IMessagePayload
{
    // TODO: unnecessary?
    public ServerDto Server { get; set; }
    public IEnumerable<MapDto> Maps { get; set; }
    public IEnumerable<TeamDto> Teams { get; set; }
    public IEnumerable<PlayerDto> Players { get; set; }
    public IEnumerable<ChatLogDto> ChatLog { get; set; }
    public IEnumerable<EventLogDto> EventLog { get; set; }
}