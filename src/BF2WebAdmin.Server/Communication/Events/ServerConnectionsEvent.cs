using BF2WebAdmin.Server.Communication.DTOs;

namespace BF2WebAdmin.Server.Communication.Events;

/// <summary>The players' connections over a game server's ServerHealth window; it follows the window's <see cref="ServerHealthEvent"/>.</summary>
public class ServerConnectionsEvent
{
    public required string ServerId { get; set; }
    public required ServerConnectionsDto Connections { get; set; }
}
