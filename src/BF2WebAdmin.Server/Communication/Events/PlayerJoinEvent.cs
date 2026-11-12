using BF2WebAdmin.Server.Communication.DTOs;

namespace BF2WebAdmin.Server.Communication.Events;

public class PlayerJoinEvent
{
    public PlayerDto Player { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}