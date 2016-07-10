using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Entities;
using BF2WebAdmin.Server.Logging;
using Microsoft.Extensions.Logging;

namespace BF2WebAdmin.Server
{
    public class SocketServer
    {
        private static ILogger Logger { get; } = ApplicationLogging.CreateLogger<SocketServer>();

        private readonly ConcurrentDictionary<IPEndPoint, TcpClient> _connections;

        public SocketServer()
        {
            _connections = new ConcurrentDictionary<IPEndPoint, TcpClient>();
        }

        public async Task ListenAsync()
        {
            var server = StartTcpListener();
            Logger.LogInformation("Server started");
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
                    Logger.LogError("Game server TCP error", ex);
                }
            }

            Logger.LogInformation("Server stopped");

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Logger.LogError("Handle connection error", ex);
            }
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

            Logger.LogInformation($"Client {ipEndPoint.Address}:{ipEndPoint.Port} connected");

            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new BinaryWriter(stream))
            {
                IGameWriter gameWriter = new GameWriter(writer);
                IGameServer gameServer = new GameServer(ipEndPoint.Address, gameWriter);
                IGameReader gameReader = new GameReader(gameServer);

                while (client.Connected)
                {
                    try
                    {
                        var message = await reader.ReadLineAsync();
                        if (message.StartsWith("playerPos")) continue; // TODO: temp ignore spam
                        Logger.LogDebug($"recv: {message}");
                        gameReader.ParseMessage(message);
                    }
                    catch (IOException ex) when (ex.InnerException is SocketException)
                    {
                        // Disconnected
                    }
                    catch (NotImplementedException)
                    {
                        Logger.LogDebug("Not implemented!");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Error while handling server message", ex);
                    }
                }
            }

            TcpClient removed;
            _connections.TryRemove(ipEndPoint, out removed);
            Logger.LogInformation($"Client {ipEndPoint.Address}:{ipEndPoint.Port} disconnected");
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
