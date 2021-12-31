using System.Net;
using BF2WebAdmin.Server.Extensions;
using Microsoft.Extensions.Options;
using Serilog;

namespace BF2WebAdmin.Server
{
    // Commands: .whois .autopad .follow @user .follow #hashtag .nasa blurs .shuffle teams/teleport .vote mg/nasa, teleports on the map? (position)
    // Auth: Steam
    public class BF2WebAdminService : IHostedService
    {
        private readonly ISocketServer _server;

        private readonly ServerSettings _settings;
        //private static ILogger Logger { get; } = ApplicationLogging.CreateLogger<Program>();

        //public static void Main(string[] args) => MainAsync().GetAwaiter().GetResult();

        public BF2WebAdminService(IOptions<ServerSettings> settings, ISocketServer server)
        {
            _server = server;
            _settings = settings.Value;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // create service collection
            //var settings = GetSettings();

            // Listen for game servers
            //var allowedServers = settings.GameServers?.Select(s => s.IpAddress).ToList() ?? Enumerable.Empty<string>();
            //var serverIp = await _settings.IpAddress.GetIpAddressAsync();
            //var server = new SocketServer(serverIp, _settings.Port, _settings.GameServers, _settings.PrintSendLog, _settings.PrintRecvLog);
            var listenTask = _server.ListenAsync();

            // Create a fake game server that connects
            var fakeGameServerTask = Task.CompletedTask;
            if (_settings.StartFakeGameServer)
            {
                // TODO: Config file
                //var fakeGameServer = new FakeGameServer(IPAddress.Loopback, settings.Port, settings.GameServers[0].RconPort, @"C:\Projects\DotNet\BF2WebAdmin\src\BF2WebAdmin.Server\bin\Debug\netcoreapp2.0\gameevents-31-220-7-51-0-1515868847.txt", 4_500_000); // ?
                //var fakeGameServer = new FakeGameServer(IPAddress.Loopback, settings.Port, settings.GameServers[0].RconPort, @"C:\Projects\DotNet\BF2WebAdmin\src\BF2WebAdmin.Server\bin\Debug\netcoreapp2.0\gameevents-31-220-7-51-0-1515868847.txt", 6_700_000); // classic draw?
                //var fakeGameServer = new FakeGameServer(IPAddress.Loopback, settings.Port, settings.GameServers[0].RconPort, @"C:\Projects\DotNet\BF2WebAdmin\src\BF2WebAdmin.Server\bin\Debug\netcoreapp2.0\gameevents-31-220-7-51-0-1515868847.txt", 1_000_000); // 2v2 action?
                //var fakeGameServer = new FakeGameServer(IPAddress.Loopback, settings.Port, settings.GameServers[0].RconPort, @"C:\Projects\DotNet\BF2WebAdmin\src\BF2WebAdmin.Server\bin\Debug\netcoreapp2.0\gameevents-31-220-7-51-0-1515868847.txt", 524_308); // 2v2 action?
                var fakeGameServer = new FakeGameServer(
                    IPAddress.Loopback,
                    _settings.Port,
                    _settings.GameServers[0].RconPort,
                    @"C:\Projects\DotNet\BF2WebAdmin\src\BF2WebAdmin.Server\bin\Debug\netcoreapp2.0\gameevents-31-220-7-51-0-1515868847.txt",
                    475_617
                ); // 2v2 start?
                //var fakeGameServer = new FakeGameServer(IPAddress.Loopback, settings.Port, settings.GameServers[0].RconPort, @"C:\Projects\DotNet\BF2WebAdmin\src\BF2WebAdmin.Server\bin\Debug\netcoreapp2.0\gameevents-31-220-7-51-0-1515868847.txt", 0);
                fakeGameServerTask = fakeGameServer.ConnectAsync();
            }

            var connectToGameServersTask = Task.CompletedTask;
            if (!_settings.GameServers.IsNullOrEmpty())
            {
                // Wait for the server to start up TODO: callback when ready instead
                //await Task.Delay(15000);

                connectToGameServersTask = Task.WhenAll(_settings.GameServers.Select(serverInfo => _server.RetryConnectionAsync(_server.GetIpAddress(), _settings.Port, serverInfo)));
                //connectToGameServersTask = ConnectToGameServersAsync(serverIp, settings.Port, settings.GameServers);
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

            // Block this task until the program is closed.
            //await Task.Delay(-1);        }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            // TODO: shutdown gracefully with cancellationtokens? D:
        }

        //private static ServerSettings GetSettings()
        //{
        //    // TODO: inject instead
        //    var serviceCollection = new ServiceCollection();
        //    serviceCollection.AddOptions();
        //    var config = BuildConfiguration();
        //    serviceCollection.Configure<ServerSettings>(config.GetSection("ServerSettings"));
        //    var services = serviceCollection.BuildServiceProvider();
        //    var settings = services.GetService<IOptions<ServerSettings>>();
        //    if (settings is null)
        //        throw new Exception("No ServerSettings found");

        //    return settings.Value;
        //}

        //private static IConfiguration BuildConfiguration()
        //{
        //    // TODO: inject instead
        //    var configPath = Path.Combine(ProjectContext.GetDirectory(), "Configuration");
        //    var profile = ProjectContext.GetEnvironmentName();
        //    Log.Information("Current profile: {profile}", profile);

        //    var builder = new ConfigurationBuilder()
        //        .SetBasePath(configPath)
        //        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        //        .AddJsonFile($"appsettings.{profile}.json", optional: false, reloadOnChange: false)
        //        .AddJsonFile("appsecrets.json", optional: false, reloadOnChange: false)
        //        .AddJsonFile($"appsecrets.{profile}.json", optional: false, reloadOnChange: false);

        //    return builder.Build();
        //}

        //private static Task ConnectToGameServersAsync(IPAddress ipAddress, int port, IEnumerable<ServerInfo> gameServers)
        //{
        //    var tasks = gameServers.Select(serverInfo => SocketServer.AttemptConnectionAsync(ipAddress, port, serverInfo));
        //    return Task.WhenAll(tasks);
        //}
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
}
