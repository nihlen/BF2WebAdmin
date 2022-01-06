using BF2WebAdmin.Common.Abstractions;
using BF2WebAdmin.Common.Communication;
using BF2WebAdmin.Common.Communication.DTOs;
using BF2WebAdmin.Common.Communication.Messages;
using BF2WebAdmin.Server.Abstractions;
using Serilog;

namespace BF2WebAdmin.Server.Modules.BF2
{
    public class WebModule : IModule,
        IHandleEventAsync<BF2WebAdmin.Server.ServerUpdateEvent>,
        IHandleEventAsync<BF2WebAdmin.Server.PlayerPositionEvent>,
        IHandleEventAsync<BF2WebAdmin.Server.ChatMessageEvent>,
        IHandleEventAsync<BF2WebAdmin.Server.PlayerJoinEvent>,
        IHandleEventAsync<BF2WebAdmin.Server.PlayerLeftEvent>,
        IHandleEventAsync<BF2WebAdmin.Server.PlayerKillEvent>,
        IHandleEventAsync<BF2WebAdmin.Server.PlayerDeathEvent>,
        IHandleEventAsync<BF2WebAdmin.Server.PlayerSpawnEvent>,
        IHandleEventAsync<BF2WebAdmin.Server.PlayerTeamEvent>,
        IHandleEventAsync<BF2WebAdmin.Server.PlayerScoreEvent>,
        IHandleEventAsync<BF2WebAdmin.Server.PlayerVehicleEvent>,
        IHandleEventAsync<BF2WebAdmin.Server.GameStateChangedEvent>,
        IHandleEventAsync<BF2WebAdmin.Server.ProjectilePositionEvent>,
        IHandleEventAsync<BF2WebAdmin.Server.MapChangedEvent>
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

            //_gameServer.ServerUpdate += (name, gamePort, queryPort, maxPlayers) =>
            //{
            //    SendGameEvent(new Common.Communication.Messages.ServerUpdateEvent
            //    {
            //        Id = _gameServer.Id,
            //        Name = name,
            //        IpAddress = _gameServer.IpAddress.ToString(),
            //        GamePort = gamePort,
            //        QueryPort = queryPort,
            //        Map = _gameServer.Map?.Name,
            //        Players = _gameServer.Players.Count(),
            //        MaxPlayers = maxPlayers
            //    });

            //    return Task.CompletedTask;
            //};

            //_gameServer.PlayerPosition += (player, position, rotation, ping) =>
            //{
            //    SendGameEvent(new Common.Communication.Messages.PlayerPositionEvent
            //    {
            //        PlayerId = player.Index,
            //        Position = new Vector3(position),
            //        Rotation = new Vector3(rotation),
            //        Ping = ping
            //    });

            //    return Task.CompletedTask;
            //};

            //_gameServer.ChatMessage += message =>
            //{
            //    SendGameEvent(new Common.Communication.Messages.ChatEvent
            //    {
            //        Message = new MessageDto(message)
            //    });

            //    return Task.CompletedTask;
            //};

            //_gameServer.PlayerJoin += player =>
            //{
            //    using (Profiler.Start("DEBUG PlayerJoin SendGameEvent"))
            //    {
            //        SendGameEvent(new Common.Communication.Messages.PlayerJoinEvent
            //        {
            //            Player = new PlayerDto(player)
            //        });
            //    }

            //    return Task.CompletedTask;
            //};

            //_gameServer.PlayerLeft += player =>
            //{
            //    SendGameEvent(new Common.Communication.Messages.PlayerLeftEvent
            //    {
            //        PlayerId = player.Index
            //    });

            //    return Task.CompletedTask;
            //};

            //_gameServer.PlayerKill += (attacker, attackerPosition, victim, victimPosition, weapon) =>
            //{
            //    SendGameEvent(new Common.Communication.Messages.PlayerKillEvent
            //    {
            //        AttackerId = attacker.Index,
            //        AttackerPosition = new Vector3(attackerPosition),
            //        VictimId = victim.Index,
            //        VictimPosition = new Vector3(victimPosition),
            //        Weapon = weapon
            //    });

            //    return Task.CompletedTask;
            //};

            //_gameServer.PlayerDeath += (player, position, isSuicide) =>
            //{
            //    SendGameEvent(new Common.Communication.Messages.PlayerDeathEvent
            //    {
            //        PlayerId = player.Index,
            //        Position = new Vector3(position),
            //        //IsSuicide = isSuicide // TODO: add property
            //    });

            //    return Task.CompletedTask;
            //};

            //_gameServer.PlayerSpawn += (player, position, rotation) =>
            //{
            //    SendGameEvent(new Common.Communication.Messages.PlayerSpawnEvent
            //    {
            //        PlayerId = player.Index,
            //        Position = new Vector3(position),
            //        Rotation = new Vector3(rotation)
            //    });

            //    return Task.CompletedTask;
            //};

            //_gameServer.PlayerTeam += (player, team) =>
            //{
            //    SendGameEvent(new Common.Communication.Messages.PlayerTeamEvent
            //    {
            //        PlayerId = player.Index,
            //        TeamId = team.Id
            //    });

            //    return Task.CompletedTask;
            //};

            //_gameServer.PlayerScore += (player, teamScore, kills, deaths, totalScore) =>
            //{
            //    SendGameEvent(new Common.Communication.Messages.PlayerScoreEvent
            //    {
            //        PlayerId = player.Index,
            //        TeamScore = teamScore,
            //        Kills = kills,
            //        Deaths = deaths,
            //        TotalScore = totalScore
            //    });

            //    return Task.CompletedTask;
            //};

