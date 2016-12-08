using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Logging;
using Microsoft.Extensions.Logging;

namespace BF2WebAdmin.Server
{
    public class FakeGameServer
    {
        private static ILogger Logger { get; } = ApplicationLogging.CreateLogger<FakeGameServer>();

        private readonly IPAddress _ipAddress;
        private readonly int _port;

        private StreamReader _reader;
        private StreamWriter _writer;

        public FakeGameServer(IPAddress ipAddress, int port)
        {
            _ipAddress = ipAddress;
            _port = port;
        }

        public async Task Connect()
        {
            // Wait for the listening server to start up
            await Task.Delay(2000);

            using (var client = new TcpClient())
            {
                await client.ConnectAsync(_ipAddress, _port);

                using (var stream = client.GetStream())
                using (_reader = new StreamReader(stream))
                using (_writer = new StreamWriter(stream))
                {
                    _writer.AutoFlush = true;
                    await AddPlayerAsync(1, "Tester", 1, "127.0.1.1", "hash", 1);
                    await SayAsync("ALL", "", 1, ".follow #blacklivesmatter");

                    while (stream.CanRead)
                    {
                        var message = await _reader.ReadLineAsync();
                        Logger.LogInformation($"Received: {message}");
                    }
                }
            }
        }

        private async Task AddPlayerAsync(int index, string name, int pid, string ipAddress, string hash, int teamId)
        {
            await _writer.WriteLineAsync($"playerConnect\t{index}\t{name}\t{pid}\t{ipAddress}\t{hash}\t{teamId}");
        }

        private async Task SayAsync(string channel, string flags, int index, string text)
        {
            await _writer.WriteLineAsync($"chatPlayer\t{channel}\t{flags}\t{index}\t{text}");
        }
    }
}