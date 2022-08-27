namespace BF2WebAdmin.Common.Entities.Game;

public class Message
{
    public Player Player { get; set; }
    public MessageType Type { get; set; }
    public string Channel { get; set; }
    public string Flags { get; set; }

    private string _text;
    public string Text
    {
        get { return _text; }
        set
        {
            _text = value.Replace("�", "§").Replace("§C1001", "").Replace("§3", "").Trim();
        }
    }
}

public class ChatChannel
{
    public const string Squad = "Squad";
    public const string Team = "Team";
    public const string Global = "Global";
    public const string ServerTeamMessage = "ServerTeamMessage";
    public const string ServerMessage = "ServerMessage";
}

public enum MessageType
{
    Server,
    Player
}