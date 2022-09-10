namespace BF2WebAdmin.Shared.Communication.DTOs;

public class ServerDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string IpAddress { get; set; }
    public int GamePort { get; set; }
    public int QueryPort { get; set; }
    public string Map { get; set; }
    public int Players { get; set; }
    public int MaxPlayers { get; set; }
    public GameState GameState { get; set; }
    public SocketState SocketState { get; set; }
    public string ServerGroup { get; set; }
}
