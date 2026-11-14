using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Constants;
using BF2WebAdmin.Server.Entities;
using BF2WebAdmin.Server.Services;

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
    private readonly string? _chatPrefix;

    /// <param name="chatPrefix">The name in brackets before WebAdmin's chat lines (setting ServerSettings:ChatPrefix); empty for none.</param>
    public AdminGameWriter(GameServer server, ILogger<AdminGameWriter> logger, string? chatPrefix = DefaultChatPrefix)
    {
        _server = server;
        _logger = logger;
        _chatPrefix = chatPrefix;
    }

    public const string DefaultChatPrefix = "netsky";

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

    /// <summary>The most characters of a chat line WebAdmin sends, before the prefix.</summary>
    public const int MaxTextLength = 180;

    public void SendText(string text, bool useServerName = true, bool sanitize = true)
    {
        if (sanitize)
        {
            text = Regex.Replace(text, @"[\r\n\t\[\]]", "", RegexOptions.Compiled);
            text = text.Replace("\"", "'");
        }

        text = Truncate(text, MaxTextLength);
        GameTextCapture.Add(text);

        if (useServerName && !string.IsNullOrEmpty(_chatPrefix))
        {
            text = $"[§C1001{_chatPrefix}§C1001] {text}";
        }

        Fire(id => new CmdSay(id, CmdSay.TargetAll, 0, text));
    }

    public void SendPrivateMessage(Player player, string message)
    {
        // A web or Discord user is a player with index -1: the answer only reaches it through the capture, whole
        GameTextCapture.Add(IsReal(player) ? $"(to {player.Name}) {message}" : message);
        if (!IsReal(player))
            return;

        // One chat line, like SendText's
        var line = Truncate(Regex.Replace(message, @"[\r\n\t]+", " ", RegexOptions.Compiled), MaxTextLength);
        Fire(id => new CmdSay(id, CmdSay.TargetPlayer, Id(player), line));
    }

    /// <summary>
    /// At most <paramref name="maxLength"/> characters plus "...", cut before a colour code (§C1001, six characters)
    /// that would not fit whole.
    /// </summary>
    public static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength)
            return text;

        var cut = maxLength;
        var code = text.LastIndexOf('§', cut - 1, Math.Min(cut, ColorCodeLength - 1));
        if (code >= 0)
            cut = code;

        return text[..cut] + "...";
    }

    private const int ColorCodeLength = 6;

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

    public bool SetSeatPreference(Player player, SeatPreference preference)
    {
        if (!IsReal(player) || _server.Session is not { IsOpen: true } session || (session.Capabilities & Welcome.CapSeats) == 0)
            return false;

        session.Fire(id => new CmdSeatPreference(id, Id(player), (byte)preference));
        return true;
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

    public async Task BanAsync(Player player, bool byCdKey, BanLength length, string reason)
    {
        if (!IsReal(player))
            throw new AdminCommandException($"no player {player.Index}", AdminErrorCode.UnknownPlayer);

        var (kind, seconds) = length.Kind switch
        {
            BanKind.Round => (CmdBan.DurationRound, 0u),
            BanKind.Timed => (CmdBan.DurationSeconds, (uint)length.Duration.TotalSeconds),
            _ => (CmdBan.DurationPermanent, 0u),
        };
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

    public void SetVehicleDisabled(Vehicle vehicle, bool disabled)
    {
        if (!Activate(vehicle))
            return;

        SendConsole($"object.setIsDisabledRecursive {(disabled ? 1 : 0)}");
    }

    public bool SetVehicleCollision(Vehicle vehicle, bool hasCollision)
    {
        if (vehicle.RootVehicleId <= 0 || _server.Session is not { IsOpen: true } session || (session.Capabilities & Welcome.CapCollision) == 0)
            return false;

        session.Fire(id => new CmdSetCollision(id, ObjectRef.Networkable((uint)vehicle.RootVehicleId), hasCollision ? CmdSetCollision.FlagCollision : (byte)0));
        return true;
    }

    /// <summary>Makes the vehicle the console's active object; false without a console id (logged).</summary>
    private bool Activate(Vehicle vehicle)
    {
        if (vehicle.ConsoleId < 0)
        {
            _logger.LogWarning("Admin channel {ServerId}: {Template} {Id} has no console id", _server.Id, vehicle.RootVehicleTemplate, vehicle.RootVehicleId);
            return false;
        }

        SendSetObjectActive(vehicle.ConsoleId);
        return true;
    }

    public void SetGravity(double gravity) => SendConsole($"physics.gravity {gravity.ToString(CultureInfo.InvariantCulture)}");

    public void SetTemplate(string type, string template, params (string Property, string Value)[] values)
    {
        var lines = new List<string>(values.Length + 1) { $"ObjectTemplate.activeSafe {ConsoleWord(type)} {ConsoleWord(template)}" };
        lines.AddRange(values.Select(v => $"ObjectTemplate.{ConsoleWord(v.Property)} {ConsoleValue(v.Value)}"));
        SendConsole(lines.ToArray());
    }

    public async Task<string> GetTemplateValueAsync(string type, string template, string property)
    {
        // Commands run in order, so the getter reads the template made active just before it
        SendConsole($"ObjectTemplate.activeSafe {ConsoleWord(type)} {ConsoleWord(template)}");
        var lines = await ConsoleAsync($"ObjectTemplate.{ConsoleWord(property)}");
        return string.Join(" ", lines).Trim();
    }

    public Task<IReadOnlyList<string>> ListBansAsync(bool byCdKey) =>
        ConsoleAsync(byCdKey ? "admin.listBannedKeys" : "admin.listBannedAddresses");

    public Task RemoveBanAsync(bool byCdKey, string identifier) =>
        ConsoleAsync((byCdKey ? "admin.removeKeyFromBanList " : "admin.removeAddressFromBanList ") + ConsoleWord(identifier));

    /// <summary>A template, type or property name: one word, no spaces or line breaks.</summary>
    private static string ConsoleWord(string word) =>
        string.IsNullOrWhiteSpace(word) || word.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))
            ? throw new ArgumentException($"'{word}' is not a console word", nameof(word))
            : word;

    /// <summary>A value: one line (a value such as CRD_NONE/3.5/0/0 has no spaces either, but some take several words).</summary>
    private static string ConsoleValue(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl)
            ? throw new ArgumentException($"'{value}' is not a console value", nameof(value))
            : value.Trim();

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
