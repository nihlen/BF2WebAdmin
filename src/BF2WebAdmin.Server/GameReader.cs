using System.Diagnostics;
using System.Threading.Channels;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using Serilog;

namespace BF2WebAdmin.Server
{
    public class GameReader : IGameReader
    {
        //private static ILogger Logger { get; } = ApplicationLogging.CreateLogger<GameReader>();

        private readonly IGameServer _gameServer;
        private readonly string _gameLogPath;
        private readonly DateTime _startTime;
        private readonly Dictionary<string, Func<string[], Task>> _eventHandlers;
        private readonly Stopwatch _messageStopWatch;
        private readonly Channel<string> _gameEventChannel;

        public GameReader(IGameServer gameServer, string gameLogPath = null)
        {
            var eventHandler = new EventHandler(gameServer);
            _eventHandlers = GetEventsHandlers(eventHandler);
            _startTime = DateTime.UtcNow;
            _gameServer = gameServer;
            _gameLogPath = gameLogPath;
            _messageStopWatch = new Stopwatch();
            _gameEventChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
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
            _gameEventChannel.Writer.TryWrite(message);
        }

        private async Task ParseAllMessagesAsync()
        {
            // Parse all messages written to the channel asynchronously
            await foreach (var message in _gameEventChannel.Reader.ReadAllAsync())
            {
                try
                {
                    await ParseMessageAsync(message);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to parse message {message}", message);
                }
            }
        }

        private async Task ParseMessageAsync(string message)
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
                await eventHandler(parts);
                LogGameEvent(message);

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

        private void LogGameEvent(string message)
        {
            if (_gameLogPath == null)
                return;

            var diff = (int)(DateTime.UtcNow - _startTime).TotalMilliseconds;
            var line = $"{diff} {message}\n";
            File.AppendAllText(_gameLogPath, line);
        }

        private static Dictionary<string, Func<string[], Task>> GetEventsHandlers(IEventHandler eh)
        {
            return new Dictionary<string, Func<string[], Task>>
            {
                // Server events
                { "serverInfo", p => eh.OnServerInfoAsync(p[1], p[2], Int(p[3]), Int(p[4]), Int(p[5])) },

                // Game status events
                { "gameStatePreGame", p => eh.OnGameStatePreGameAsync() },
                { "gameStatePlaying", p => eh.OnGameStatePlayingAsync(p[1], p[2], p[3], Int(p[4])) },
                { "gameStateEndGame", p => eh.OnGameStateEndGameAsync(p[1], Int(p[2]), p[3], Int(p[4]), p[5]) },
                { "gameStatePaused", p => eh.OnGameStatePausedAsync() },
                { "gameStateRestart", p => eh.OnGameStateRestartAsync() },
                { "gameStateNotConnected", p => eh.OnGameStateNotConnectedAsync() },

                // Game events
                { "controlPointCapture", p => eh.OnControlPointCaptureAsync(Int(p[1]), p[2]) },
                { "controlPointNeutralised", p => eh.OnControlPointNeutralisedAsync(p[1]) },

                // Timer events
                { "ticketStatus", p => eh.OnTicketStatusAsync(p[1], Int(p[2]), p[3], Int(p[4]), p[5]) },
                { "playerPositionUpdate", p => eh.OnPlayerPositionUpdateAsync(Int(p[1]), Pos(p[2]), Rot(p[3]), Int(p[4])) },
                { "projectilePositionUpdate", p => eh.OnProjectilePositionUpdateAsync(Int(p[1]), p[2], Pos(p[3]), Rot(p[4])) },

                // Player events
                { "playerConnect", p => eh.OnPlayerConnectAsync(Int(p[1]), p[2], Int(p[3]), p[4], p[5], Int(p[6])) },
                { "playerSpawn", p => eh.OnPlayerSpawnAsync(Int(p[1]), Pos(p[2]), Rot(p[3])) },
                { "playerChangeTeam", p => eh.OnPlayerChangeTeamAsync(Int(p[1]), Int(p[2])) },
                { "playerScore", p => eh.OnPlayerScoreAsync(Int(p[1]), Int(p[2]), Int(p[3]), Int(p[4]), Int(p[5])) },
                { "playerRevived", p => eh.OnPlayerRevivedAsync(Int(p[1]), Int(p[2])) },
                { "playerKilledSelf", p => eh.OnPlayerKilledSelfAsync(Int(p[1]), Pos(p[2])) },
                { "playerTeamkilled", p => eh.OnPlayerTeamkilledAsync(Int(p[1]), Pos(p[2]), Int(p[3]), Pos(p[4])) },
                { "playerKilled", p => eh.OnPlayerKilledAsync(Int(p[1]), Pos(p[2]), Int(p[3]), Pos(p[4]), p[5]) },
                { "playerDeath", p => eh.OnPlayerDeathAsync(Int(p[1]), Pos(p[2])) },
                { "playerDisconnect", p => eh.OnPlayerDisconnectAsync(Int(p[1])) },

                // Vehicle events
                { "enterVehicle", p => eh.OnEnterVehicleAsync(Int(p[1]), Int(p[2]), p[3], p[4]) },
                { "exitVehicle", p => eh.OnExitVehicleAsync(Int(p[1]), Int(p[2]), p[3], p[4]) },
                { "vehicleDestroyed", p => eh.OnVehicleDestroyedAsync(Int(p[1]), p[2]) },

                // Chat events
                { "chatServer", p => eh.OnChatServerAsync(p[1], p[2], p[3]) },
                { "chatPlayer", p => eh.OnChatPlayerAsync(p[1], p[2], Int(p[3]), p[4]) }
            };
        }

        private static int Int(string arg) => int.Parse(arg);
        private static Position Pos(string arg) => Position.Parse(arg);
        private static Rotation Rot(string arg) => Rotation.Parse(arg);
    }
}
