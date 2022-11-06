using Dapper.Contrib.Extensions;

namespace BF2WebAdmin.Data.Entities;

// TODO: fix these tables with proper relations and names
public class Server
{
    [ExplicitKey] public string ServerId { get; set; }
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

public class ServerModule
{
    [ExplicitKey] public string ServerGroup { get; set; }
    [ExplicitKey] public string Module { get; set; }
    public bool IsEnabled { get; set; }
}

public class ServerPlayerAuth
{
    [ExplicitKey] public string ServerGroup { get; set; }
    [ExplicitKey] public string PlayerHash { get; set; }
    public int AuthLevel { get; set; }
}