using System.Net;
using BF2WebAdmin.Server.Extensions;
using Microsoft.Extensions.Options;
using Serilog;

namespace BF2WebAdmin.Server;

public class BF2WebAdminService : BackgroundService
{
    private readonly ISocketServer _server;

    private readonly ServerSettings _settings;

    public BF2WebAdminService(IOptions<ServerSettings> settings, ISocketServer server)
    {
        _server = server;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // TODO: shutdown gracefully with cancellationtokens? D:
        var listenTask = _server.ListenAsync();

        // Create a fake game server that connects
        var fakeGameServerTask = Task.CompletedTask;
        if (_settings.StartFakeGameServer)
        {
            // TODO: Config file
            var fakeGameServer = new FakeGameServer(
                IPAddress.Loopback,
                _settings.Port,
                _settings.GameServers[0].RconPort,
                @"C:\Projects\DotNet\BF2WebAdmin\src\BF2WebAdmin.Server\bin\Debug\netcoreapp2.0\gameevents-31-220-7-51-0-1515868847.txt",
                0
                //475_617
            ); // 2v2 start?
            fakeGameServerTask = fakeGameServer.ConnectAsync();
        }

        var connectToGameServersTask = Task.CompletedTask;
        if (!_settings.GameServers.IsNullOrEmpty())
        {
            connectToGameServersTask = Task.WhenAll(_settings.GameServers.Select(serverInfo => _server.RetryConnectionAsync(_server.GetIpAddress(), _settings.Port, serverInfo)));
        }

        try
        {
            // TODO: exit when the listenTask fails/finishes so container will restart?
            await Task.WhenAll(listenTask, fakeGameServerTask, connectToGameServersTask);
        }
        catch (Exception ex)
        {
            Log.Error(ex.Message);
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

    public class DiscordBotConfig
    {
        public string Token { get; set; }
        public string AdminChannel { get; set; }
        public string NotificationChannel { get; set; }
        public string MatchResultChannel { get; set; }
    }
}

public class SeqSettings
{
    public string ServerUrl { get; set; }
    public string ApiKey { get; set; }
}