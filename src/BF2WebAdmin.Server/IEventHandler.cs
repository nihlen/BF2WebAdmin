using BF2WebAdmin.Common.Entities.Game;

namespace BF2WebAdmin.Server
{
    public interface IEventHandler
    {
        void OnChatPlayer(string channel, string flags, int index, string text);
        void OnChatServer(string channel, string flags, string text);
        void OnControlPointCapture(int teamId, string cpName);
        void OnControlPointNeutralised(string cpName);
        void OnEnterVehicle(int index, int vehicleId, string rootVehicleName, string vehicleName);
        void OnExitVehicle(int index, int vehicleId, string rootVehicleName, string vehicleName);
        void OnGameStateEndGame(string team1Name, int team1Tickets, string team2Name, int team2Tickets, string mapName);
        void OnGameStateNotConnected();
        void OnGameStatePaused();
        void OnGameStatePlaying(string team1Name, string team2Name, string mapName, int maxPlayers);
        void OnGameStatePreGame();
        void OnGameStateRestart();
        void OnPlayerChangeTeam(int index, int teamId);
        void OnPlayerConnect(int index, string name, int pid, string ipAddress, string hash, int teamId);
        void OnPlayerDeath(int index, Position pos, int score, int kills, int deaths);
        void OnPlayerDisconnect(int index);
        void OnPlayerKilled(int attackerIndex, Position attackerPos, int attackerScore, int attackerKills, int attackerDeaths, int victimIndex, Position victimPos, int victimScore, int victimKills, int victimDeaths, string weapon);
        void OnPlayerKilledSelf(int index, Position pos, int score, int kills, int deaths);
        void OnPlayerPositionUpdate(int playerIndex, Position position, Rotation rotation, int ping);
        void OnPlayerRevived(int medicIndex, int medicScore, int medicKills, int medicDeaths, int reviveeIndex, int reviveeScore, int reviveeKills, int reviveeDeaths);
        void OnPlayerScore(int index, int score, int teamScore, int kills, int deaths);
        void OnPlayerSpawn(int index, Position pos);
        void OnPlayerTeamkilled(int attackerIndex, Position attackerPos, int attackerScore, int attackerKills, int attackerDeaths, int victimIndex, Position victimPos, int victimScore, int victimKills, int victimDeaths);
        void OnProjectilePositionUpdate(int id, string templateName, Position position, Rotation rotation);
        void OnServerInfo(string serverName, string maps, int gamePort, int queryPort, int maxPlayers);
        void OnTicketStatus(string team1Name, int team1Tickets, string team2Name, int team2Tickets, string mapName);
        void OnVehicleDestroyed(int vehicleId, string vehicleName);
    }
}
