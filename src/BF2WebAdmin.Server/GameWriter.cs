using System;
using System.IO;
using System.Reflection;
using System.Text;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using log4net;

namespace BF2WebAdmin.Server
{
    public class GameWriter : IGameWriter
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly BinaryWriter _writer;
        private int _responseCounter;

        public GameWriter(BinaryWriter writer)
        {
            _writer = writer;
        }

        private void Send(string message)
        {
            if (_writer == null || !_writer.BaseStream.CanWrite)
                throw new IOException($"Cannot write to {_writer}");

            // TODO: Batch send?
            try
            {
                var bytes = Encoding.UTF8.GetBytes(message + "\n");
                lock (_writer)
                {
                    _writer.Write(bytes);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Write error for server", ex);
            }
        }

        // Run RCon command
        public void SendRcon(string command)
        {
            Send($"rcon {command}");
        }

        // Run RCon command and get response
        public void GetRconResponse(string command)
        {
            var responseCode = "response" + _responseCounter++;
            Send($"rconresponse {responseCode} {command}");
        }

        // PM a player using the rcon feedback function (message appears in their console)
        public void SendPrivateMessage(Player player, string message)
        {
            Send($"pm {player.Index} {message}");
        }

        // Teleport a player
        public void SendTeleport(Player player, Position position)
        {
            Send($"position {player.Index} {position.X} {position.Height} {position.Y}");
        }

        // Rotate a player
        public void SendRotate(Player player, Rotation rotation)
        {
            Send($"rotation {player.Index} {rotation.Yaw} {rotation.Pitch} {rotation.Roll}");
        }

        // Set player health (vehicle damage)
        public void SendHealth(Player player, int health)
        {
            Send($"damage {player.Index} {health}");
        }

        // Set player rank with optional event
        public void SendRank(Player player, Rank rank, bool rankEvent)
        {
            var evnt = rankEvent ? 1 : 0;
            Send($"rank {player.Index} {(int)rank} {evnt}");
        }

        // Give player a medal award
        public void SendMedal(Player player, int medalNumber, int medalValue)
        {
            Send($"medal {player.Index} {medalNumber} {medalValue}");
        }

        // Send a game event
        public void SendGameEvent(Player player, int eventType, int data)
        {
            Send($"gameevent {player.Index} {eventType} {data}");
        }

        // Send a HUD event
        public void SendHudEvent(Player player, int eventType, int data)
        {
            Send($"hudevent {player.Index} {eventType} {data}");
        }

        // Set score
        public void SendScore(Player player, int totalScore, int teamScore, int kills, int deaths)
        {
            Send($"score {player.Index} {totalScore} {teamScore} {kills} {deaths}");
        }

        // Set team
        public void SendTeam(Player player, int teamId)
        {
            Send($"team {player.Index} {teamId}");
        }
    }
}