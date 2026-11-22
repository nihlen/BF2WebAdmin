namespace BF2WebAdmin.Server.Communication.DTOs;

public class ServerDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string IpAddress { get; set; }
    public int GamePort { get; set; }
    public int QueryPort { get; set; }
    public string? Map { get; set; }
    public int Players { get; set; }
    public int MaxPlayers { get; set; }
    public GameState GameState { get; set; }
    public SocketState SocketState { get; set; }
    public string ServerGroup { get; set; }

    /// <summary>The running map's game mode (gpm_cq, gpm_ctf, ...); kept as last seen while the server is not connected, null before it has been.</summary>
    public string? GameMode { get; set; }

    /// <summary>When the admin channel last connected; null when it has not since WebAdmin started (see <c>GET /api/admin-channel</c>'s StartedAt).</summary>
    public DateTimeOffset? ConnectedAt { get; set; }

    /// <summary>When the admin channel last closed; null while connected since WebAdmin started, or never connected.</summary>
    public DateTimeOffset? DisconnectedAt { get; set; }

    /// <summary>
    /// The health now (the latest ServerHealth window, its players' connections and the worse of their statuses); null
    /// for a server whose module has sent none. A ServerUpdateEvent goes to every client when its status changes.
    /// </summary>
    public ServerHealthNowDto? Health { get; set; }
}
