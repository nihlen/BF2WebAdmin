using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;

namespace BF2WebAdmin.Server.Abstractions;

public interface IGameWriter
{
    double CurrentTrackerInterval { get; }

    Task<string> GetRconResponseAsync(string command);
    void SetRconResponse(string responseCode, string value);
    void SendGameEvent(Player player, int eventType, int data);
    void SendHealth(Player player, int health);
    void SendHudEvent(Player player, int eventType, int data);
    void SendMedal(Player player, int medalNumber, int medalValue);
    void SendPrivateMessage(Player player, string message);
    void SendRank(Player player, Rank rank, bool rankEvent);
    void SendRcon(params string[] command);
    void SendRotate(Player player, Rotation rotation);
    void SendScore(Player player, int totalScore, int teamScore, int kills, int deaths);
    void SendTeam(Player player, int teamId);
    void SendTeleport(Player player, Position position);
    void SendText(string text, bool useServerName = true, bool sanitize = true);
    void SendTimerInterval(double timerInterval);
    void SendHeartbeat();
}