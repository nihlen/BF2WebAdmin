using System.Diagnostics;
using System.Text.RegularExpressions;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Entities;

namespace BF2WebAdmin.Server.AdminChannel;

/// <summary>
/// <see cref="IGameWriter"/> over the server's current admin channel link. Every call queues one frame on the
/// link in call order. While the server is disconnected, fire-and-forget commands are dropped (logged) and
/// awaited ones throw <see cref="AdminCommandException"/>.
/// </summary>
public class AdminGameWriter : IGameWriter
{
    private const double DefaultTrackerInterval = 0.25;
    private readonly GameServer _server;
    private readonly ILogger<AdminGameWriter> _logger;

    public AdminGameWriter(GameServer server, ILogger<AdminGameWriter> logger)
    {
        _server = server;
        _logger = logger;
    }

    public double CurrentTrackerInterval { get; private set; } = DefaultTrackerInterval;

    /// <summary>A stream rate in Hz for an interval in seconds, 1-30 (the module ticks at 30 Hz).</summary>
    public static byte RateHz(double interval) => (byte)Math.Clamp((int)Math.Round(1 / Math.Max(interval, 1.0 / 30)), 1, 30);

    private void Fire(Func<uint, AdminMessage> create)
    {
        var session = _server.Session;
        if (session == null || !session.IsOpen)
        {
            _logger.LogDebug("Admin channel {ServerId}: not connected, dropped {Command}", _server.Id, create(0).Type);
            return;
        }

        session.Fire(create);
    }

    private Task<AdminMessage> RequestAsync(Func<uint, AdminMessage> create, TimeSpan? timeout = null)
    {
        var session = _server.Session;
        if (session == null || !session.IsOpen)
            throw new AdminCommandException($"{_server.Id} is not connected");

        return session.RequestAsync(create, timeout);
    }

    private static byte Id(Player player) => (byte)player.Index;

    private static ObjectRef Root(Player player) => ObjectRef.Player(player.Index);

    private static bool IsReal(Player player) => player.Index is >= 0 and <= 255 && player.Name != null;

    public void SendConsole(params string[] lines)
    {
        foreach (var line in lines)
            Fire(id => new CmdConsole(id, line));
    }

    public async Task<IReadOnlyList<string>> ConsoleAsync(string line)
    {
        var result = (ConsoleResult)await RequestAsync(id => new CmdConsole(id, line));
        if ((result.Flags & ConsoleResult.FlagTruncated) != 0)
            _logger.LogWarning("Admin channel {ServerId}: console output of '{Line}' was truncated", _server.Id, line);
        return result.Lines;
    }

    public void SendText(string text, bool useServerName = true, bool sanitize = true)
    {
        const int maxLength = 180;

        if (sanitize)
        {
            text = Regex.Replace(text, @"[\r\n\t\[\]]", "", RegexOptions.Compiled);
            text = text.Replace("\"", "'");
        }

        text = text.Length > maxLength ? text[..maxLength] + "..." : text;
        GameTextCapture.Add(text);

        if (useServerName)
        {
            text = $"[§C1001netsky§C1001] {text}";
        }

        Fire(id => new CmdSay(id, CmdSay.TargetAll, 0, text));
    }

    public void SendPrivateMessage(Player player, string message)
    {
        // A web or Discord user is a player with index -1: the answer only reaches it through the capture
        GameTextCapture.Add(IsReal(player) ? $"(to {player.Name}) {message}" : message);
        if (IsReal(player))
            Fire(id => new CmdSay(id, CmdSay.TargetPlayer, Id(player), message));
    }

    public void SendTeleport(Player player, Position position) =>
        Teleport(player, CmdTeleport.FlagPosition, position, null);

    public void SendRotate(Player player, Rotation rotation) =>
        Teleport(player, CmdTeleport.FlagRotation, null, rotation);

    public void SendTeleport(Player player, Position position, Rotation rotation) =>
        Teleport(player, CmdTeleport.FlagPosition | CmdTeleport.FlagRotation, position, rotation);

    private void Teleport(Player player, byte flags, Position? position, Rotation? rotation)
    {
        if (!IsReal(player))
            return;

        var p = position != null ? AdminStateApplier.ToVec3(position) : default;
        var r = rotation != null ? AdminStateApplier.ToVec3(rotation) : default;
        Fire(id => new CmdTeleport(id, Root(player), flags, p, r));
    }

    public void SendHealth(Player player, double hitPoints)
    {
        if (IsReal(player))
            Fire(id => new CmdSetHealth(id, Root(player), 0, (float)hitPoints));
    }

    public void SendHealthFraction(Player player, double fraction)
    {
        if (IsReal(player))
            Fire(id => new CmdSetHealth(id, Root(player), CmdSetHealth.FlagFraction, (float)fraction));
    }

    public void SendKill(Player player, bool destroyVehicle = false)
    {
        if (IsReal(player))
            Fire(id => new CmdKillPlayer(id, Id(player), destroyVehicle ? CmdKillPlayer.FlagDestroyVehicle : (byte)0));
    }

    public void SendTeam(Player player, int teamId, bool kill = true)
    {
        if (IsReal(player))
            Fire(id => new CmdSetTeam(id, Id(player), (byte)teamId, kill ? CmdSetTeam.FlagKill : (byte)0));
    }

