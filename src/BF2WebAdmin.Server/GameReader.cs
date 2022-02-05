using System.Diagnostics;
using System.Threading.Channels;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using Serilog;

namespace BF2WebAdmin.Server
{
    public class GameReader : IGameReader
    {
        private readonly IGameServer _gameServer;
        private readonly string _gameLogPath;
        private readonly DateTime _startTime;
        private readonly Dictionary<string, Func<string[], DateTimeOffset, ValueTask>> _eventHandlers;
        private readonly Stopwatch _messageStopWatch;
        private readonly Channel<(string, DateTimeOffset)> _gameEventChannel;

        public GameReader(IGameServer gameServer, string gameLogPath = null)
        {
            var eventHandler = new EventHandler(gameServer);
            _eventHandlers = GetEventsHandlers(eventHandler);
            _startTime = DateTime.UtcNow;
            _gameServer = gameServer;
            _gameLogPath = gameLogPath;
            _messageStopWatch = new Stopwatch();
            _gameEventChannel = Channel.CreateUnbounded<(string, DateTimeOffset)>(new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = true,
                SingleReader = true,
                SingleWriter = true
            });

            if (_gameLogPath != null)
                Log.Debug("Logging game events to {gameLogPath}", _gameLogPath);

            _ = Task.Run(ParseAllMessagesAsync);
        }

        public void QueueMessage(string message)
        {
            _gameEventChannel.Writer.TryWrite((message, DateTimeOffset.UtcNow));
        }

        private async Task ParseAllMessagesAsync()
        {
            // Parse all messages written to the channel asynchronously
            await foreach (var (message, time) in _gameEventChannel.Reader.ReadAllAsync())
            {
                try
                {
                    await ParseMessageAsync(message, time);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to parse message {message}", message);
                }
            }
        }

        private async Task ParseMessageAsync(string message, DateTimeOffset time)
        {
            var parts = message.Split('\t');
            var eventType = parts[0];

            if (eventType == "response")
            {
                _gameServer.SetRconResponse(parts[1], parts[2]);
            }
            else if (_eventHandlers.TryGetValue(eventType, out var eventHandler))
            {
                _messageStopWatch.Restart();
                await eventHandler(parts, time);
                LogGameEvent(message, time);

                _messageStopWatch.Stop();
                var elapsedMs = _messageStopWatch.ElapsedMilliseconds;
                if (elapsedMs > 400)
                {
                    Log.Warning("Event {EventType} took {ElapsedMilliseconds} ms ({message})", eventType, _messageStopWatch.ElapsedMilliseconds, message);
                }
            }
            else if (eventType.Length > 0)
            {
                if (eventType != "Unknown object or method!" && !eventType.StartsWith("id") && !eventType.StartsWith("0x"))
                {
                    Log.Error($"Unknown server event: '{eventType}'");
                }
            }
        }

        private void LogGameEvent(string message, DateTimeOffset time)
        {
            if (_gameLogPath == null)
                return;

            var diff = (int)(time - _startTime).TotalMilliseconds;
            var line = $"{diff} {message}\n";
            File.AppendAllText(_gameLogPath, line);
        }

        private static Dictionary<string, Func<string[], DateTimeOffset, ValueTask>> GetEventsHandlers(IEventHandler eh)
        {
            return new Dictionary<string, Func<string[], DateTimeOffset, ValueTask>>
            {
                // Server events
                { "serverInfo", (p, t) => eh.OnServerInfoAsync(p[1], p[2], Int(p[3]), Int(p[4]), Int(p[5]), t) },

                // Game status events
                { "gameStatePreGame", (p, t) => eh.OnGameStatePreGameAsync(t) },
                { "gameStatePlaying", (p, t) => eh.OnGameStatePlayingAsync(p[1], p[2], p[3], Int(p[4]), t) },
                { "gameStateEndGame", (p, t) => eh.OnGameStateEndGameAsync(p[1], Int(p[2]), p[3], Int(p[4]), p[5], t) },
                { "gameStatePaused", (p, t) => eh.OnGameStatePausedAsync(t) },
                { "gameStateRestart", (p, t) => eh.OnGameStateRestartAsync(t) },
                { "gameStateNotConnected", (p, t) => eh.OnGameStateNotConnectedAsync(t) },

                // Game events
                { "controlPointCapture", (p, t) => eh.OnControlPointCaptureAsync(Int(p[1]), p[2], t) },
                { "controlPointNeutralised", (p, t) => eh.OnControlPointNeutralisedAsync(p[1], t) },

                // Timer events
                { "ticketStatus", (p, t) => eh.OnTicketStatusAsync(p[1], Int(p[2]), p[3], Int(p[4]), p[5], t) },
                { "playerPositionUpdate", (p, t) => eh.OnPlayerPositionUpdateAsync(Int(p[1]), Pos(p[2]), Rot(p[3]), Int(p[4]), t) },
                { "projectilePositionUpdate", (p, t) => eh.OnProjectilePositionUpdateAsync(Int(p[1]), p[2], Pos(p[3]), Rot(p[4]), t) },

                // Player events
                { "playerConnect", (p, t) => eh.OnPlayerConnectAsync(Int(p[1]), p[2], Int(p[3]), p[4], p[5], Int(p[6]), t) },
                { "playerSpawn", (p, t) => eh.OnPlayerSpawnAsync(Int(p[1]), Pos(p[2]), Rot(p[3]), t) },
                { "playerChangeTeam", (p, t) => eh.OnPlayerChangeTeamAsync(Int(p[1]), Int(p[2]), t) },
                { "playerScore", (p, t) => eh.OnPlayerScoreAsync(Int(p[1]), Int(p[2]), Int(p[3]), Int(p[4]), Int(p[5]), t) },
                { "playerRevived", (p, t) => eh.OnPlayerRevivedAsync(Int(p[1]), Int(p[2]), t) },
                { "playerKilledSelf", (p, t) => eh.OnPlayerKilledSelfAsync(Int(p[1]), Pos(p[2]), t) },
                { "playerTeamkilled", (p, t) => eh.OnPlayerTeamkilledAsync(Int(p[1]), Pos(p[2]), Int(p[3]), Pos(p[4]), t) },
                { "playerKilled", (p, t) => eh.OnPlayerKilledAsync(Int(p[1]), Pos(p[2]), Int(p[3]), Pos(p[4]), p[5], t) },
                { "playerDeath", (p, t) => eh.OnPlayerDeathAsync(Int(p[1]), Pos(p[2]), t) },
                { "playerDisconnect", (p, t) => eh.OnPlayerDisconnectAsync(Int(p[1]), t) },

                // Vehicle events
                { "enterVehicle", (p, t) => eh.OnEnterVehicleAsync(Int(p[1]), Int(p[2]), p[3], p[4], t) },
                { "exitVehicle", (p, t) => eh.OnExitVehicleAsync(Int(p[1]), Int(p[2]), p[3], p[4], t) },
                { "vehicleDestroyed", (p, t) => eh.OnVehicleDestroyedAsync(Int(p[1]), p[2], t) },

                // Chat events
                { "chatServer", (p, t) => eh.OnChatServerAsync(p[1], p[2], p[3], t) },
                { "chatPlayer", (p, t) => eh.OnChatPlayerAsync(p[1], p[2], Int(p[3]), p[4], t) }
            };
        }

        private static int Int(string arg) => int.Parse(arg);
        private static Position Pos(string arg) => Position.Parse(arg);
        private static Rotation Rot(string arg) => Rotation.Parse(arg);
    }
}
