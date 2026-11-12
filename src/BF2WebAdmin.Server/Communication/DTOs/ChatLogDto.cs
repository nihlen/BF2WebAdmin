namespace BF2WebAdmin.Server.Communication.DTOs;

public class ChatLogDto
{
    public MessageDto Message { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
