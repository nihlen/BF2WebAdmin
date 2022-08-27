using BF2WebAdmin.Common.Entities.Game;

namespace BF2WebAdmin.Server.Abstractions;

public interface IEventHandler
{
    ValueTask OnChatPlayerAsync(string channel, string flags, int index, string text, DateTimeOffset time);
    ValueTask OnChatServerAsync(string channel, string flags, string text, DateTimeOffset time);
    ValueTask OnControlPointCaptureAsync(int teamId, string cpName, DateTimeOffset time);
    ValueTask OnControlPointNeutralisedAsync(string cpName, DateTimeOffset time);
    ValueTask OnEnterVehicleAsync(int index, int rootVehicleId, string rootVehicleName, string vehicleName, DateTimeOffset time);
    ValueTask OnExitVehicleAsync(int index, int rootVehicleId, string rootVehicleName, string vehicleName, DateTimeOffset time);
    ValueTask OnGameStateEndGameAsync(string team1Name, int team1Tickets, string team2Name, int team2Tickets, string mapName, DateTimeOffset time);
    ValueTask OnGameStateNotConnectedAsync(DateTimeOffset time);
    ValueTask OnGameStatePausedAsync(DateTimeOffset time);
    ValueTask OnGameStatePlayingAsync(string team1Name, string team2Name, string mapName, int maxPlayers, DateTimeOffset time);
    ValueTask OnGameStatePreGameAsync(DateTimeOffset time);
    ValueTask OnGameStateRestartAsync(DateTimeOffset time);
    ValueTask OnPlayerChangeTeamAsync(int index, int teamId, DateTimeOffset time);
    ValueTask OnPlayerConnectAsync(int index, string name, int pid, string ipAddress, string hash, int teamId, DateTimeOffset time);
    ValueTask OnPlayerDeathAsync(int index, Position pos, DateTimeOffset time);
    ValueTask OnPlayerDisconnectAsync(int index, DateTimeOffset time);
    ValueTask OnPlayerKilledAsync(int attackerIndex, Position attackerPos, int victimIndex, Position victimPos, string weapon, DateTimeOffset time);
    ValueTask OnPlayerKilledSelfAsync(int index, Position pos, DateTimeOffset time);
    ValueTask OnPlayerPositionUpdateAsync(int playerIndex, Position position, Rotation rotation, int ping, DateTimeOffset time);
    ValueTask OnPlayerRevivedAsync(int medicIndex, int reviveeIndex, DateTimeOffset time);
    ValueTask OnPlayerScoreAsync(int index, int score, int teamScore, int kills, int deaths, DateTimeOffset time);
    ValueTask OnPlayerSpawnAsync(int index, Position pos, Rotation rot, DateTimeOffset time);
    ValueTask OnPlayerTeamkilledAsync(int attackerIndex, Position attackerPos, int victimIndex, Position victimPos, DateTimeOffset time);
    ValueTask OnProjectilePositionUpdateAsync(int id, string templateName, Position position, Rotation rotation, DateTimeOffset time);
    ValueTask OnServerInfoAsync(string serverName, string maps, int gamePort, int queryPort, int maxPlayers, DateTimeOffset time);
    ValueTask OnTicketStatusAsync(string team1Name, int team1Tickets, string team2Name, int team2Tickets, string mapName, DateTimeOffset time);
    ValueTask OnVehicleDestroyedAsync(int vehicleId, string vehicleName, DateTimeOffset time);
}