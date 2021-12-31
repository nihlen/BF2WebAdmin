using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using Serilog;

namespace BF2WebAdmin.Server
{
    public class GameWriter : IGameWriter
    {
        //private static ILogger Logger { get; } = ApplicationLogging.CreateLogger<GameWriter>();

        private readonly BinaryWriter _writer;
        private readonly Channel<string> _gameMessageChannel;
        private readonly bool _logSend;
        private int _responseCounter;

        private readonly Encoding _encoding;
        private readonly byte _delimiter;

        private DateTime StartDate;

        public double CurrentTrackerInterval { get; private set; } = 300;

        public GameWriter(BinaryWriter writer, bool logSend)
        {
            _writer = writer;
            _logSend = logSend;

            // Battlefield 2 does not support UTF-8, only what seems like Windows-1252
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _encoding = Encoding.GetEncoding(1252);
            _delimiter = _encoding.GetBytes("\n").Single();

            // Set invariant culture
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            StartDate = DateTime.UtcNow;

            _gameMessageChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = true,
                SingleReader = true,
                SingleWriter = true
            });

            _ = Task.Run(SendAllMessagesAsync);
        }

        private void Send(string message)
        {
            if (_writer == null || !_writer.BaseStream.CanWrite)
                throw new IOException($"Cannot write to {_writer}");

            _gameMessageChannel.Writer.TryWrite(message);

            // TODO: Batch send?
            //try
            //{
            //    if (_logSend)
            //    {
            //        Logger.LogDebug($"send: {message}");
            //    }
            //    //var bytes = Encoding.UTF8.GetBytes(message + "\n");
            //    var bytes = _encoding.GetBytes(message + "\n");
            //    lock (_writer)
            //    {
            //        _writer.Write(bytes);
            //    }
            //}
            //catch (Exception ex)
            //{
            //    Logger.LogError(ex, "Write error for server");
            //}
        }

        private async Task SendAllMessagesAsync()
        {
            // TODO: Batch send?
            // Parse all messages written to the channel asynchronously
            await foreach (var message in _gameMessageChannel.Reader.ReadAllAsync())
            {
                try
                {
                    if (_logSend)
                    {
                        Log.Debug("send: {message}", message);
                    }

                    // Easy method
                    //var bytes = _encoding.GetBytes(message + "\n");
                    //_writer.Write(bytes);

                    // Memory efficient method?
                    var bufferSize = _encoding.GetMaxByteCount(message.Length + 1);
                    using var memory = MemoryPool<byte>.Shared.Rent(bufferSize);
                    var encodedBytes = _encoding.GetBytes(message.AsSpan(), memory.Memory.Span);
                    memory.Memory.Span[encodedBytes] = _delimiter;
                    _writer.Write(memory.Memory.Span[..(encodedBytes + 1)]);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Write error for server");
                }
            }
        }

        /// <summary>
        /// Run RCon command
        /// </summary>
        /// <param name="commands">Battlefield 2 RCon commands</param>
        public void SendRcon(params string[] commands)
        {
            foreach (var command in commands)
                Send($"rcon {command}");
        }

        private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingRconResponses = new ConcurrentDictionary<string, TaskCompletionSource<string>>();

        /// <summary>
        /// Run RCon command and get response
        /// </summary>
        /// <param name="command">Battlefield 2 rcon command</param>
        public Task<string> GetRconResponseAsync(string command)
        {
            string responseCode = null;
            try
            {
                responseCode = "response" + _responseCounter++;
                var taskCompletionSource = new TaskCompletionSource<string>();
                _pendingRconResponses.TryAdd(responseCode, taskCompletionSource);

                var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                cancellationTokenSource.Token.Register(() =>
                {
                    taskCompletionSource.TrySetCanceled();
                    _pendingRconResponses.TryRemove(responseCode, out _);
                }, useSynchronizationContext: false);

                Send($"rconresponse {responseCode} {command}");

                return taskCompletionSource.Task;
            }
            finally
            {
                _responseCounter %= 10_000;
            }
        }

        /// <summary>
        /// Callback method to set the rconresponse from GameReader
        /// </summary>
        /// <param name="message">Full message response</param>
        public void SetRconResponse(string responseCode, string value)
        {
            var exists = _pendingRconResponses.TryGetValue(responseCode, out var pendingResponse);
            if (!exists || pendingResponse == null)
            {
                Log.Information("No responseCode {responseCode} found", responseCode);
                return;
            }

            Log.Information("RCON response {responseCode} {value}", responseCode, value);

            pendingResponse.TrySetResult(value);
        }

        /// <summary>
        /// PM a player using the rcon feedback function (message appears in their console)
        /// </summary>
        /// <param name="player">Player on the server to send to</param>
        /// <param name="message">Message</param>
        public void SendPrivateMessage(Player player, string message)
        {
            Send($"pm {player.Index} {message}");
        }

        /// <summary>
        /// Teleport a player
        /// </summary>
        /// <param name="player">Player to teleport</param>
        /// <param name="position">New position</param>
        public void SendTeleport(Player player, Position position)
        {
            Send($"position {player.Index} {position.X:0.000} {position.Height:0.000} {position.Y:0.000}");
        }

        /// <summary>
        /// Rotate a player
        /// </summary>
        /// <param name="player">Player to rotate</param>
        /// <param name="rotation">New rotation</param>
        public void SendRotate(Player player, Rotation rotation)
        {
            Send($"rotation {player.Index} {rotation.Yaw:0.000} {rotation.Pitch:0.000} {rotation.Roll:0.000}");
        }

        /// <summary>
        /// Set player health (vehicle damage)
        /// </summary>
        /// <param name="player">Player to set health on</param>
        /// <param name="health">New health (range varies)</param>
        public void SendHealth(Player player, int health)
        {
            Send($"damage {player.Index} {health}");
        }

        /// <summary>
        /// Set player rank with optional event
        /// </summary>
        /// <param name="player">Player to change rank on</param>
        /// <param name="rank">New rank 0-21</param>
        /// <param name="rankEvent">Whether to send a rank up event or not</param>
        public void SendRank(Player player, Rank rank, bool rankEvent)
        {
            var evnt = rankEvent ? 1 : 0;
            Send($"rank {player.Index} {(int)rank} {evnt}");
        }

        /// <summary>
        /// Give player a medal award
        /// </summary>
        /// <param name="player">Player to award</param>
        /// <param name="medalNumber"></param>
        /// <param name="medalValue"></param>
        public void SendMedal(Player player, int medalNumber, int medalValue)
        {
            Send($"medal {player.Index} {medalNumber} {medalValue}");
        }

        /// <summary>
        /// Send a game event
        /// </summary>
        /// <param name="player">Player to send event to</param>
        /// <param name="eventType"></param>
        /// <param name="data"></param>
        public void SendGameEvent(Player player, int eventType, int data)
        {
            Send($"gameevent {player.Index} {eventType} {data}");
        }

        /// <summary>
        /// Send a HUD event
        /// </summary>
        /// <param name="player">Player to send HUD event to</param>
        /// <param name="eventType"></param>
        /// <param name="data"></param>
        public void SendHudEvent(Player player, int eventType, int data)
        {
            Send($"hudevent {player.Index} {eventType} {data}");
        }

        /// <summary>
        /// Set score
        /// </summary>
        /// <param name="player">Player to change score on</param>
        /// <param name="totalScore">New total score</param>
        /// <param name="teamScore">New team score</param>
        /// <param name="kills">New kill count</param>
        /// <param name="deaths">New death count</param>
        public void SendScore(Player player, int totalScore, int teamScore, int kills, int deaths)
        {
            Send($"score {player.Index} {totalScore} {teamScore} {kills} {deaths}");
        }

        /// <summary>
        /// Change player team
        /// </summary>
        /// <param name="player">Player to change team on</param>
        /// <param name="teamId">New team id</param>
        public void SendTeam(Player player, int teamId)
        {
            Send($"team {player.Index} {teamId}");
        }

        /// <summary>
        /// Send text (convenience function)
        /// </summary>
        /// <param name="text">Text to send to server chat</param>
        public void SendText(string text, bool useServerName = true, bool sanitize = true)
        {
            const int maxLength = 180;

            if (sanitize)
            {
                text = Regex.Replace(text, @"[\r\n\t\[\]]", "", RegexOptions.Compiled);
                text = text.Replace("\"", "'");
            }

            text = text.Length > maxLength ? text.Substring(0, maxLength) + "..." : text;

            if (useServerName)
            {
                text = $"[§C1001netsky§C1001] {text}";
            }

            SendRcon($"game.sayall \"{text}\"");
        }

        /// <summary>
        /// Set position tracker timer update interval (fast timer)
        /// </summary>
        /// <param name="timerInterval">Update interval in seconds</param>
        public void SendTimerInterval(double timerInterval)
        {
            Send($"timerinterval {timerInterval}");
            CurrentTrackerInterval = timerInterval;
        }

        /// <summary>
        /// Socket heartbeat - not needed since the problem was caused by NetMQ queue size being full and blocking
        /// </summary>
        public void SendHeartbeat()
        {
            //SendText("*heartbeat*");
            //Send("noop");
            // physics.gravity
            try
            {
                var sw = Stopwatch.StartNew();
                var response = GetRconResponseAsync("physics.gravity").GetAwaiter().GetResult();
                Log.Information("Heartbeat response: {response} in {elapsedMs} ms", sw.ElapsedMilliseconds);
            }
            catch (Exception e)
            {
                var duration = (DateTime.UtcNow - StartDate).TotalSeconds;
                Log.Warning(e, "Failed to send heartbeat ({duration} s since start)", duration);
            }
        }
    }
}