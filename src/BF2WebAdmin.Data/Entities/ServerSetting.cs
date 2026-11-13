
namespace BF2WebAdmin.Data.Entities;

// TODO: fix these tables with proper relations and names
public class Server
{
    public string ServerId { get; set; }
    public string ServerGroup { get; set; }
    public string IpAddress { get; set; }
    public int GamePort { get; set; }
    public int QueryPort { get; set; }

    /// <summary>Unused since WebAdmin stopped using classic rcon (everything goes over the admin channel); the columns stay so existing databases need no change.</summary>
    public int RconPort { get; set; }

    /// <summary>Unused, see <see cref="RconPort"/>.</summary>
    public string RconPassword { get; set; }

    public string DiscordBotToken { get; set; }
    public string DiscordAdminChannel { get; set; }
    public string DiscordNotificationChannel { get; set; }
    public string DiscordMatchResultChannel { get; set; }

    /// <summary>The admin channel key shared with the server's serverext.cfg (adminKey), 32-128 hex digits. No key: the server cannot connect.</summary>
    public string AdminKey { get; set; }

    /// <summary>A test server accepts players without a CD-key hash, keyed by name instead (decision B6).</summary>
    public bool IsTestServer { get; set; }
}

public class ServerModule
{
    public string ServerGroup { get; set; }
    public string Module { get; set; }
    public bool IsEnabled { get; set; }
}

public class ServerPlayerAuth
{
    public string ServerGroup { get; set; }
    public string PlayerHash { get; set; }
    public int AuthLevel { get; set; }
}