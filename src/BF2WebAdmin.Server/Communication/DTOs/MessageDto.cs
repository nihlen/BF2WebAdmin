namespace BF2WebAdmin.Server.Communication.DTOs;

public class MessageDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int? PlayerId { get; set; }
    public string? PlayerName { get; set; }

    /// <summary>The sender's CD-key hash (or a test server's "name:" key), null when it has none; unlike PlayerId it stays theirs after they leave.</summary>
    public string? PlayerHash { get; set; }

    public string? TeamName { get; set; }
    public string Type { get; set; }
    public string Channel { get; set; }
    public string Flags { get; set; }
    public string Text { get; set; }
}