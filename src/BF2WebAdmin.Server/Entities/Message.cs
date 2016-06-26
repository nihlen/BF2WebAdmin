using System;
using BF2WebAdmin.Server.Entities.Game;

namespace BF2WebAdmin.Server.Entities
{
    public class Message
    {
        public Player Player { get; set; }
        public DateTime Time { get; set; }
        public MessageType Type { get; set; }
        public string Channel { get; set; }
        public string Flags { get; set; }

        private string _text;
        public string Text
        {
            get { return _text; }
            set
            {
                _text = value.Replace("�", "§").Replace("§C1001", "").Replace("§3", "").Trim();
            }
        }
    }

    public enum MessageType
    {
        Server,
        Player
    }
}
