namespace BF2WebAdmin.Server.Communication;

public enum GameState
{
    PreGame,
    Playing,
    EndGame,
    Paused,
    Restart,
    NotConnected
}

public enum SocketState
{
    Connected,
    Disconnected
}
