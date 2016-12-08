using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Logging;
using Microsoft.Extensions.Logging;

namespace BF2WebAdmin.Server
{
    public class GameWriter : IGameWriter
    {
        private static ILogger Logger { get; } = ApplicationLogging.CreateLogger<GameWriter>();

        private readonly BinaryWriter _writer;
        private int _responseCounter;

        private readonly Encoding _encoding;

        public GameWriter(BinaryWriter writer)
        {
            _writer = writer;

            // Battlefield 2 does not support UTF-8, only what seems like Windows-1252
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _encoding = Encoding.GetEncoding(1252);
        }

        private void Send(string message)
        {
            if (_writer == null || !_writer.BaseStream.CanWrite)
                throw new IOException($"Cannot write to {_writer}");

            // TODO: Batch send?
            try
            {
                //var bytes = Encoding.UTF8.GetBytes(message + "\n");
                var bytes = _encoding.GetBytes(message + "\n");
                lock (_writer)
                {
                    _writer.Write(bytes);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Write error for server", ex);
            }
        }

        /// <summary>
        /// Run RCon command
        /// </summary>
        /// <param name="command">Battlefield 2 rcon command</param>
        public void SendRcon(string command)
        {
            Send($"rcon {command}");
        }

        /// <summary>
        /// Run RCon command and get response
        /// </summary>
        /// <param name="command">Battlefield 2 rcon command</param>
        public void GetRconResponse(string command)
        {
            var responseCode = "response" + _responseCounter++;
            Send($"rconresponse {responseCode} {command}");
        }

        /// <summary>
        /// PM a player using the rcon feedback function (message appears in their console)
        /// </summary>
        /// <param name="player">Player on the server to send to</param>
        /// <param name="message">Message</param>
        public void SendPrivateMessage(Player player, string message)
        {
            Send($"pm {player.Index} {message}");
        }

        /// <summary>
        /// Teleport a player
        /// </summary>
        /// <param name="player">Player to teleport</param>
        /// <param name="position">New position</param>
        public void SendTeleport(Player player, Position position)
        {
            Send($"position {player.Index} {position.X} {position.Height} {position.Y}");
        }

        /// <summary>
        /// Rotate a player
        /// </summary>
        /// <param name="player">Player to rotate</param>
        /// <param name="rotation">New rotation</param>
        public void SendRotate(Player player, Rotation rotation)
        {
            Send($"rotation {player.Index} {rotation.Yaw} {rotation.Pitch} {rotation.Roll}");
        }

        /// <summary>
        /// Set player health (vehicle damage)
        /// </summary>
        /// <param name="player">Player to set health on</param>
        /// <param name="health">New health (range varies)</param>
        public void SendHealth(Player player, int health)
        {
            Send($"damage {player.Index} {health}");
        }

        /// <summary>
        /// Set player rank with optional event
        /// </summary>
        /// <param name="player">Player to change rank on</param>
        /// <param name="rank">New rank 0-21</param>
        /// <param name="rankEvent">Whether to send a rank up event or not</param>
        public void SendRank(Player player, Rank rank, bool rankEvent)
        {
            var evnt = rankEvent ? 1 : 0;
            Send($"rank {player.Index} {(int)rank} {evnt}");
        }

        /// <summary>
        /// Give player a medal award
        /// </summary>
        /// <param name="player">Player to award</param>
        /// <param name="medalNumber"></param>
        /// <param name="medalValue"></param>
        public void SendMedal(Player player, int medalNumber, int medalValue)
        {
            Send($"medal {player.Index} {medalNumber} {medalValue}");
        }

        /// <summary>
        /// Send a game event
        /// </summary>
        /// <param name="player">Player to send event to</param>
        /// <param name="eventType"></param>
        /// <param name="data"></param>
        public void SendGameEvent(Player player, int eventType, int data)
        {
            Send($"gameevent {player.Index} {eventType} {data}");
        }

        /// <summary>
        /// Send a HUD event
        /// </summary>
        /// <param name="player">Player to send HUD event to</param>
        /// <param name="eventType"></param>
        /// <param name="data"></param>
        public void SendHudEvent(Player player, int eventType, int data)
        {
            Send($"hudevent {player.Index} {eventType} {data}");
        }

        /// <summary>
        /// Set score
        /// </summary>
        /// <param name="player">Player to change score on</param>
        /// <param name="totalScore">New total score</param>
        /// <param name="teamScore">New team score</param>
        /// <param name="kills">New kill count</param>
        /// <param name="deaths">New death count</param>
        public void SendScore(Player player, int totalScore, int teamScore, int kills, int deaths)
        {
            Send($"score {player.Index} {totalScore} {teamScore} {kills} {deaths}");
        }

        /// <summary>
        /// Change player team
        /// </summary>
        /// <param name="player">Player to change team on</param>
        /// <param name="teamId">New team id</param>
        public void SendTeam(Player player, int teamId)
        {
            Send($"team {player.Index} {teamId}");
        }

        /// <summary>
        /// Send text (convenience function)
        /// </summary>
        /// <param name="text">Text to send to server chat</param>
        public void SendText(string text)
        {
            var maxLength = 180;
            text = Regex.Replace(text, @"[\r\n\t\[\]]", "", RegexOptions.Compiled);
            text = text.Replace("\"", "'");
            text = text.Length > maxLength ? text.Substring(0, maxLength) + "..." : text;
            SendRcon($"game.sayall \"{text}\"");
        }
    }
}