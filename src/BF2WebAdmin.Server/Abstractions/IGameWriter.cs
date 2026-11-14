using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.AdminChannel;

namespace BF2WebAdmin.Server.Abstractions;

/// <summary>
/// Commands to the game server. Every command goes out on the admin channel in call order, and the
/// module runs them in arrival order, so raw console lines and typed commands never overtake each other.
/// Fire-and-forget methods log a refusal; the async ones throw <see cref="AdminCommandException"/>.
/// </summary>
public interface IGameWriter
{
    /// <summary>The player position stream interval in seconds (see <see cref="SendTimerInterval"/>).</summary>
    double CurrentTrackerInterval { get; }

    /// <summary>Run raw console lines (one line each) in order.</summary>
    void SendConsole(params string[] lines);

    /// <summary>Run one raw console line and return its output lines.</summary>
    Task<IReadOnlyList<string>> ConsoleAsync(string line);

    void SendText(string text, bool useServerName = true, bool sanitize = true);
    void SendPrivateMessage(Player player, string message);

    /// <summary>Move the player's root object (its vehicle when seated); the rotation is kept.</summary>
    void SendTeleport(Player player, Position position);

    /// <summary>Rotate the player's root object; the position is kept.</summary>
    void SendRotate(Player player, Rotation rotation);

    void SendTeleport(Player player, Position position, Rotation rotation);

    /// <summary>Set the hit points of the player's root object. Zero on an occupied object is refused by the module.</summary>
    void SendHealth(Player player, double hitPoints);

    /// <summary>Set the player's root object's health as a fraction of its maximum (1 = full repair).</summary>
    void SendHealthFraction(Player player, double fraction);

    /// <summary>Kill the player; with <paramref name="destroyVehicle"/> its vehicle is wrecked when nobody else is in it.</summary>
    void SendKill(Player player, bool destroyVehicle = false);

    /// <summary>Move the player to a team, killing it unless <paramref name="kill"/> is false.</summary>
    void SendTeam(Player player, int teamId, bool kill = true);

    /// <summary>
    /// The seat the player wants when teammates spawn into the same vehicle together: the game server spawns them in
    /// that order, so one teammate's preference is enough. It holds until the player leaves the server. False when the
    /// server's module takes no seat preferences or the server is not connected (nothing is sent).
    /// </summary>
    bool SetSeatPreference(Player player, SeatPreference preference);

    void SendScore(Player player, int totalScore, int teamScore, int kills, int deaths);
    void SendRank(Player player, Rank rank, bool rankEvent);
    void SendMedal(Player player, int medalNumber, int medalValue);
    void SendGameEvent(Player player, int eventType, int data);
    void SendHudEvent(Player player, int eventType, int data);

    /// <summary>Kick a connected human player. The module refuses bots (AI-flagged players) with <see cref="AdminErrorCode.Refused"/>.</summary>
    Task KickAsync(Player player, string reason);

    /// <summary>
    /// Ban a connected human player in the engine's ban list (which kicks it; permanent and timed bans are saved in the server's
    /// settings/banlist.con). Bots and a CD-key ban without a hash are refused. <see cref="Services.ServerAdmin"/> also records it.
    /// </summary>
    Task BanAsync(Player player, bool byCdKey, Services.BanLength length, string reason);

    /// <summary>Restart the current round (admin.restartMap).</summary>
    void SendRestartMap();

    /// <summary>Restart the current round and wait for the module's answer; a refusal throws <see cref="AdminChannel.AdminCommandException"/>.</summary>
    Task RestartMapAsync();

    /// <summary>Run the next map of the rotation (admin.runNextLevel).</summary>
    Task RunNextMapAsync();

    /// <summary>Run a map of the rotation by name, mode and size (0 any). A map that is not in the rotation is <see cref="AdminErrorCode.Unavailable"/>.</summary>
    Task RunMapAsync(string name, string mode, int size);

    /// <summary>
    /// Quit the game server cleanly (the console's <c>quit</c>): the engine stops at the next frame and exits with code 0,
    /// so the module unloads and the link closes normally. Whatever runs the server (a container restart policy) starts it again.
    /// </summary>
    Task QuitAsync();

    /// <summary>Make an object the console's active object for the raw console lines that follow (object.active idN).</summary>
    void SendSetObjectActive(long consoleId);

    /// <summary>Disable (freeze) or enable a vehicle and its parts (object.setIsDisabledRecursive).</summary>
    void SetVehicleDisabled(Vehicle vehicle, bool disabled);

    /// <summary>
    /// Give a vehicle and its parts collision back, or take it away (noclip). False when the server's module cannot
    /// (no collision command), the vehicle has no networkable id, or the server is not connected (nothing is sent).
    /// </summary>
    bool SetVehicleCollision(Vehicle vehicle, bool hasCollision);

    /// <summary>The world's gravity (physics.gravity, default <see cref="DefaultGravity"/>).</summary>
    void SetGravity(double gravity);

    const double DefaultGravity = -14.73;

    /// <summary>
    /// Set properties of an object template, in order: <c>ObjectTemplate.activeSafe {type} {template}</c>, then
    /// <c>ObjectTemplate.{property} {value}</c> for each. Every new object of the template has them; existing ones keep theirs.
    /// </summary>
    void SetTemplate(string type, string template, params (string Property, string Value)[] values);

    /// <summary>A property of an object template, as the console prints it (empty when the engine has none).</summary>
    Task<string> GetTemplateValueAsync(string type, string template, string property);

    /// <summary>The engine's ban list lines (admin.listBannedKeys or admin.listBannedAddresses).</summary>
    Task<IReadOnlyList<string>> ListBansAsync(bool byCdKey);

    /// <summary>Take a CD-key hash or an address off the engine's ban list.</summary>
    Task RemoveBanAsync(bool byCdKey, string identifier);

    Task<IReadOnlyList<ObjectListEntry>> ListObjectsAsync(string template, int maxEntries = 4096);
    Task<ObjectSpawned> SpawnObjectAsync(string template, Position position, Rotation rotation, int team = 0);
    void SendDeleteObject(long consoleId);

    /// <summary>Set the player and projectile stream interval in seconds (1/30 to 1).</summary>
    void SendTimerInterval(double timerInterval);

    /// <summary>Ping the module; the reply comes from the game thread, so it also shows the server is ticking.</summary>
    void SendHeartbeat();
}

/// <summary>A player's seat when spawning into a vehicle with teammates (<see cref="IGameWriter.SetSeatPreference"/>).</summary>
public enum SeatPreference
{
    /// <summary>The first seat: a heli's pilot.</summary>
    Pilot = CmdSeatPreference.First,

    /// <summary>The second seat: a heli's gunner.</summary>
    Gunner = CmdSeatPreference.Second,

    None = CmdSeatPreference.None,
}
