namespace BF2WebAdmin.Shared;

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
