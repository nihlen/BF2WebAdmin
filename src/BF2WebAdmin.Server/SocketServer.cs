using System;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BF2WebAdmin.Server.Entities;
using log4net;

namespace BF2WebAdmin.Server
{
    public class SocketServer
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly ConcurrentDictionary<IPEndPoint, TcpClient> _connections;

        public SocketServer()
        {
            _connections = new ConcurrentDictionary<IPEndPoint, TcpClient>();
        }

        public async Task ListenAsync()
        {
            var server = StartTcpListener();
            Log.Info("Server started");
            var tasks = new List<Task>();

            while (server.Server.IsBound)
            {
                try
                {
                    var client = await server.AcceptTcpClientAsync();
                    _connections.TryAdd(GetIpEndPoint(client), client);

                    var task = HandleConnectionAsync(client);
                    tasks.Add(task);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }

            Log.Info("Server stopped");
            await Task.WhenAll(tasks);
        }

        private static TcpListener StartTcpListener()
        {
            var server = new TcpListener(IPAddress.Parse("127.0.0.1"), 4300);
            server.Start();
            return server;
        }

        private async Task HandleConnectionAsync(TcpClient client)
        {
            var ipEndPoint = GetIpEndPoint(client);

            Log.Info($"Client {ipEndPoint.Address}:{ipEndPoint.Port} connected");

            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new BinaryWriter(stream))
            {
                var gameWriter = new GameWriter(writer);
                var gameServer = new GameServer(ipEndPoint.Address, gameWriter);
                var eventHandler = new EventHandler(gameServer);
                var eventParser = new EventParser(gameServer, eventHandler);

                while (client.Connected)
                {
                    try
                    {
                        var message = await reader.ReadLineAsync();
                        if (message.StartsWith("playerPos")) continue; // TODO: temp ignore spam
                        Log.Debug($"recv: {message}");
                        eventParser.ParseMessage(message);
                    }
                    catch (IOException ex) when (ex.InnerException is SocketException)
                    {
                        // Disconnected
                    }
                    catch (NotImplementedException)
                    {
                        Log.Error("Not implemented!");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                    }
                }
            }

            TcpClient removed;
            _connections.TryRemove(ipEndPoint, out removed);
            Log.Info($"Client {ipEndPoint.Address}:{ipEndPoint.Port} disconnected");
        }

        private static IPEndPoint GetIpEndPoint(TcpClient client)
        {
            var result = client.Client.RemoteEndPoint as IPEndPoint;
            if (result == null)
                throw new InvalidCastException();

            return result;
        }
    }
}
