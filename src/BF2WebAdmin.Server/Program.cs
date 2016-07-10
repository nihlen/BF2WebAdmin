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
            var server = new SocketServer();
            var task = server.ListenAsync();

            try
            {
                using (var client = new RconClient(IPAddress.Parse("127.0.0.1"), 4711, "secret"))
                {
                    await client.SendAsync("wa connect");
                }
            }
            catch (Exception ex)
            {
                // SocketException
                Logger.LogError(ex.Message);
            }

            try
            {
                await task;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }
        }
    }
}
