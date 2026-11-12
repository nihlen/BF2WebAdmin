using BF2WebAdmin.Server.Communication.DTOs;

namespace BF2WebAdmin.Server.Communication.Events;

public class ServerUpdateEvent : ServerDto
{
    public DateTimeOffset TimeStamp { get; set; }
}