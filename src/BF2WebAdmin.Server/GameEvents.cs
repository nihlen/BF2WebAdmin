using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;

namespace BF2WebAdmin.Server;

// Server events
public record ServerUpdateEvent(string Name, int GamePort, int QueryPort, int MaxPlayers, DateTimeOffset TimeStamp) : IEvent;
public record SocketStateChangedEvent(SocketState SocketState, DateTimeOffset TimeStamp) : IEvent;
public record GameStateChangedEvent(GameState GameState, DateTimeOffset TimeStamp) : IEvent;
public record MapsChangedEvent(IEnumerable<Map> Maps, DateTimeOffset TimeStamp) : IEvent;
public record MapChangedEvent(Map Map, DateTimeOffset TimeStamp) : IEvent;
public record PlayerJoinEvent(Player Player, DateTimeOffset TimeStamp) : IEvent;
public record PlayerPositionEvent(Player Player, Position Position, Rotation Rotation, int Ping, DateTimeOffset TimeStamp) : IEvent;
public record PlayerSpawnEvent(Player Player, Position Position, Rotation Rotation, DateTimeOffset TimeStamp) : IEvent;
public record PlayerVehicleEvent(Player Player, Vehicle Vehicle, DateTimeOffset TimeStamp) : IEvent;
public record PlayerTeamEvent(Player Player, Team Team, DateTimeOffset TimeStamp) : IEvent;
public record PlayerScoreEvent(Player Player, int TeamScore, int Kills, int Deaths, int TotalScore, DateTimeOffset TimeStamp) : IEvent;
public record PlayerLeftEvent(Player Player, DateTimeOffset TimeStamp) : IEvent;
public record PlayerKillEvent(Player Attacker, Position AttackerPosition, Player Victim, Position VictimPosition, string Weapon, DateTimeOffset TimeStamp) : IEvent;
public record PlayerDeathEvent(Player Player, Position Position, bool IsSuicide, DateTimeOffset TimeStamp) : IEvent;
public record ProjectilePositionEvent(Projectile Projectile, Position Position, Rotation Rotation, DateTimeOffset TimeStamp) : IEvent;
public record ChatMessageEvent(Message Message, DateTimeOffset TimeStamp) : IEvent;

// Heli 2v2 events
public record MatchStartEvent(Modules.BF2.Match Match, DateTimeOffset TimeStamp) : IEvent;
public record MatchEndEvent(Modules.BF2.Match Match, DateTimeOffset TimeStamp) : IEvent;
public record RoundStartEvent(Modules.BF2.Round Round, DateTimeOffset TimeStamp) : IEvent;
public record RoundEndEvent(Modules.BF2.Round Round, DateTimeOffset TimeStamp) : IEvent;


/*

             _gameServer.ServerUpdate += (name, gamePort, queryPort, maxPlayers) =>
            _gameServer.PlayerPosition += (player, position, rotation, ping) =>
            _gameServer.ChatMessage += message =>
            _gameServer.PlayerJoin += player =>
            _gameServer.PlayerLeft += player =>
            _gameServer.PlayerKill += (attacker, attackerPosition, victim, victimPosition, weapon) =>
            _gameServer.PlayerDeath += (player, position, isSuicide) =>
            _gameServer.PlayerSpawn += (player, position, rotation) =>
            _gameServer.PlayerTeam += (player, team) =>
            _gameServer.PlayerScore += (player, teamScore, kills, deaths, totalScore) =>
            _gameServer.PlayerVehicle += (player, vehicle) =>
            _gameServer.GameStateChanged += state =>
            _gameServer.ProjectilePosition += (projectile, position, rotation) =>
            _gameServer.MapChanged += map =>


 */

