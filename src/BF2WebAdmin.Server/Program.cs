using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace BF2WebAdmin.Server
{
    // Commands: .whois .autopad .follow @user .follow #hashtag .nasa blurs .shuffle teams/teleport .vote mg/nasa
    // Auth: Steam
    class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();

            var task = RunAsync();
            task.Wait();
        }

        private static async Task RunAsync()
        {
            var server = new SocketServer();
            var task = server.ListenAsync();

            try
            {
                Rcon.SendCommand(IPAddress.Parse("127.0.0.1"), 4711, "secret", "wa connect");
            }
            catch (Exception ex)
            {
                // SocketException
                Log.Error(ex.Message);
            }

            try
            {
                await task;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
        }
    }
}
