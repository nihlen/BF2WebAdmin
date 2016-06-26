using System;
using System.Collections.Generic;
using System.Reflection;
using BF2WebAdmin.Server.Entities;
using BF2WebAdmin.Server.Entities.Game;
using log4net;

namespace BF2WebAdmin.Server
{
    public class EventParser
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Dictionary<string, Action<string[]>> _eventHandlers;

        public EventParser(GameServer server, IEventHandler eventHandler)
        {
            _eventHandlers = GetEventsHandlers(eventHandler);
        }

        public void ParseMessage(string message)
        {
            var parts = message.Split('\t');
            var eventType = parts[0];

            if (_eventHandlers.ContainsKey(eventType))
            {
                var eventHandler = _eventHandlers[eventType];
                eventHandler(parts);
            }
            else
            {
                Log.Error($"Unknown server event: {eventType}");
            }
        }

        private static Dictionary<string, Action<string[]>> GetEventsHandlers(IEventHandler eh)
        {
            return new Dictionary<string, Action<string[]>>
            {
                // Server events
                { "serverInfo", p => eh.OnServerInfo(p[1], p[2], Int(p[3]), Int(p[4]), Int(p[5])) },

                // Game status events
                { "gameStatePreGame", p => eh.OnGameStatePreGame() },
                { "gameStatePlaying", p => eh.OnGameStatePlaying(p[1], p[2], p[3], Int(p[4])) },
                { "gameStateEndGame", p => eh.OnGameStateEndGame(p[1], Int(p[2]), p[3], Int(p[4]), p[5]) },
                { "gameStatePaused", p => eh.OnGameStatePaused() },
                { "gameStateRestart", p => eh.OnGameStateRestart() },
                { "gameStateNotConnected", p => eh.OnGameStateNotConnected() },

                // Game events
                { "controlPointCapture", p => eh.OnControlPointCapture(Int(p[1]), p[2]) },
                { "controlPointNeutralised", p => eh.OnControlPointNeutralised(p[1]) },

                // Timer events
                { "ticketStatus", p => eh.OnTicketStatus(p[1], Int(p[2]), p[3], Int(p[4]), p[5]) },
                { "playerPositionUpdate", p => eh.OnPlayerPositionUpdate(Int(p[1]), Pos(p[2]), Rot(p[3]), Int(p[4])) },
                { "projectilePositionUpdate", p => eh.OnProjectilePositionUpdate(Int(p[1]), p[2], Pos(p[3]), Rot(p[4])) },

                // Player events
                { "playerConnect", p => eh.OnPlayerConnect(Int(p[1]), p[2], Int(p[3]), p[4], p[5], Int(p[6])) },
                { "playerSpawn", p => eh.OnPlayerSpawn(Int(p[1]), Pos(p[2])) },
                { "playerChangeTeam", p => eh.OnPlayerChangeTeam(Int(p[1]), Int(p[2])) },
                { "playerScore", p => eh.OnPlayerScore(Int(p[1]), Int(p[2]), Int(p[3]), Int(p[4]), Int(p[5])) },
                { "playerRevived", p => eh.OnPlayerRevived(Int(p[1]), Int(p[2]), Int(p[3]), Int(p[4]), Int(p[5]), Int(p[6]), Int(p[7]), Int(p[8])) },
                { "playerKilledSelf", p => eh.OnPlayerKilledSelf(Int(p[1]), Pos(p[2]), Int(p[3]), Int(p[4]), Int(p[5])) },
                { "playerTeamkilled", p => eh.OnPlayerTeamkilled(Int(p[1]), Pos(p[2]), Int(p[3]), Int(p[4]), Int(p[5]), Int(p[6]), Pos(p[7]), Int(p[8]), Int(p[9]), Int(p[10])) },
                { "playerKilled", p => eh.OnPlayerKilled(Int(p[1]), Pos(p[2]), Int(p[3]), Int(p[4]), Int(p[5]), Int(p[6]), Pos(p[7]), Int(p[8]), Int(p[9]), Int(p[10]), p[11]) },
                { "playerDeath", p => eh.OnPlayerDeath(Int(p[1]), Pos(p[2]), Int(p[3]), Int(p[4]), Int(p[5])) },
                { "playerDisconnect", p => eh.OnPlayerDisconnect(Int(p[1])) },

                // Vehicle events
                { "enterVehicle", p => eh.OnEnterVehicle(Int(p[1]), Int(p[2]), p[3], p[4]) },
                { "exitVehicle", p => eh.OnExitVehicle(Int(p[1]), Int(p[2]), p[3], p[4]) },
                { "vehicleDestroyed", p => eh.OnVehicleDestroyed(Int(p[1]), p[2]) },

                // Chat events
                { "chatServer", p => eh.OnChatServer(p[1], p[2], p[3]) },
                { "chatPlayer", p => eh.OnChatPlayer(p[1], p[2], Int(p[3]), p[4]) }
            };
        }

        private static int Int(string arg) => int.Parse(arg);
        private static Position Pos(string arg) => Position.Parse(arg);
        private static Rotation Rot(string arg) => Rotation.Parse(arg);
    }
}