            //_gameServer.PlayerVehicle += (player, vehicle) =>
            //{
            //    SendGameEvent(new Common.Communication.Messages.PlayerVehicleEvent
            //    {
            //        PlayerId = player.Index,
            //        Vehicle = new VehicleDto(vehicle)
            //    });

            //    return Task.CompletedTask;
            //};

            //_gameServer.GameStateChanged += state =>
            //{
            //    SendGameEvent(new GameStateEvent
            //    {
            //        State = state.ToString()
            //    });

            //    return Task.CompletedTask;
            //};

            //_gameServer.ProjectilePosition += (projectile, position, rotation) =>
            //{
            //    SendGameEvent(new Common.Communication.Messages.ProjectilePositionEvent
            //    {
            //        ProjectileId = projectile.Id,
            //        Template = projectile.Template,
            //        Position = new Vector3(position),
            //        Rotation = new Vector3(rotation)
            //    });

            //    return Task.CompletedTask;
            //};

            //_gameServer.MapChanged += map =>
            //{
            //    SendGameEvent(new MapChangeEvent
            //    {
            //        Map = map?.Name ?? "?",
            //        Size = map?.Size ?? 0
            //    });

            //    return Task.CompletedTask;
            //};
        }

        private void HandleAction(MessageAction message)
        {
            if (message.Type == nameof(UserConnectAction))
            {
                // Send server info to new users
                Log.Debug("User connected: {UserId}", (message.Payload as UserConnectAction)?.Id);
                SendGameEvent(
                    new Common.Communication.Messages.ServerUpdateEvent
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
                    SendGameEvent(new Common.Communication.Messages.PlayerJoinEvent
                    {
                        Player = new PlayerDto(player)
                    });

                    // Send vehicle info
                    if (player.Vehicle != null)
                    {
                        SendGameEvent(new Common.Communication.Messages.PlayerVehicleEvent
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

        public ValueTask HandleAsync(ServerUpdateEvent e)
        {
            SendGameEvent(new Common.Communication.Messages.ServerUpdateEvent
            {
                Id = _gameServer.Id,
                Name = e.Name,
                IpAddress = _gameServer.IpAddress.ToString(),
                GamePort = e.GamePort,
                QueryPort = e.QueryPort,
                Map = _gameServer.Map?.Name,
                Players = _gameServer.Players.Count(),
                MaxPlayers = e.MaxPlayers
            });

            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(PlayerPositionEvent e)
        {
            SendGameEvent(new Common.Communication.Messages.PlayerPositionEvent
            {
                PlayerId = e.Player.Index,
                Position = new Vector3(e.Position),
                Rotation = new Vector3(e.Rotation),
                Ping = e.Ping
            });

            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(ChatMessageEvent e)
        {
            SendGameEvent(new Common.Communication.Messages.ChatEvent
            {
                Message = new MessageDto(e.Message)
            });

            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(PlayerJoinEvent e)
        {
            using (Profiler.Start("DEBUG PlayerJoin SendGameEvent"))
            {
                SendGameEvent(new Common.Communication.Messages.PlayerJoinEvent
                {
                    Player = new PlayerDto(e.Player)
                });
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(PlayerLeftEvent e)
        {
            SendGameEvent(new Common.Communication.Messages.PlayerLeftEvent
            {
                PlayerId = e.Player.Index
            });

            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(PlayerKillEvent e)
        {
            SendGameEvent(new Common.Communication.Messages.PlayerKillEvent
            {
                AttackerId = e.Attacker.Index,
                AttackerPosition = new Vector3(e.AttackerPosition),
                VictimId = e.Victim.Index,
                VictimPosition = new Vector3(e.VictimPosition),
                Weapon = e.Weapon
            });

            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(PlayerDeathEvent e)
        {
            SendGameEvent(new Common.Communication.Messages.PlayerDeathEvent
            {
                PlayerId = e.Player.Index,
                Position = new Vector3(e.Position),
                //IsSuicide = isSuicide // TODO: add property
            });

            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(PlayerSpawnEvent e)
        {
            SendGameEvent(new Common.Communication.Messages.PlayerSpawnEvent
            {
                PlayerId = e.Player.Index,
                Position = new Vector3(e.Position),
                Rotation = new Vector3(e.Rotation)
            });

            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(PlayerTeamEvent e)
        {
            SendGameEvent(new Common.Communication.Messages.PlayerTeamEvent
            {
                PlayerId = e.Player.Index,
                TeamId = e.Team.Id
            });

            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(PlayerScoreEvent e)
        {
            SendGameEvent(new Common.Communication.Messages.PlayerScoreEvent
            {
                PlayerId = e.Player.Index,
                TeamScore = e.TeamScore,
                Kills = e.Kills,
                Deaths = e.Deaths,
                TotalScore = e.TotalScore
            });

            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(PlayerVehicleEvent e)
        {
            SendGameEvent(new Common.Communication.Messages.PlayerVehicleEvent
            {
                PlayerId = e.Player.Index,
                Vehicle = new VehicleDto(e.Vehicle)
            });

            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(GameStateChangedEvent e)
        {
            SendGameEvent(new GameStateEvent
            {
                State = e.GameState.ToString()
            });

            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(ProjectilePositionEvent e)
        {
            SendGameEvent(new Common.Communication.Messages.ProjectilePositionEvent
            {
                ProjectileId = e.Projectile.Id,
                Template = e.Projectile.Template,
                Position = new Vector3(e.Position),
                Rotation = new Vector3(e.Rotation)
            });

            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(MapChangedEvent e)
        {
            SendGameEvent(new MapChangeEvent
            {
                Map = e.Map?.Name ?? "?",
                Size = e.Map?.Size ?? 0
            });

            return ValueTask.CompletedTask;
        }
    }
}
