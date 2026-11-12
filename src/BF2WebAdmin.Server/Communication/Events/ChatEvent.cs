using BF2WebAdmin.Server.Communication.DTOs;

namespace BF2WebAdmin.Server.Communication.Events;

public class ChatEvent
{
    public MessageDto Message { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}