using System.Net;
using System.Net.Sockets;
using System.Text;
using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Modules.BF2;
using Serilog;

namespace BF2WebAdmin.Server;

// TODO: move this to a separate project that can run in its own container?
public class FakeGameServer
{
    private readonly IPAddress _ipAddress;
    private readonly int _port;
    private readonly int _rconPort;
    private readonly string _gameLogPath;
    private readonly int _skip;
    private bool _run;

    private StreamReader _reader;
    private StreamWriter _writer;

    public FakeGameServer(IPAddress ipAddress, int port, int rconPort, string gameLogPath, int skip = 0)
    {
        _ipAddress = ipAddress;
        _port = port;
        _rconPort = rconPort;
        _gameLogPath = gameLogPath;
        _skip = skip;
        _run = true;
    }

    public async Task ConnectAsync()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var rconListener = new TcpListener(IPAddress.Any, _rconPort);
                rconListener.Start();
                while (rconListener.Server.IsBound)
                {
                    var client = await rconListener.AcceptTcpClientAsync();
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await using var stream = client.GetStream();
                            using var reader = new StreamReader(stream);
                            await using var writer = new BinaryWriter(stream);
                            writer.Write(Encoding.UTF8.GetBytes(RconClient.RconResponses.AuthenticationSuccessResponse + "\n"));
                            while (client.Connected && stream.CanRead)
                            {
                                var message = await reader.ReadLineAsync();
                                writer.Write(new byte[] { 0x04 });
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error(e, "Rcon message read failed");
                        }
                    });
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Rcon listener failed");
            }
        });

        // Wait for the listening server to start up
        await Task.Delay(5000);

        _ = Task.Run(async () =>
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(_ipAddress, _port);

                await using var stream = client.GetStream();
                using (_reader = new StreamReader(stream))
                await using (_writer = new StreamWriter(stream))
                {
                    Log.Information("Starting FakeGameServer");
                    _writer.AutoFlush = true;
                    var writeTask = SendGameEvents();
                    //await AddPlayerAsync(1, "Tester", 1, "127.0.1.1", "hash", 1);
                    //await SayAsync("ALL", "", 1, ".follow #metoothanks");

                    while (stream.CanRead)
                    {
                        var message = await _reader.ReadLineAsync();
                        Log.Information("Received: {message}", message);
                    }

                    await writeTask;
                    Log.Information("Stopping FakeGameServer");
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Fake game server failed");
            }
        });
    }

    private bool skipFinished = false;

    // Log format
    // <TimestampDiffInMs> <GameEvent>
    private async Task SendGameEvents()
    {
        DiscordModule.IsEnabled = false;
        var lines = await File.ReadAllLinesAsync(_gameLogPath);
        Log.Information("Read {lines} game events from file", lines.Length);

        //lines = lines.Skip(7445).ToArray(); // skip until 2v2 action - there will be mismatch though

        while (_run)
        {
            var startTime = DateTime.UtcNow;
            var lineNumber = 1;
            foreach (var line in lines)
            {
                //if (line.Contains("playerPositionUpdate") || line.Contains("projectilePositionUpdate"))
                //    continue;

                var diff = (int)(DateTime.UtcNow - startTime).TotalMilliseconds + _skip;
                var parts = line.Split(" ", 2);
                var timestamp = int.Parse(parts[0]);
                //Logger.LogDebug($"Next {timestamp} ms: {parts[1]}");

                if (timestamp > diff && timestamp > _skip)
                {
                    if (!skipFinished)
                    {
                        skipFinished = true;
                        DiscordModule.IsEnabled = true; // don't spam discord
                        Log.Information("Skip finished");
                    }

                    // TODO: tryout spinwait and see if it's more accurate and not too cpu intensive
                    //SpinWait.SpinUntil(() => sw.ElapsedMilliseconds > snapshot.Item1, 1000);
                    await MultimediaTimer.Delay(timestamp - diff);
                    //await Task.Delay(timestamp - diff);
                }

                lineNumber++;
                //if (line.Contains("playerPosition"))
                //    continue;

                //Log.Debug("{Line} Sending... {Message}", lineNumber, parts[1]);
                await _writer.WriteLineAsync(parts[1]);
            }
        }
    }

    //private async Task SendServerInfo(int index, string name, int pid, string ipAddress, string hash, int teamId)
    //{
    //    await _writer.WriteLineAsync($"playerConnect\t{index}\t{name}\t{pid}\t{ipAddress}\t{hash}\t{teamId}");
    //}

    //private async Task AddPlayerAsync(int index, string name, int pid, string ipAddress, string hash, int teamId)
    //{
    //    await _writer.WriteLineAsync($"playerConnect\t{index}\t{name}\t{pid}\t{ipAddress}\t{hash}\t{teamId}");
    //}

    //private async Task SayAsync(string channel, string flags, int index, string text)
    //{
    //    await _writer.WriteLineAsync($"chatPlayer\t{channel}\t{flags}\t{index}\t{text}");
    //}
}