namespace BF2WebAdmin.Server.Communication.DTOs;

/// <summary>A server's current state: the start point that the server hub's events then update.</summary>
public class ServerSnapshotDto
{
    public required ServerDto Server { get; set; }
    public required List<MapDto> Maps { get; set; }
    public required List<TeamDto> Teams { get; set; }
    public required List<PlayerDto> Players { get; set; }
    public required List<ChatLogDto> ChatLog { get; set; }
    public required List<EventLogDto> EventLog { get; set; }
}
