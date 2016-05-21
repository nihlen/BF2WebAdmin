using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace BF2WebAdmin.Server
{
    public class BF2SocketServer
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public async void Start()
        {
            var server = StartTcpListener();
            Log.Info("Server started");

            while (server.Server.IsBound)
            {
                var client = await server.AcceptTcpClientAsync();
                HandleBF2ServerConnection(client);
            }
        }

        public TcpListener StartTcpListener()
        {
            var localEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 4300);
            var server = new TcpListener(localEndpoint);
            server.Start();
            return server;
        }

        public async void HandleBF2ServerConnection(TcpClient client)
        {
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new BinaryWriter(stream))
            {
                while (client.Connected)
                {
                    var message = await reader.ReadLineAsync();
                    Log.Debug($"recv: {message}");
                }
            }
        }
    }
}
