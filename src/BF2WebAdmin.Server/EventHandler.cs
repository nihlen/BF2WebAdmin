using System.Net;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;

namespace BF2WebAdmin.Server
{
    public class EventHandler : IEventHandler
    {
        private readonly IGameServer _server;

        public EventHandler(IGameServer server)
        {
            _server = server;
        }

        /*
         * Server events
         */
        public async ValueTask OnServerInfoAsync(string serverName, string maps, int gamePort, int queryPort, int maxPlayers)
        {
            var mapNames = maps.Split(",").Select(l => l.Split("|").FirstOrDefault()).ToList();
            //return Task.WhenAll(
            //    _server.UpdateServerInfoAsync(serverName, gamePort, queryPort, maxPlayers),
            //    _server.UpdateMapsAsync(mapNames)
            //);
            await _server.UpdateServerInfoAsync(serverName, gamePort, queryPort, maxPlayers);
            await _server.UpdateMapsAsync(mapNames);
        }

        /*
         * Game status events
         */
        public ValueTask OnGameStatePreGameAsync()
        {
            return _server.UpdateGameStateAsync(GameState.PreGame);
        }

        public async ValueTask OnGameStatePlayingAsync(string team1Name, string team2Name, string mapName, int maxPlayers)
        {
            //return Task.WhenAll(
            //    _server.UpdateGameStateAsync(GameState.Playing),
            //    _server.UpdateMapAsync(mapName, team1Name, team2Name)
            //);
            await _server.UpdateGameStateAsync(GameState.Playing);
            await _server.UpdateMapAsync(mapName, team1Name, team2Name);
        }

        public ValueTask OnGameStateEndGameAsync(string team1Name, int team1Tickets, string team2Name, int team2Tickets, string mapName)
        {
            return _server.UpdateGameStateAsync(GameState.EndGame);
        }

        public ValueTask OnGameStatePausedAsync()
        {
            return _server.UpdateGameStateAsync(GameState.Paused);
        }

        public ValueTask OnGameStateRestartAsync()
        {
            return _server.UpdateGameStateAsync(GameState.Restart);
        }

        public ValueTask OnGameStateNotConnectedAsync()
        {
            return _server.UpdateGameStateAsync(GameState.NotConnected);
        }

        /*
         * Game events
         */
        public ValueTask OnControlPointCaptureAsync(int teamId, string cpName)
        {
            throw new NotImplementedException();
        }

        public ValueTask OnControlPointNeutralisedAsync(string cpName)
        {
            throw new NotImplementedException();
        }

        /*
         * Timer events
         */
        public ValueTask OnTicketStatusAsync(string team1Name, int team1Tickets, string team2Name, int team2Tickets, string mapName)
        {
            throw new NotImplementedException();
        }

        public ValueTask OnPlayerPositionUpdateAsync(int playerIndex, Position position, Rotation rotation, int ping)
        {
            var player = _server.GetPlayer(playerIndex);
            return _server.UpdatePlayerAsync(player, position, rotation, ping);
        }

        public ValueTask OnProjectilePositionUpdateAsync(int id, string templateName, Position position, Rotation rotation)
        {
            var projectile = _server.GetProjectile(id, templateName, position);
            return _server.UpdateProjectileAsync(projectile, position, rotation);
        }

        /*
         * Player events
         */
        public ValueTask OnPlayerConnectAsync(int index, string name, int pid, string ipAddress, string hash, int teamId)
        {
            var player = new Player
            {
                Name = name,
                Id = pid,
                Index = index,
                IpAddress = IPAddress.Parse(ipAddress),
                Hash = hash,
                Team = _server.Teams.First(t => t.Id == teamId)
            };
            return _server.AddPlayerAsync(player);
        }

        public ValueTask OnPlayerSpawnAsync(int index, Position pos, Rotation rot)
        {
            var player = _server.GetPlayer(index);
            return _server.SetPlayerSpawnAsync(player, pos, rot);
        }

        public ValueTask OnPlayerChangeTeamAsync(int index, int teamId)
        {
            var player = _server.GetPlayer(index);
            return _server.UpdatePlayerTeamAsync(player, teamId);
        }

        public ValueTask OnPlayerScoreAsync(int index, int score, int teamScore, int kills, int deaths)
        {
            var player = _server.GetPlayer(index);
            return _server.UpdatePlayerScoreAsync(player, teamScore, kills, deaths, score);
        }

        public ValueTask OnPlayerRevivedAsync(int medicIndex, int reviveeIndex)
        {
            throw new NotImplementedException();
        }

        public ValueTask OnPlayerKilledSelfAsync(int index, Position pos)
        {
            throw new NotImplementedException();
        }

        public ValueTask OnPlayerTeamkilledAsync(int attackerIndex, Position attackerPos, int victimIndex, Position victimPos)
        {
            throw new NotImplementedException();
        }

        public ValueTask OnPlayerKilledAsync(int attackerIndex, Position attackerPos, int victimIndex, Position victimPos, string weapon)
        {
            var attacker = _server.GetPlayer(attackerIndex);
            var victim = _server.GetPlayer(victimIndex);
            return _server.SetPlayerKillAsync(attacker, attackerPos, victim, victimPos, weapon);
        }

        public ValueTask OnPlayerDeathAsync(int index, Position pos)
        {
            var player = _server.GetPlayer(index);
            return _server.SetPlayerDeathAsync(player, pos);
        }

        public ValueTask OnPlayerDisconnectAsync(int index)
        {
            var player = _server.GetPlayer(index);
            return _server.RemovePlayerAsync(player);
        }

        /*
         * Vehicle events
         */
        public ValueTask OnEnterVehicleAsync(int index, int rootVehicleId, string rootVehicleName, string vehicleName)
        {
            if (vehicleName.Contains("soldier"))
                return ValueTask.CompletedTask;

            var player = _server.GetPlayer(index);
            var vehicle = _server.GetVehicle(player, rootVehicleId, rootVehicleName, vehicleName);
            return _server.UpdatePlayerVehicleAsync(player, vehicle, vehicleName);
        }

        public ValueTask OnExitVehicleAsync(int index, int rootVehicleId, string rootVehicleName, string vehicleName)
        {
            var player = _server.GetPlayer(index);
            return _server.UpdatePlayerVehicleAsync(player, null, null);
        }

        public ValueTask OnVehicleDestroyedAsync(int vehicleId, string vehicleName)
        {
            // vehicleId is currently always -1 here
            //throw new NotImplementedException();
            return ValueTask.CompletedTask;
        }

        /*
         * Chat events	
         */
        public ValueTask OnChatServerAsync(string channel, string flags, string text)
        {
            var message = new Message
            {
                Channel = channel,
                Flags = flags,
                Text = text,
                Type = MessageType.Server,
                Time = DateTime.UtcNow
            };
            return _server.AddMessageAsync(message);
        }

        public ValueTask OnChatPlayerAsync(string channel, string flags, int index, string text)
        {
            var message = new Message
            {
                Player = _server.GetPlayer(index),
                Channel = channel,
                Flags = flags,
                Text = text,
                Type = MessageType.Player,
                Time = DateTime.UtcNow
            };
            return _server.AddMessageAsync(message);
        }
    }
}
