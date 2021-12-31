using BF2WebAdmin.Common.Entities.Game;
using MessagePack;
using ProtoBuf;

namespace BF2WebAdmin.Common.Communication.DTOs
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [MessagePackObject(keyAsPropertyName: true)]
    public class MessageDto
    {
        public int PlayerId { get; set; }
        public string Time { get; set; }
        public string Type { get; set; }
        public string Channel { get; set; }
        public string Flags { get; set; }
        public string Text { get; set; }

        public MessageDto() { }

        public MessageDto(Message message)
        {
            PlayerId = message.Player?.Index ?? 0;
            Time = message.Time.ToString("O");
            Type = message.Type.ToString();
            Channel = message.Channel;
            Flags = message.Flags;
            Text = message.Text;
        }
    }
}