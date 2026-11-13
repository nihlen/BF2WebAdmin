using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.AdminChannel;

namespace BF2WebAdmin.Server.Services;

public enum BanKind
{
    Permanent,
    Round,
    Timed,
}

/// <summary>How long a ban lasts: permanently, for the rest of the round, or for a time.</summary>
public readonly record struct BanLength(BanKind Kind, TimeSpan Duration = default)
{
    public static readonly BanLength Permanent = new(BanKind.Permanent);
    public static readonly BanLength Round = new(BanKind.Round);

    public static BanLength For(TimeSpan duration) => new(BanKind.Timed, duration);

    /// <summary>"Perm" (or "Permanent"), "Round", a number of seconds ("3600"), or a number with m, h, d or w ("30m", "7d").</summary>
    public static bool TryParse(string? text, out BanLength length)
    {
        length = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        if (text.Equals("perm", StringComparison.OrdinalIgnoreCase) || text.Equals("permanent", StringComparison.OrdinalIgnoreCase))
        {
            length = Permanent;
            return true;
        }

        if (text.Equals("round", StringComparison.OrdinalIgnoreCase))
        {
            length = Round;
            return true;
        }

        var match = Regex.Match(text, @"^(\d{1,9})([smhdw]?)$", RegexOptions.IgnoreCase);
        if (!match.Success)
            return false;

        var value = long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var seconds = char.ToLowerInvariant(match.Groups[2].Value.FirstOrDefault('s')) switch
        {
            'm' => value * 60,
            'h' => value * 3600,
            'd' => value * 86400,
            'w' => value * 604800,
            _ => value,
        };

        // The engine takes a 32-bit Unix expiry; ten years is plenty
        if (seconds is <= 0 or > 10L * 365 * 86400)
            return false;

        length = For(TimeSpan.FromSeconds(seconds));
        return true;
    }

    public override string ToString() => Kind switch
    {
        BanKind.Permanent => "permanently",
        BanKind.Round => "for this round",
        _ => "for " + ServerAdmin.Describe(Duration),
    };
}

/// <summary>A ban in a game server's ban list, with what WebAdmin recorded about it (null when it did not ban it itself).</summary>
public record BanDto(string Identifier, bool ByCdKey, BanKind Kind, DateTime? ExpiresAt, string? Name, string? BannedBy, string? Reason, DateTime? BannedAt);

/// <summary>
/// Kicks, warnings and bans on one game server, and the "!" admin commands of the web chat box and Discord, all over
/// the admin channel. The bans are the engine's own ban list (settings/banlist.con on the server, which enforces them
/// without WebAdmin); WebAdmin records the name, the reason and who banned in <see cref="IServerBanRepository"/>.
/// Failures throw <see cref="AdminCommandException"/> with a message for the user.
/// </summary>
public sealed class ServerAdmin
{
    /// <summary>The "!" commands, for help texts.</summary>
    public const string Help =
        """
        !kick (!k) <player> [reason]            kick a player (index or part of the name)
        !warn (!w) <player> [reason]            warn a player in the chat
        !ban (!b) <player> [length] [reason]    ban by CD key (by address without one); length: Perm (default), Round, 3600, 30m, 12h, 7d, 2w
        !unban <CD-key hash or address>         lift a ban
        !banlist                                the server's bans
        !clearbans                              lift every ban
        !map (!m) <map> <mode> [size]           run a map of the rotation (e.g. !m dalian_2_v_2 gpm_cq 16)
        !list                                   the players (admin.listPlayers)
        !<console command>                      any other console line, e.g. !sv.serverName (!exec <line> also works)
        "." works as well as "!" (.kick, .k); in game both run these commands except the console lines
        """;

