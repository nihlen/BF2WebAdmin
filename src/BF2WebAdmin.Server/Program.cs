using System;
using System.Net;
using System.Threading.Tasks;
using BF2WebAdmin.Server.Logging;
using Microsoft.Extensions.Logging;

namespace BF2WebAdmin.Server
{
    // Commands: .whois .autopad .follow @user .follow #hashtag .nasa blurs .shuffle teams/teleport .vote mg/nasa, teleports on the map? (position)
    // Auth: Steam
    class Program
    {
        private static ILogger Logger { get; } = ApplicationLogging.CreateLogger<Program>();

        static void Main(string[] args)
        {
            var task = RunAsync();
            task.Wait();
        }

        private static async Task RunAsync()
        {
            var ipAddress = IPAddress.Parse("127.0.0.1");
            var port = 4300;

            // Listen for game servers
            var server = new SocketServer(ipAddress, port);
            var listenTask = server.ListenAsync();

            // Create a fake game server that connects
            //var fakeGameServer = new FakeGameServer(ipAddress, port);
            //var fakeTask = fakeGameServer.Connect();

           try
            {
                using (var client = new RconClient(IPAddress.Parse("127.0.0.1"), 4711, "secret"))
                {
                    await client.SendAsync($"wa connect {ipAddress} {port}");
                }
            }
            catch (Exception ex)
            {
                // SocketException
                Logger.LogError(ex.Message);
            }

            try
            {
                await listenTask;
                //await fakeTask;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }
        }
    }
}
