namespace BF2WebAdmin.Server.Abstractions;

public interface IRconClient
{
    Task<string> SendAsync(string command);
    Task<string> SendAsync(IEnumerable<string> commands);
}