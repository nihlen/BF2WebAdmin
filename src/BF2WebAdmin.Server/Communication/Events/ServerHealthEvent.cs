using BF2WebAdmin.Server.Communication.DTOs;

namespace BF2WebAdmin.Server.Communication.Events;

/// <summary>A game server's new ServerHealth window (one per 10 s while its module sends them).</summary>
public class ServerHealthEvent
{
    public required string ServerId { get; set; }
    public required ServerHealthWindowDto Window { get; set; }
}
