using BF2WebAdmin.Common.Entities.Game;

namespace BF2WebAdmin.Server.Abstractions
{
    public interface IEventHandler
    {
        Task OnChatPlayerAsync(string channel, string flags, int index, string text);
        Task OnChatServerAsync(string channel, string flags, string text);
        Task OnControlPointCaptureAsync(int teamId, string cpName);
        Task OnControlPointNeutralisedAsync(string cpName);
        Task OnEnterVehicleAsync(int index, int rootVehicleId, string rootVehicleName, string vehicleName);
        Task OnExitVehicleAsync(int index, int rootVehicleId, string rootVehicleName, string vehicleName);
        Task OnGameStateEndGameAsync(string team1Name, int team1Tickets, string team2Name, int team2Tickets, string mapName);
        Task OnGameStateNotConnectedAsync();
        Task OnGameStatePausedAsync();
        Task OnGameStatePlayingAsync(string team1Name, string team2Name, string mapName, int maxPlayers);
        Task OnGameStatePreGameAsync();
        Task OnGameStateRestartAsync();
        Task OnPlayerChangeTeamAsync(int index, int teamId);
        Task OnPlayerConnectAsync(int index, string name, int pid, string ipAddress, string hash, int teamId);
        Task OnPlayerDeathAsync(int index, Position pos);
        Task OnPlayerDisconnectAsync(int index);
        Task OnPlayerKilledAsync(int attackerIndex, Position attackerPos, int victimIndex, Position victimPos, string weapon);
        Task OnPlayerKilledSelfAsync(int index, Position pos);
        Task OnPlayerPositionUpdateAsync(int playerIndex, Position position, Rotation rotation, int ping);
        Task OnPlayerRevivedAsync(int medicIndex, int reviveeIndex);
        Task OnPlayerScoreAsync(int index, int score, int teamScore, int kills, int deaths);
        Task OnPlayerSpawnAsync(int index, Position pos, Rotation rot);
        Task OnPlayerTeamkilledAsync(int attackerIndex, Position attackerPos, int victimIndex, Position victimPos);
        Task OnProjectilePositionUpdateAsync(int id, string templateName, Position position, Rotation rotation);
        Task OnServerInfoAsync(string serverName, string maps, int gamePort, int queryPort, int maxPlayers);
        Task OnTicketStatusAsync(string team1Name, int team1Tickets, string team2Name, int team2Tickets, string mapName);
        Task OnVehicleDestroyedAsync(int vehicleId, string vehicleName);
    }
}
