using System.Text.Json;
using BF2WebAdmin.Common;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;

namespace BF2WebAdmin.Server.Api;

/// <summary>A page of an export feed. <c>Next</c> is the cursor for the next request (<c>since=Next</c>); it equals the request's cursor when the page is empty.</summary>
public record ExportPage<T>(IReadOnlyList<T> Items, long Next, bool HasMore);

/// <summary>A whole match, sent again whenever it or one of its rounds is written. Times are UTC.</summary>
public record ExportMatch(
    Guid Id,
    long ChangeSeq,
    string ServerId,
    string ServerName,
    string Map,
    string Type,
    string TeamAHash,
    string TeamAName,
    int TeamAScore,
    string TeamBHash,
    string TeamBName,
    int TeamBScore,
    DateTime? MatchStart,
    DateTime? MatchEnd,
    IReadOnlyList<ExportRound> Rounds);

/// <summary>A round, in start order within its match. <c>PositionTrackerInterval</c> is in seconds.</summary>
public record ExportRound(
    Guid Id,
    int WinningTeamId,
    double PositionTrackerInterval,
    DateTime? RoundStart,
    DateTime? RoundEnd,
    IReadOnlyList<ExportRoundPlayer> Players);

/// <summary>
/// A player's round. Positions are "x/height/z" strings as in the game. <c>MovementPath</c> is a JSON array of samples and
/// <c>ProjectilePaths</c> an array of such arrays, one per projectile; a sample is the string
/// "time|x/height/z|yaw/pitch/roll|ping" (see docs/export-api.md).
/// </summary>
public record ExportRoundPlayer(
    string PlayerHash,
    string PlayerName,
    int TeamId,
    string? SubVehicle,
    bool SaidGo,
    string? StartPosition,
    string? DeathPosition,
    DateTime? DeathTime,
    string? KillerHash,
    string? KillerWeapon,
    string? KillerPosition,
    JsonElement? MovementPath,
    JsonElement? ProjectilePaths);

/// <summary>A configured server, without its secrets.</summary>
public record ExportServer(string ServerId, string ServerGroup, string? IpAddress, int GamePort, int QueryPort, bool IsTestServer);

/// <summary>A player join (a human player connecting). <c>Guid</c> is the CD-key hash; rows from before OpenBF2 hold PunkBuster GUIDs.</summary>
public record ExportJoin(long Id, string Name, string Guid, string IpAddress, string ServerIpAddress, DateTime JoinDate);

/// <summary>
/// The export API (/api/export/v1): a read-only feed of the recorded matches, the player joins and the server list, for
/// a statistics site or another consumer with its own database. Admin authentication (the X-API-KEY header). The
/// contract is docs/export-api.md.
/// </summary>
public static class ExportEndpoints
{
    public const int DefaultMatchLimit = 50, MaxMatchLimit = 200;
    public const int DefaultJoinLimit = 1000, MaxJoinLimit = 5000;

    public static void MapExport(this RouteGroupBuilder api)
    {
        var export = api.MapGroup("/export/v1").WithTags("Export").RequireAuthorization(ApiEndpoints.AdminPolicy);
        export.MapGet("/matches", GetMatchesAsync);
        export.MapGet("/servers", GetServersAsync);
        export.MapGet("/joins", GetJoinsAsync);
    }

    /// <summary>Matches written after cursor <paramref name="since"/> (0: all), in write order.</summary>
    private static async Task<ExportPage<ExportMatch>> GetMatchesAsync(IMatchRepository matches, ILoggerFactory loggerFactory, long since = 0, int limit = DefaultMatchLimit)
    {
        limit = Math.Clamp(limit, 1, MaxMatchLimit);
        var logger = loggerFactory.CreateLogger(typeof(ExportEndpoints));
        var page = await matches.GetMatchesChangedSinceAsync(since, limit + 1);
        var items = page.Take(limit).Select(m => ToExport(m, logger)).ToList();
        return new ExportPage<ExportMatch>(items, items.Count > 0 ? items[^1].ChangeSeq : since, page.Count > limit);
    }

    private static async Task<IReadOnlyList<ExportServer>> GetServersAsync(IServerSettingsRepository servers) =>
        (await servers.GetServersAsync())
        .Select(s => new ExportServer(s.ServerId, s.ServerGroup, s.IpAddress, s.GamePort, s.QueryPort, s.IsTestServer))
        .OrderBy(s => s.ServerId, StringComparer.Ordinal)
        .ToList();

    /// <summary>Joins with an id above <paramref name="since"/> (0: all), in id order.</summary>
    private static async Task<ExportPage<ExportJoin>> GetJoinsAsync(IJoinHistoryRepository joins, long since = 0, int limit = DefaultJoinLimit)
    {
        limit = Math.Clamp(limit, 1, MaxJoinLimit);
        var page = await joins.GetJoinsSinceAsync(since, limit + 1);
        var items = page.Take(limit)
            .Select(j => new ExportJoin(j.Id, j.Name, j.Guid, j.IpAddress, j.ServerIpAddress, Utc(j.JoinDate)))
            .ToList();
        return new ExportPage<ExportJoin>(items, items.Count > 0 ? items[^1].Id : since, page.Count > limit);
    }

    public static ExportMatch ToExport(Match m, ILogger logger) => new(
        m.Id,
        m.ChangeSeq ?? 0,
        m.ServerId,
        m.ServerName,
        m.Map,
        m.Type,
        m.TeamAHash,
        m.TeamAName,
        m.TeamAScore,
        m.TeamBHash,
        m.TeamBName,
        m.TeamBScore,
        Utc(m.MatchStart),
        Utc(m.MatchEnd),
        m.MatchRounds
            .OrderBy(r => r.RoundStart)
            .Select(r => new ExportRound(
                r.Id,
                r.WinningTeamId,
                r.PositionTrackerInterval,
                Utc(r.RoundStart),
                Utc(r.RoundEnd),
                r.MatchRoundPlayers
                    .OrderBy(p => p.TeamId).ThenBy(p => p.PlayerHash, StringComparer.Ordinal)
                    .Select(p => new ExportRoundPlayer(
                        p.PlayerHash,
                        p.PlayerName,
                        p.TeamId,
                        p.SubVehicle,
                        p.SaidGo,
                        p.StartPosition?.ToString(),
                        p.DeathPosition?.ToString(),
                        Utc(p.DeathTime),
                        p.KillerHash,
                        p.KillerWeapon,
                        p.KillerPosition?.ToString(),
                        DecodePath(p.MovementPathJson, m.Id, logger),
                        DecodePath(p.ProjectilePathsJson, m.Id, logger)))
                    .ToList()))
            .ToList());

    /// <summary>The stored path (base64 of gzipped JSON) as JSON; null when missing or unreadable.</summary>
    public static JsonElement? DecodePath(string? stored, Guid matchId, ILogger logger)
    {
        if (string.IsNullOrEmpty(stored))
            return null;

        try
        {
            using var document = JsonDocument.Parse(Compression.DecompressText(stored));
            return document.RootElement.Clone();
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or JsonException)
        {
            logger.LogWarning("Export: unreadable path in match {MatchId}: {Error}", matchId, ex.Message);
            return null;
        }
    }

    /// <summary>The databases keep UTC without a kind; marks it, so the JSON carries the Z.</summary>
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? Utc(DateTime? value) => value is { } v ? Utc(v) : null;
}