    public void SendScore(Player player, int totalScore, int teamScore, int kills, int deaths)
    {
        const byte mask = CmdSetScore.MaskScore | CmdSetScore.MaskTeamScore | CmdSetScore.MaskKills | CmdSetScore.MaskDeaths;
        if (IsReal(player))
            Fire(id => new CmdSetScore(id, Id(player), mask, totalScore, teamScore, kills, deaths, 0));
    }

    public void SendRank(Player player, Rank rank, bool rankEvent)
    {
        if (IsReal(player))
            Fire(id => new CmdRank(id, Id(player), (byte)rank, rankEvent ? (byte)1 : (byte)0));
    }

    public void SendMedal(Player player, int medalNumber, int medalValue)
    {
        if (IsReal(player))
            Fire(id => new CmdMedal(id, Id(player), medalNumber, medalValue));
    }

    public void SendGameEvent(Player player, int eventType, int data)
    {
        if (IsReal(player))
            Fire(id => new CmdGameEvent(id, Id(player), (byte)eventType, data));
    }

    public void SendHudEvent(Player player, int eventType, int data)
    {
        if (IsReal(player))
            Fire(id => new CmdHudEvent(id, Id(player), (byte)eventType, data));
    }

    public async Task KickAsync(Player player, string reason)
    {
        if (!IsReal(player))
            throw new AdminCommandException($"no player {player.Index}", AdminErrorCode.UnknownPlayer);

        await RequestAsync(id => new CmdKick(id, Id(player), reason));
    }

    public async Task BanAsync(Player player, bool byCdKey, TimeSpan? duration, string reason)
    {
        if (!IsReal(player))
            throw new AdminCommandException($"no player {player.Index}", AdminErrorCode.UnknownPlayer);

        var (kind, seconds) = duration == null ? (CmdBan.DurationPermanent, 0u) : (CmdBan.DurationSeconds, (uint)duration.Value.TotalSeconds);
        await RequestAsync(id => new CmdBan(id, Id(player), byCdKey ? CmdBan.ByCdKey : CmdBan.ByAddress, kind, seconds, reason));
    }

    public void SendRestartMap() => Fire(id => new CmdMap(id, CmdMap.ActionRestart, 0, "", "", 0));

    public Task RestartMapAsync() => RequestAsync(id => new CmdMap(id, CmdMap.ActionRestart, 0, "", "", 0));

    public Task RunNextMapAsync() => RequestAsync(id => new CmdMap(id, CmdMap.ActionNext, 0, "", "", 0));

    public Task RunMapAsync(string name, string mode, int size) =>
        RequestAsync(id => new CmdMap(id, CmdMap.ActionByName, 0, name, mode, (ushort)Math.Clamp(size, 0, ushort.MaxValue)));

    public async Task QuitAsync()
    {
        if (_server.Session is not { IsOpen: true })
            throw new AdminCommandException($"{_server.Id} is not connected");

        try
        {
            // The answer ("*** Game will exit! **") is queued before the engine stops, but the link may close first
            await RequestAsync(id => new CmdConsole(id, "quit"), TimeSpan.FromSeconds(5));
        }
        catch (AdminCommandException e) when (e.Code is null)
        {
            // No reply, or the link closed: the server is quitting
            _logger.LogInformation("Admin channel {ServerId}: no answer to quit ({Message})", _server.Id, e.Message);
        }
    }

    public void SendSetObjectActive(long consoleId)
    {
        if (consoleId < 0)
        {
            _logger.LogWarning("Admin channel {ServerId}: SetObjectActive without a console id", _server.Id);
            return;
        }

        Fire(id => new CmdSetObjectActive(id, ObjectRef.Console((uint)consoleId)));
    }

    public async Task<IReadOnlyList<ObjectListEntry>> ListObjectsAsync(string template, int maxEntries = 4096)
    {
        var result = (ObjectList)await RequestAsync(id => new CmdListObjects(id, template, (ushort)Math.Clamp(maxEntries, 1, 4096)));
        if ((result.Flags & ObjectList.FlagMore) != 0)
            _logger.LogWarning("Admin channel {ServerId}: more than {Max} objects of {Template}", _server.Id, maxEntries, template);
        return result.Objects;
    }

    public async Task<ObjectSpawned> SpawnObjectAsync(string template, Position position, Rotation rotation, int team = 0)
    {
        return (ObjectSpawned)await RequestAsync(id =>
            new CmdSpawnObject(id, template, AdminStateApplier.ToVec3(position), AdminStateApplier.ToVec3(rotation), (byte)team));
    }

    public void SendDeleteObject(long consoleId)
    {
        if (consoleId >= 0)
            Fire(id => new CmdDeleteObject(id, ObjectRef.Console((uint)consoleId)));
    }

    public void SendTimerInterval(double timerInterval)
    {
        CurrentTrackerInterval = timerInterval;
        // At this rate, or at 30 Hz while a 3D viewer is open
        _server.Applier.Resubscribe();
    }

    public void SendHeartbeat()
    {
        var session = _server.Session;
        if (session == null)
            return;

        _ = Task.Run(async () =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await session.RequestAsync(id => new Ping(id, (ulong)Environment.TickCount64));
                _logger.LogInformation("Heartbeat response in {ElapsedMs} ms", sw.ElapsedMilliseconds);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Failed to send heartbeat ({Duration} s since start)", (DateTime.UtcNow - _server.StartTime).TotalSeconds);
            }
        });
    }
}
