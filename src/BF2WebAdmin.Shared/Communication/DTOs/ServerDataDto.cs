namespace BF2WebAdmin.Shared.Communication.DTOs;

public class ServerDataDto
{
    public string ServerId { get; set; }
    public string ServerGroup { get; set; }
    public string IpAddress { get; set; }
    public int GamePort { get; set; }
    public int QueryPort { get; set; }
    public int RconPort { get; set; }
    public string RconPassword { get; set; }
    public string DiscordBotToken { get; set; }
    public string DiscordAdminChannel { get; set; }
    public string DiscordNotificationChannel { get; set; }
    public string DiscordMatchResultChannel { get; set; }
}
