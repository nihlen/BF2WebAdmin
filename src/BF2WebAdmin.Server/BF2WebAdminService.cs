using System.Net;
using Microsoft.Extensions.Options;
using Nihlen.Common.Telemetry;

namespace BF2WebAdmin.Server;

public class BF2WebAdminService : BackgroundService
{
    private readonly ISocketServer _server;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<BF2WebAdminService> _logger;
    private readonly ServerSettings _settings;

    public BF2WebAdminService(IOptions<ServerSettings> settings, ISocketServer server, IHostApplicationLifetime applicationLifetime, ILoggerFactory loggerFactory)
    {
        _server = server;
        _applicationLifetime = applicationLifetime;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<BF2WebAdminService>();
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await WaitForActivityListenersAsync(stoppingToken);

        using var activity = Telemetry.ActivitySource.StartActivity(nameof(BF2WebAdminService));

        var listenTask = _server.StartAsync(stoppingToken);

        // Create a fake game server that connects
        // TODO: move to a separate container image
        var fakeGameServerTask = Task.CompletedTask;
        if (_settings.StartFakeGameServer)
        {
            // TODO: Config file
            var fakeGameServer = new FakeGameServer(
                IPAddress.Loopback,
                _settings.Port,
                _settings.GameServers[0].RconPort,
                @"C:\Projects\DotNet\BF2WebAdmin\src\BF2WebAdmin.Server\bin\Debug\netcoreapp2.0\gameevents-31-220-7-51-0-1515868847.txt",
                _loggerFactory.CreateLogger<FakeGameServer>(),
                0, //475_617
                stoppingToken
            ); // 2v2 start?

            fakeGameServerTask = fakeGameServer.ConnectAsync();
        }

        try
        {
            await Task.WhenAll(listenTask, fakeGameServerTask);
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
    public int Port { get; set; }
    public List<ServerInfo> GameServers { get; set; }
    public bool StartFakeGameServer { get; set; }
    public string ServerLogDirectory { get; set; }
    public bool PrintSendLog { get; set; }
    public bool PrintRecvLog { get; set; }
    public bool ForceHttps { get; set; } = true;
}

public class ServerInfo
{
    public string IpAddress { get; set; }
    public int GamePort { get; set; }
    public int QueryPort { get; set; }
    public int RconPort { get; set; }
    public string RconPassword { get; set; }
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