    private static readonly Regex HashPattern = new("^[0-9a-f]{32}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IGameServer _server;
    private readonly IServerBanRepository _bans;

    public ServerAdmin(IGameServer server, IServerBanRepository bans)
    {
        _server = server;
        _bans = bans;
    }

    /// <summary>A connected player by index ("3") or by a part of the name, ignoring case; null when none or several match.</summary>
    public Player? FindPlayer(string text, out string? error)
    {
        error = null;
        text = text.Trim();
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
        {
            var byIndex = _server.Players.FirstOrDefault(p => p.Index == index);
            if (byIndex == null)
                error = $"No player {index}";
            return byIndex;
        }

        var matches = _server.Players.Where(p => p.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false).ToList();
        if (matches.Count == 1)
            return matches[0];

        error = matches.Count == 0
            ? $"No player matches '{text}'"
            : $"{matches.Count} players match '{text}': {string.Join(", ", matches.Select(p => $"{p.Index} {p.DisplayName}"))}";
        return null;
    }

    public Player GetPlayer(string text) =>
        FindPlayer(text, out var error) ?? throw new AdminCommandException(error!, AdminErrorCode.UnknownPlayer);

    public Player GetPlayer(int index) =>
        _server.Players.FirstOrDefault(p => p.Index == index) ?? throw new AdminCommandException($"No player {index}", AdminErrorCode.UnknownPlayer);

    public async Task<string> KickAsync(Player player, string? reason, string by)
    {
        reason = Reason(reason, $"Kicked by {by}");
        await _server.GameWriter.KickAsync(player, reason);
        _server.GameWriter.SendText($"{player.DisplayName} was kicked: {reason}");
        return $"Kicked {player.DisplayName}";
    }

    public string Warn(Player player, string? reason, string by)
    {
        reason = Reason(reason, "behave");
        _server.GameWriter.SendText($"Warning {player.DisplayName}: {reason}");
        return $"Warned {player.DisplayName}";
    }

    /// <summary>Bans a connected player by CD-key hash, or by address when it has none (LAN and test servers), and records it.</summary>
    public async Task<string> BanAsync(Player player, BanLength length, string? reason, string by)
    {
        reason = Reason(reason, $"Banned by {by}");
        var byCdKey = !string.IsNullOrEmpty(player.Hash) && !player.HasNameKey;
        var identifier = byCdKey ? player.Hash : player.IpAddress?.ToString();
        if (string.IsNullOrEmpty(identifier))
            throw new AdminCommandException($"{player.DisplayName} has neither a CD-key hash nor an address", AdminErrorCode.Refused);

        await _server.GameWriter.BanAsync(player, byCdKey, length, reason);

        // A record outlives a round or timed ban harmlessly: the list shows only what the engine still has
        await _bans.SetBanAsync(new ServerBan
        {
            ServerId = _server.Id,
            Identifier = identifier,
            ByCdKey = byCdKey,
            Name = Clip(player.DisplayName, ServerBan.NameLength),
            BannedBy = Clip(by, ServerBan.BannedByLength),
            Reason = Clip(reason, ServerBan.ReasonLength),
            BannedAt = DateTime.UtcNow,
        });

        _server.GameWriter.SendText($"{player.DisplayName} was banned {length}: {reason}");
        return $"Banned {player.DisplayName} {length}";
    }

    /// <summary>The server's ban list (from the engine) with WebAdmin's records, newest first.</summary>
    public async Task<IReadOnlyList<BanDto>> GetBansAsync()
    {
        var keys = await _server.GameWriter.ListBansAsync(byCdKey: true);
        var addresses = await _server.GameWriter.ListBansAsync(byCdKey: false);
        var records = (await _bans.GetBansAsync(_server.Id)).ToDictionary(b => b.Identifier, StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;

        return ParseBanList(keys.Concat(addresses))
            .Select(e =>
            {
                records.TryGetValue(e.Identifier, out var record);
                return new BanDto(e.Identifier, e.ByCdKey, e.Kind, e.SecondsLeft is { } left ? now.AddSeconds(left) : null,
                    record?.Name, record?.BannedBy, record?.Reason, record?.BannedAt);
            })
            .OrderByDescending(b => b.BannedAt ?? DateTime.MinValue)
            .ToList();
    }

    /// <summary>Lifts the ban of a CD-key hash or an address and forgets its record.</summary>
    public async Task<string> UnbanAsync(string identifier)
    {
        identifier = identifier.Trim();
        if (identifier.Length == 0 || identifier.Any(char.IsWhiteSpace) || identifier.Contains('"'))
            throw new AdminCommandException($"Not a CD-key hash or an address: '{identifier}'", AdminErrorCode.InvalidValue);

        var byCdKey = HashPattern.IsMatch(identifier);
        var listed = ParseBanList(await _server.GameWriter.ListBansAsync(byCdKey))
            .Any(e => e.Identifier.Equals(identifier, StringComparison.OrdinalIgnoreCase));

        await _server.GameWriter.RemoveBanAsync(byCdKey, identifier);
        await _bans.RemoveBanAsync(_server.Id, identifier);
        return listed ? $"Unbanned {identifier}" : $"{identifier} was not banned";
    }

    public async Task<string> ClearBansAsync()
    {
        await _server.GameWriter.ConsoleAsync("admin.clearBanList");
        await _bans.RemoveBansAsync(_server.Id);
        return "The ban list is empty";
    }

    private static readonly HashSet<string> Verbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "k", "kick", "w", "warn", "b", "ban", "unban", "banlist", "clearbans", "m", "map", "list", "exec", "help",
    };

    /// <summary>Whether a "!" or "." line is one of <see cref="RunAsync"/>'s own commands (not a console line or a module's chat command).</summary>
    public static bool IsAdminCommand(string text) => Verbs.Contains(Split(text.Trim().TrimStart('!', '.')).Head);

    /// <summary>Runs a "!" command (see <see cref="Help"/>; a leading "." works too) and returns its answer.</summary>
    public async Task<string> RunAsync(string text, string by)
    {
        var line = text.Trim().TrimStart('!', '.').Trim();
        var (verb, rest) = Split(line);

        switch (verb.ToLowerInvariant())
        {
            case "k":
            case "kick":
            {
                var (target, reason) = Split(rest);
                return await KickAsync(GetPlayer(Required(target, "!kick <player> [reason]")), Unquote(reason), by);
            }

            case "w":
            case "warn":
            {
                var (target, reason) = Split(rest);
                return Warn(GetPlayer(Required(target, "!warn <player> [reason]")), Unquote(reason), by);
            }

            case "b":
            case "ban":
            {
                var (target, after) = Split(rest);
                var player = GetPlayer(Required(target, "!ban <player> [length] [reason]"));
                var (first, remainder) = Split(after);
                var length = BanLength.Permanent;
                var reason = after;
                if (BanLength.TryParse(first, out var parsed))
                {
                    length = parsed;
                    reason = remainder;
                }

                return await BanAsync(player, length, Unquote(reason), by);
            }

            case "unban":
                return await UnbanAsync(Required(Unquote(rest), "!unban <CD-key hash or address>"));

            case "banlist":
                return FormatBans(await GetBansAsync());

            case "clearbans":
                return await ClearBansAsync();

            case "m":
            case "map":
            {
                var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || (parts.Length > 2 && !int.TryParse(parts[2], out _)))
                    throw new AdminCommandException("Usage: !map <map> <mode> [size]", AdminErrorCode.InvalidValue);
                var size = parts.Length > 2 ? int.Parse(parts[2], CultureInfo.InvariantCulture) : 0;
                await _server.GameWriter.RunMapAsync(parts[0], parts[1], size);
                return $"Loading {parts[0]}";
            }

            case "list":
                return Output(await _server.GameWriter.ConsoleAsync("admin.listPlayers"));

            case "exec":
                return Output(await _server.GameWriter.ConsoleAsync(Required(rest, "!exec <console line>")));

            case "":
            case "help":
                return Help;

            default:
                return Output(await _server.GameWriter.ConsoleAsync(line));
        }
    }

