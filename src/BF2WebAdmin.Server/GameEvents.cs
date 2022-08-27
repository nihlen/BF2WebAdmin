using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Shared;

namespace BF2WebAdmin.Server;

// Server events
public record ServerUpdateEvent(string Name, int GamePort, int QueryPort, int MaxPlayers, DateTimeOffset TimeStamp) : IEvent;
public record SocketStateChangedEvent(SocketState SocketState, DateTimeOffset TimeStamp) : IEvent;
public record GameStateChangedEvent(GameState GameState, DateTimeOffset TimeStamp) : IEvent;
public record MapsChangedEvent(IEnumerable<Map> Maps, DateTimeOffset TimeStamp) : IEvent;
public record MapChangedEvent(Map Map, IEnumerable<Team> Teams, DateTimeOffset TimeStamp) : IEvent;
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

// RabbitMQ events
public record GameStreamStartedEvent(Guid? MatchId, string StreamUrl, string BotName, DateTimeOffset TimeStamp) : IEvent;
public record GameStreamStoppedEvent(Guid? MatchId, DateTimeOffset TimeStamp) : IEvent;
