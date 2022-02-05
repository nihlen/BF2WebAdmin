namespace BF2WebAdmin.Shared.Communication.DTOs;

public class ChatLogDto
{
    public MessageDto Message { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