    /// <summary>The ban list as text, for Discord and the chat box.</summary>
    public static string FormatBans(IReadOnlyList<BanDto> bans)
    {
        if (bans.Count == 0)
            return "No bans";

        var text = new StringBuilder();
        foreach (var ban in bans)
        {
            var length = ban.Kind switch
            {
                BanKind.Permanent => "Perm",
                BanKind.Round => "Round",
                _ => ban.ExpiresAt is { } expires ? $"until {expires:yyyy-MM-dd HH:mm} UTC" : "Timed",
            };
            text.Append($"{ban.Name ?? "?"} | {ban.Identifier} | {length}");
            if (ban.BannedBy != null)
                text.Append($" | by {ban.BannedBy}");
            if (ban.BannedAt is { } at)
                text.Append($" {at:yyyy-MM-dd}");
            if (!string.IsNullOrEmpty(ban.Reason))
                text.Append($" | {ban.Reason}");
            text.Append('\n');
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>A user's message for a failed command.</summary>
    public static string Describe(AdminCommandException e) => e.Code switch
    {
        AdminErrorCode.Unavailable => "The server refused: the map is not in its rotation",
        AdminErrorCode.Disabled => "The server refused: disabled in its serverext.cfg",
        AdminErrorCode.Refused => $"The server refused: {e.Message}",
        _ => e.Message,
    };

    public static string Describe(TimeSpan duration) => duration switch
    {
        { TotalDays: >= 7 } when duration.TotalDays % 7 == 0 => Plural((int)(duration.TotalDays / 7), "week"),
        { TotalDays: >= 1 } when duration.TotalHours % 24 == 0 => Plural((int)duration.TotalDays, "day"),
        { TotalHours: >= 1 } when duration.TotalMinutes % 60 == 0 => Plural((int)duration.TotalHours, "hour"),
        { TotalMinutes: >= 1 } when duration.TotalSeconds % 60 == 0 => Plural((int)duration.TotalMinutes, "minute"),
        _ => Plural((int)duration.TotalSeconds, "second"),
    };

    /// <summary>
    /// The engine's admin.listBannedKeys and admin.listBannedAddresses lines: "Key: &lt;hash&gt; Perm", "IP: &lt;address&gt; Round",
    /// "Key: &lt;hash&gt; Time Left: &lt;seconds&gt;".
    /// </summary>
    public static IEnumerable<(string Identifier, bool ByCdKey, BanKind Kind, int? SecondsLeft)> ParseBanList(IEnumerable<string> lines)
    {
        foreach (var raw in lines.SelectMany(l => l.Split('\n')))
        {
            var match = Regex.Match(raw.Trim(), @"^(Key|IP): (\S+) (?:(Perm)|(Round)|Time Left: (-?\d+))$");
            if (!match.Success)
                continue;

            var kind = match.Groups[3].Success ? BanKind.Permanent : match.Groups[4].Success ? BanKind.Round : BanKind.Timed;
            int? left = kind == BanKind.Timed ? int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture) : null;
            yield return (match.Groups[2].Value, match.Groups[1].Value == "Key", kind, left);
        }
    }

    private static string Plural(int value, string unit) => value == 1 ? $"1 {unit}" : $"{value} {unit}s";

    private static (string Head, string Tail) Split(string text)
    {
        text = text.Trim();
        var space = text.IndexOf(' ');
        return space < 0 ? (text, "") : (text[..space], text[(space + 1)..].Trim());
    }

    private static string Unquote(string text)
    {
        text = text.Trim();
        return text.Length >= 2 && text[0] == '"' && text[^1] == '"' ? text[1..^1].Trim() : text;
    }

    private static string Required(string value, string usage) =>
        string.IsNullOrWhiteSpace(value) ? throw new AdminCommandException("Usage: " + usage, AdminErrorCode.InvalidValue) : value;

    private static string Reason(string? reason, string fallback)
    {
        // The game's chat and the kick message take no line breaks or double quotes
        reason = Regex.Replace(reason ?? "", @"[\r\n\t""]", " ").Trim();
        return Clip(reason.Length > 0 ? reason : fallback, ServerBan.ReasonLength);
    }

    private static string Clip(string? text, int length) => text == null || text.Length <= length ? text! : text[..length];

    private static string Output(IReadOnlyList<string> lines) => lines.Count > 0 ? string.Join('\n', lines) : "Done";
}
