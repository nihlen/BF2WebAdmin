using BF2WebAdmin.Common.Entities.Game;

namespace BF2WebAdmin.Server.Abstractions
{
    public interface IEventHandler
    {
        ValueTask OnChatPlayerAsync(string channel, string flags, int index, string text);
        ValueTask OnChatServerAsync(string channel, string flags, string text);
        ValueTask OnControlPointCaptureAsync(int teamId, string cpName);
        ValueTask OnControlPointNeutralisedAsync(string cpName);
        ValueTask OnEnterVehicleAsync(int index, int rootVehicleId, string rootVehicleName, string vehicleName);
        ValueTask OnExitVehicleAsync(int index, int rootVehicleId, string rootVehicleName, string vehicleName);
        ValueTask OnGameStateEndGameAsync(string team1Name, int team1Tickets, string team2Name, int team2Tickets, string mapName);
        ValueTask OnGameStateNotConnectedAsync();
        ValueTask OnGameStatePausedAsync();
        ValueTask OnGameStatePlayingAsync(string team1Name, string team2Name, string mapName, int maxPlayers);
        ValueTask OnGameStatePreGameAsync();
        ValueTask OnGameStateRestartAsync();
        ValueTask OnPlayerChangeTeamAsync(int index, int teamId);
        ValueTask OnPlayerConnectAsync(int index, string name, int pid, string ipAddress, string hash, int teamId);
        ValueTask OnPlayerDeathAsync(int index, Position pos);
        ValueTask OnPlayerDisconnectAsync(int index);
        ValueTask OnPlayerKilledAsync(int attackerIndex, Position attackerPos, int victimIndex, Position victimPos, string weapon);
        ValueTask OnPlayerKilledSelfAsync(int index, Position pos);
        ValueTask OnPlayerPositionUpdateAsync(int playerIndex, Position position, Rotation rotation, int ping);
        ValueTask OnPlayerRevivedAsync(int medicIndex, int reviveeIndex);
        ValueTask OnPlayerScoreAsync(int index, int score, int teamScore, int kills, int deaths);
        ValueTask OnPlayerSpawnAsync(int index, Position pos, Rotation rot);
        ValueTask OnPlayerTeamkilledAsync(int attackerIndex, Position attackerPos, int victimIndex, Position victimPos);
        ValueTask OnProjectilePositionUpdateAsync(int id, string templateName, Position position, Rotation rotation);
        ValueTask OnServerInfoAsync(string serverName, string maps, int gamePort, int queryPort, int maxPlayers);
        ValueTask OnTicketStatusAsync(string team1Name, int team1Tickets, string team2Name, int team2Tickets, string mapName);
        ValueTask OnVehicleDestroyedAsync(int vehicleId, string vehicleName);
    }
}
