using BF2WebAdmin.Common.External;
using BF2WebAdmin.Server.AdminChannel;
using Microsoft.Extensions.Options;

namespace BF2WebAdmin.Server;

public class BF2WebAdminService : BackgroundService
{
    private readonly AdminListener _listener;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<BF2WebAdminService> _logger;
    private readonly ServerSettings _settings;

    public BF2WebAdminService(IOptions<ServerSettings> settings, AdminListener listener, IHostApplicationLifetime applicationLifetime, ILoggerFactory loggerFactory)
    {
        _listener = listener;
        _applicationLifetime = applicationLifetime;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<BF2WebAdminService>();
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await WaitForActivityListenersAsync(stoppingToken);

        using var activity = Telemetry.ActivitySource.StartActivity(nameof(BF2WebAdminService));

        try
        {
            await _listener.RunAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            // Stop the application if the main service fails
            // The container will restart automatically if configured
            _logger.LogCritical(ex, "Service failed");
            _applicationLifetime.StopApplication();
        }

        async Task WaitForActivityListenersAsync(CancellationToken cancellationToken)
        {
            // The activity listeners take around 300 ms to be added in debug mode
            // We want to delay server connections until this is done to get the initial traces
            // There should be a better way to do this though...
            for (var i = 0; i < 20 && !Telemetry.ActivitySource.HasListeners(); i++)
            {
                await Task.Delay(100, cancellationToken);
            }

            if (!Telemetry.ActivitySource.HasListeners())
                _logger.LogWarning("No activity listeners found after waiting 2 s");
        }
    }
}

public class ServerSettings
{
    public string IpAddress { get; set; }

    /// <summary>The OpenBF2 serverext admin channel listener.</summary>
    public AdminChannelSettings AdminChannel { get; set; } = new();
    public List<ServerInfo> GameServers { get; set; }
    public string ServerLogDirectory { get; set; }
    public bool ForceHttps { get; set; } = true;

    /// <summary>The name in brackets before WebAdmin's own chat lines; empty for none. Read by <see cref="Entities.GameServer"/>.</summary>
    public string ChatPrefix { get; set; } = BF2WebAdmin.Server.AdminChannel.AdminGameWriter.DefaultChatPrefix;
}

public class ServerInfo
{
    private string? _serverId;

    /// <summary>Server.ServerId, the module's adminServerId. Defaults to "IpAddress:GamePort" for servers from the config file.</summary>
    public string ServerId
    {
        get => _serverId ?? $"{IpAddress}:{GamePort}";
        set => _serverId = value;
    }

    /// <summary>The admin channel key (hex), as serverext.cfg's adminKey.</summary>
    public string? AdminKey { get; set; }

    /// <summary>Players without a CD-key hash are keyed by name (decision B6).</summary>
    public bool IsTestServer { get; set; }

    public string IpAddress { get; set; }
    public int GamePort { get; set; }
    public int QueryPort { get; set; }
    public DiscordBotConfig DiscordBot { get; set; }
    public string ServerGroup { get; set; }

    public class DiscordBotConfig
    {
        public string Token { get; set; }
        public string AdminChannel { get; set; }
        public string NotificationChannel { get; set; }
        public string MatchResultChannel { get; set; }
    }
}
