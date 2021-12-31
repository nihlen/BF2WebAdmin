using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace TestSocketServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting");
            var server = new SocketServer(IPAddress.Any, 4300);
            await server.ListenAsync();
            Console.WriteLine("Finished");
            Console.ReadLine();
        }
    }

    public class SocketServer
    {
        private readonly IPAddress _ipAddress;
        private readonly int _port;
        private readonly ConcurrentDictionary<IPEndPoint, TcpClient> _connections;

        public SocketServer(IPAddress ipAddress, int port)
        {
            _ipAddress = ipAddress;
            _port = port;
            _connections = new ConcurrentDictionary<IPEndPoint, TcpClient>();
        }

        private void LogInformation(string message)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
        }

        private void LogError(Exception ex, string message)
        {
            Console.Error.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message} {ex}");
        }

        public async Task ListenAsync()
        {
            var server = StartTcpListener();
            LogInformation("Server started");
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
                    LogError(ex, "Game server TCP error");
                }
            }

            LogInformation("Server stopped");

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                LogError(ex, "Handle connection error");
            }
        }

        private TcpListener StartTcpListener()
        {
            var server = new TcpListener(_ipAddress, _port);
            server.Start();
            return server;
        }

        private async Task HandleConnectionAsync(TcpClient client)
        {
            var ipEndPoint = GetIpEndPoint(client);

            LogInformation($"Client {ipEndPoint.Address}:{ipEndPoint.Port} connected");

            try
            {
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream))
                using (var writer = new BinaryWriter(stream))
                {
                    // TODO: handle reconnections without recreating GameServer, just new reader/writes and update socket state
                    //IGameWriter gameWriter = new GameWriter(writer);
                    //IGameServer gameServer = await GameServer.CreateAsync(ipEndPoint.Address, gameWriter);
                    //IGameReader gameReader = new GameReader(gameServer);
                    // Log all game events
                    //IGameReader gameReader = new GameReader(gameServer, $"{Path.Combine(AppContext.BaseDirectory, $"gameevents-{gameServer.Id.Replace(".", "-").Replace(":", "-")}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.txt")}");

                    while (client.Connected && stream.CanRead)
                    {
                        try
                        {
                            var message = await reader.ReadLineAsync();
                            if (message == null)
                            {
                                // BF2 server returns NULL when disconnecting
                                // Happens when restarting or connecting to a different instance
                                LogInformation("Null message received");
                                break;
                            }

                            LogInformation($"recv: {message}");

                            //if (message.StartsWith("playerPos")) continue; // TODO: temp ignore spam
                            //if (!message.StartsWith("playerPos") && !message.StartsWith("projectilePos"))
                            //    LogInformation($"recv: {message}");
                            //if (!message.Contains("255"))
                            //    gameReader.ParseMessage(message);
                        }
                        catch (IOException ex) when (ex.InnerException is SocketException)
                        {
                            // Disconnected
                            LogInformation("Disconnected");
                        }
                        catch (NotImplementedException)
                        {
                            //Logger.LogDebug("Not implemented!");
                        }
                        catch (Exception ex)
                        {
                            LogError(ex, "Error while handling server message");
                        }
                    }

                    LogInformation("CLOSED");

                    //gameServer.UpdateSocketState(SocketState.Disconnected);
                }
            }
            catch (Exception ex)
            {
                LogError(ex, "Error while handling connection");
            }

            _connections.TryRemove(ipEndPoint, out var removed);
            LogInformation($"Client {ipEndPoint.Address}:{ipEndPoint.Port} disconnected");
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
