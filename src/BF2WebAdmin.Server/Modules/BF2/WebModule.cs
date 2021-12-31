using BF2WebAdmin.Common.Abstractions;
using BF2WebAdmin.Common.Communication;
using BF2WebAdmin.Common.Communication.DTOs;
using BF2WebAdmin.Common.Communication.Messages;
using BF2WebAdmin.Server.Abstractions;
using Serilog;

namespace BF2WebAdmin.Server.Modules.BF2
{
    public class WebModule : IModule
    {
        //private static ILogger Logger { get; } = ApplicationLogging.CreateLogger<WebModule>();

        private readonly IGameServer _gameServer;
        private readonly IMessageQueue<MessageEvent, MessageAction> _messageQueue;
        private const string MessageQueueAddress = ">tcp://localhost:6006";

        private void SendGameEvent(IMessagePayload payload)
        {
            _messageQueue.Send(MessageEvent.Create(_gameServer.Id, payload));
        }

        public WebModule(IGameServer server)
        {
            _gameServer = server;
            _messageQueue = new MessageQueue<MessageEvent, MessageAction>(MessageQueueAddress);
            _messageQueue.Receive += (sender, args) => HandleAction(args.Message);

            _gameServer.ServerUpdate += (name, gamePort, queryPort, maxPlayers) =>
            {
                SendGameEvent(new ServerUpdateEvent
                {
                    Id = _gameServer.Id,
                    Name = name,
                    IpAddress = _gameServer.IpAddress.ToString(),
                    GamePort = gamePort,
                    QueryPort = queryPort,
                    Map = _gameServer.Map?.Name,
                    Players = _gameServer.Players.Count(),
                    MaxPlayers = maxPlayers
                });

                return Task.CompletedTask;
            };

            _gameServer.PlayerPosition += (player, position, rotation, ping) =>
            {
                SendGameEvent(new PlayerPositionEvent
                {
                    PlayerId = player.Index,
                    Position = new Vector3(position),
                    Rotation = new Vector3(rotation),
                    Ping = ping
                });

                return Task.CompletedTask;
            };

            _gameServer.ChatMessage += message =>
            {
                SendGameEvent(new ChatEvent
                {
                    Message = new MessageDto(message)
                });

                return Task.CompletedTask;
            };

            _gameServer.PlayerJoin += player =>
            {
                using (Profiler.Start("DEBUG PlayerJoin SendGameEvent"))
                {
                    SendGameEvent(new PlayerJoinEvent
                    {
                        Player = new PlayerDto(player)
                    });
                }

                return Task.CompletedTask;
            };

            _gameServer.PlayerLeft += player =>
            {
                SendGameEvent(new PlayerLeftEvent
                {
                    PlayerId = player.Index
                });

                return Task.CompletedTask;
            };

            _gameServer.PlayerKill += (attacker, attackerPosition, victim, victimPosition, weapon) =>
            {
                SendGameEvent(new PlayerKillEvent
                {
                    AttackerId = attacker.Index,
                    AttackerPosition = new Vector3(attackerPosition),
                    VictimId = victim.Index,
                    VictimPosition = new Vector3(victimPosition),
                    Weapon = weapon
                });

                return Task.CompletedTask;
            };

            _gameServer.PlayerDeath += (player, position, isSuicide) =>
            {
                SendGameEvent(new PlayerDeathEvent
                {
                    PlayerId = player.Index,
                    Position = new Vector3(position),
                    //IsSuicide = isSuicide // TODO: add property
                });

                return Task.CompletedTask;
            };

            _gameServer.PlayerSpawn += (player, position, rotation) =>
            {
                SendGameEvent(new PlayerSpawnEvent
                {
                    PlayerId = player.Index,
                    Position = new Vector3(position),
                    Rotation = new Vector3(rotation)
                });

                return Task.CompletedTask;
            };

            _gameServer.PlayerTeam += (player, team) =>
            {
                SendGameEvent(new PlayerTeamEvent
                {
                    PlayerId = player.Index,
                    TeamId = team.Id
                });

                return Task.CompletedTask;
            };

            _gameServer.PlayerScore += (player, teamScore, kills, deaths, totalScore) =>
            {
                SendGameEvent(new PlayerScoreEvent
                {
                    PlayerId = player.Index,
                    TeamScore = teamScore,
                    Kills = kills,
                    Deaths = deaths,
                    TotalScore = totalScore
                });

                return Task.CompletedTask;
            };

            _gameServer.PlayerVehicle += (player, vehicle) =>
            {
                SendGameEvent(new PlayerVehicleEvent
                {
                    PlayerId = player.Index,
                    Vehicle = new VehicleDto(vehicle)
                });

                return Task.CompletedTask;
            };

            _gameServer.GameStateChanged += state =>
            {
                SendGameEvent(new GameStateEvent
                {
                    State = state.ToString()
                });

                return Task.CompletedTask;
            };

            _gameServer.ProjectilePosition += (projectile, position, rotation) =>
            {
                SendGameEvent(new ProjectilePositionEvent
                {
                    ProjectileId = projectile.Id,
                    Template = projectile.Template,
                    Position = new Vector3(position),
                    Rotation = new Vector3(rotation)
                });

                return Task.CompletedTask;
            };

            _gameServer.MapChanged += map =>
            {
                SendGameEvent(new MapChangeEvent
                {
                    Map = map?.Name ?? "?",
                    Size = map?.Size ?? 0
                });

                return Task.CompletedTask;
            };
        }

        private void HandleAction(MessageAction message)
        {
            if (message.Type == nameof(UserConnectAction))
            {
                // Send server info to new users
                Log.Debug("User connected: {UserId}", (message.Payload as UserConnectAction)?.Id);
                SendGameEvent(
                    new ServerUpdateEvent
                    {
                        Id = _gameServer.Id,
                        Name = _gameServer.Name,
                        IpAddress = _gameServer.IpAddress.ToString(),
                        GamePort = _gameServer.GamePort,
                        QueryPort = _gameServer.QueryPort,
                        Map = _gameServer.Map?.Name,
                        Players = _gameServer.Players.Count(),
                        MaxPlayers = _gameServer.MaxPlayers,
                    }
                );

                // Send player info
                foreach (var player in _gameServer.Players)
                {
                    SendGameEvent(new PlayerJoinEvent
                    {
                        Player = new PlayerDto(player)
                    });

                    // Send vehicle info
                    if (player.Vehicle != null)
                    {
                        SendGameEvent(new PlayerVehicleEvent
                        {
                            PlayerId = player.Index,
                            Vehicle = new VehicleDto(player.Vehicle)
                        });
                    }
                }
            }
            else if (message.Type == nameof(UserDisconnectAction))
            {
                Log.Debug("User disconnected: {UserId}", (message.Payload as UserDisconnectAction)?.Id);
            }
        }
    }
}
