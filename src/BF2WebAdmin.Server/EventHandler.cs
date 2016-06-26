using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using BF2WebAdmin.Server.Entities;
using BF2WebAdmin.Server.Entities.Game;

namespace BF2WebAdmin.Server
{
    public class EventHandler : IEventHandler
    {
        private readonly GameServer _server;

        public EventHandler(GameServer server)
        {
            _server = server;
        }

        /*
         * Server events
         */
        public void OnServerInfo(string serverName, string maps, int gamePort, int queryPort, int maxPlayers)
        {
            throw new NotImplementedException();
        }

        /*
         * Game status events
         */
        public void OnGameStatePreGame()
        {
            throw new NotImplementedException();
        }

        public void OnGameStatePlaying(string team1Name, string team2Name, string mapName, int maxPlayers)
        {
            throw new NotImplementedException();
        }

        public void OnGameStateEndGame(string team1Name, int team1Tickets, string team2Name, int team2Tickets, string mapName)
        {
            throw new NotImplementedException();
        }

        public void OnGameStatePaused()
        {
            throw new NotImplementedException();
        }

        public void OnGameStateRestart()
        {
            throw new NotImplementedException();
        }

        public void OnGameStateNotConnected()
        {
            throw new NotImplementedException();
        }

        /*
         * Game events
         */
        public void OnControlPointCapture(int teamId, string cpName)
        {
            throw new NotImplementedException();
        }

        public void OnControlPointNeutralised(string cpName)
        {
            throw new NotImplementedException();
        }

        /*
         * Timer events
         */
        public void OnTicketStatus(string team1Name, int team1Tickets, string team2Name, int team2Tickets, string mapName)
        {
            throw new NotImplementedException();
        }

        public void OnPlayerPositionUpdate(int playerIndex, Position position, Rotation rotation, int ping)
        {
            throw new NotImplementedException();
        }

        public void OnProjectilePositionUpdate(int id, string templateName, Position position, Rotation rotation)
        {
            throw new NotImplementedException();
        }

        /*
         * Player events
         */
        public void OnPlayerConnect(int index, string name, int pid, string ipAddress, string hash, int teamId)
        {
            var player = new Player
            {
                Name = name,
                Id = pid,
                Index = index,
                IpAddress = IPAddress.Parse(ipAddress),
                Hash = hash,
                Team = new Team { Id = teamId, Name = "Idk", Players = new List<Player>() } // TODO: Get real team from server
            };
            _server.AddPlayer(player);
        }

        public void OnPlayerSpawn(int index, Position pos)
        {
            throw new NotImplementedException();
        }

        public void OnPlayerChangeTeam(int index, int teamId)
        {
            throw new NotImplementedException();
        }

        public void OnPlayerScore(int index, int score, int teamScore, int kills, int deaths)
        {
            throw new NotImplementedException();
        }

        public void OnPlayerRevived(int medicIndex, int medicScore, int medicKills, int medicDeaths,
            int reviveeIndex, int reviveeScore, int reviveeKills, int reviveeDeaths)
        {
            throw new NotImplementedException();
        }

        public void OnPlayerKilledSelf(int index, Position pos, int score, int kills, int deaths)
        {
            throw new NotImplementedException();
        }

        public void OnPlayerTeamkilled(int attackerIndex, Position attackerPos, int attackerScore, int attackerKills, int attackerDeaths,
            int victimIndex, Position victimPos, int victimScore, int victimKills, int victimDeaths)
        {
            throw new NotImplementedException();
        }

        public void OnPlayerKilled(int attackerIndex, Position attackerPos, int attackerScore, int attackerKills, int attackerDeaths,
            int victimIndex, Position victimPos, int victimScore, int victimKills, int victimDeaths, string weapon)
        {
            throw new NotImplementedException();
        }

        public void OnPlayerDeath(int index, Position pos, int score, int kills, int deaths)
        {
            throw new NotImplementedException();
        }

        public void OnPlayerDisconnect(int index)
        {
            var player = _server.Players.First(x => x.Index == index);
            _server.RemovePlayer(player);
        }

        /*
         * Vehicle events
         */
        public void OnEnterVehicle(int index, int vehicleId, string rootVehicleName, string vehicleName)
        {
            throw new NotImplementedException();
        }

        public void OnExitVehicle(int index, int vehicleId, string rootVehicleName, string vehicleName)
        {
            throw new NotImplementedException();
        }

        public void OnVehicleDestroyed(int vehicleId, string vehicleName)
        {
            throw new NotImplementedException();
        }

        /*
         * Chat events	
         */
        public void OnChatServer(string channel, string flags, string text)
        {
            var message = new Message
            {
                Channel = channel,
                Flags = flags,
                Text = text,
                Type = MessageType.Server,
                Time = DateTime.UtcNow
            };
            _server.AddMessage(message);
        }

        public void OnChatPlayer(string channel, string flags, int index, string text)
        {
            var message = new Message
            {
                Player = _server.Players.First(x => x.Index == index),
                Channel = channel,
                Flags = flags,
                Text = text,
                Type = MessageType.Player,
                Time = DateTime.UtcNow
            };
            _server.AddMessage(message);
        }
    }
}
