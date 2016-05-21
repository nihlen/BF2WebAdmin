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
    class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();

            var server = new BF2SocketServer();
            server.Start();

            try
            {
                BF2Rcon.SendCommand(IPAddress.Parse("127.0.0.1"), 4711, "secret", "wa connect");
            }
            catch (Exception e)
            {
                // SocketException
                Log.Error(e.Message);
            }

            Console.ReadLine();
        }
    }
}
