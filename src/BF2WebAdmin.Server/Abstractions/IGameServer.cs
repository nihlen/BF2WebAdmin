using System.Net;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Shared;
using BF2WebAdmin.Shared.Communication.DTOs;

namespace BF2WebAdmin.Server.Abstractions;

public interface IGameServer
{
    DateTime StartTime { get; }

    IModManager ModManager { get; }
    IGameWriter GameWriter { get; }
    IGameReader GameReader { get; set; }

    ServerInfo ServerInfo { get; }

    string Id { get; }
    string Name { get; }
    IPAddress IpAddress { get; }
    IPAddress ConnectedIpAddress { get; }
    int GamePort { get; }
    int QueryPort { get; }
    int RconPort { get; }
    int MaxPlayers { get; }
    GameState State { get; }
    SocketState SocketState { get; }

    Map? Map { get; }
    IEnumerable<Map> Maps { get; }
    IEnumerable<Team> Teams { get; }
    IEnumerable<Player> Players { get; }
    IEnumerable<Vehicle> Vehicles { get; }
    IEnumerable<Projectile> Projectiles { get; }
    IEnumerable<(MessageDto Message, DateTimeOffset Time)> Messages { get; }
    IEnumerable<(string Message, DateTimeOffset Time)> Events { get; }
    ValueTask CreateModManagerAsync(bool forceReinitialize = false);
    ValueTask UpdateServerInfoAsync(string name, int gamePort, int queryPort, int maxPlayers, DateTimeOffset time);
    ValueTask UpdateGameStateAsync(GameState state, DateTimeOffset time);
    ValueTask UpdateSocketStateAsync(SocketState state, DateTimeOffset time);
    ValueTask SetReconnectedAsync(IGameWriter gameWriter, DateTimeOffset time);
    ValueTask UpdateMapsAsync(IList<Map> maps, DateTimeOffset time);
    ValueTask UpdateMapAsync(string mapName, string team1Name, string team2Name, DateTimeOffset time);
    ValueTask AddPlayerAsync(Player player, DateTimeOffset time);
    ValueTask UpdatePlayerAsync(Player player, Position position, Rotation rotation, int ping, DateTimeOffset time);
    ValueTask UpdatePlayerVehicleAsync(Player player, Vehicle vehicle, string subVehicleTemplate, DateTimeOffset time);
    ValueTask UpdatePlayerTeamAsync(Player player, int teamId, DateTimeOffset time);
    ValueTask UpdatePlayerScoreAsync(Player player, int teamScore, int kills, int deaths, int totalScore, DateTimeOffset time);
    ValueTask UpdateProjectileAsync(Projectile projectile, Position position, Rotation rotation, DateTimeOffset time);
    ValueTask RemovePlayerAsync(Player player, DateTimeOffset time);
    ValueTask SetPlayerSpawnAsync(Player player, Position position, Rotation rotation, DateTimeOffset time);
    ValueTask SetPlayerKillAsync(Player attacker, Position attackerPosition, Player victim, Position victimPosition, string weapon, DateTimeOffset time);
    ValueTask SetPlayerDeathAsync(Player player, Position position, DateTimeOffset time);
    ValueTask AddMessageAsync(Message message, DateTimeOffset time);
    void SetRconResponse(string responseCode, string value);
    Vehicle GetVehicle(Player player, int rootVehicleId, string rootVehicleName, string vehicleName);
    Player GetPlayer(int index);
    Player? GetPlayerByName(string namePart);
    Player? GetPlayerByHash(string playerHash);
    Projectile GetProjectile(int id, string templateName, Position position);
    Task<bool> FixTeamMismatchAsync(IEnumerable<Player> players);
}