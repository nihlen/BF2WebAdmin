using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace BF2WebAdmin.Data.Copy;

/// <summary>A copy that cannot start (the target is not empty, the source lacks a table) or did not verify.</summary>
public class DatabaseCopyException : Exception
{
    public DatabaseCopyException(string message) : base(message)
    {
    }
}

/// <summary>One table of a copy: rows in the source, rows left out (with the reasons in the notes), rows written and rows counted in the target.</summary>
public sealed class CopyTableResult
{
    public string Source { get; init; }
    public string Target { get; init; }
    public long SourceRows { get; set; }
    public long Skipped { get; set; }
    public long Copied { get; set; }
    public long TargetRows { get; set; }
    public long Bytes { get; set; }

    public bool Verified => TargetRows == Copied && Copied + Skipped == SourceRows;
}

public sealed class CopyResult
{
    public List<CopyTableResult> Tables { get; } = new();

    /// <summary>Data quirks found and how they were handled.</summary>
    public List<string> Notes { get; } = new();

    public bool Committed { get; set; }

    /// <summary>The next value match_change_seq gives (above every copied ChangeSeq).</summary>
    public long NextChangeSeq { get; set; }

    /// <summary>The next id player_join's identity gives (above every copied id).</summary>
    public long NextPlayerJoinId { get; set; }

    public TimeSpan Elapsed { get; set; }

    public bool Verified => Tables.All(t => t.Verified);

    public CopyTableResult this[string target] => Tables.Single(t => t.Target == target);
}

/// <summary>
/// The one-off move of WebAdmin's SQL Server database (bf2: Server, ServerModule, ServerPlayerAuth, MapMod,
/// MapModObject, Match, MatchRound, MatchRoundPlayer, JoinHistory) into an empty Postgres database (bf2webadmin),
/// keeping every id and every match's ChangeSeq, so the export cursor of a consumer stays valid (docs/database.md).
/// The source is only read. The target gets its migrations, then everything is written in one transaction with
/// binary COPY in batches, streamed row by row (the paths are most of the data); the transaction commits only when
/// every table's target count matches. Run by BF2WebAdmin.Server --db-copy-from.
/// </summary>
public sealed class SqlServerToPostgresCopier
{
    private readonly string _source;
    private readonly string _target;
    private readonly TextWriter _log;

    private Dictionary<string, HashSet<string>> _sourceColumns;
    private SqlConnection _sql;
    private NpgsqlConnection _pg;
    private CopyResult _result;

    /// <summary>Rows per COPY command. A batch also ends after <see cref="BatchBytes"/> of path data.</summary>
    public int BatchRows { get; init; } = 5000;

    public long BatchBytes { get; init; } = 64L * 1024 * 1024;

    public SqlServerToPostgresCopier(string sourceConnectionString, string targetConnectionString, TextWriter log)
    {
        _source = sourceConnectionString;
        _target = targetConnectionString;
        _log = log ?? TextWriter.Null;
    }

    // A round is copied when its match exists and it has some time to start from (see CopyRoundsAsync)
    private const string CopiedRoundJoin =
        @"JOIN [dbo].[MatchRound] r ON r.[Id] = p.[RoundId]
          JOIN [dbo].[Match] m ON m.[Id] = r.[MatchId]
          WHERE COALESCE(r.[RoundStart], r.[RoundEnd], m.[MatchStart]) IS NOT NULL";

    // The players (alias p) whose round is not copied, counted on their own to check the copy
    private const string NotCopiedRound =
        @"NOT EXISTS (SELECT 1 FROM [dbo].[MatchRound] r JOIN [dbo].[Match] m ON m.[Id] = r.[MatchId]
            WHERE r.[Id] = p.[RoundId] AND COALESCE(r.[RoundStart], r.[RoundEnd], m.[MatchStart]) IS NOT NULL)";

    public async Task<CopyResult> CopyAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _result = new CopyResult();

        var sourceBuilder = new SqlConnectionStringBuilder(_source) { CommandTimeout = 0 };
        var targetBuilder = new NpgsqlConnectionStringBuilder(_target) { CommandTimeout = 0 };
        if (targetBuilder.KeepAlive == 0)
            targetBuilder.KeepAlive = 30;

        _log.WriteLine($"Source: SQL Server {sourceBuilder.DataSource}, database {sourceBuilder.InitialCatalog}");
        _log.WriteLine($"Target: Postgres {targetBuilder.Host}:{targetBuilder.Port}, database {targetBuilder.Database}");

        await using (_sql = new SqlConnection(sourceBuilder.ConnectionString))
        {
            await _sql.OpenAsync(cancellationToken);
            await ReadSourceColumnsAsync();

            await using (var context = new PostgresBF2Context(DatabaseProviders.PostgresOptions(targetBuilder.ConnectionString)))
            {
                _log.WriteLine("Applying the Postgres migrations");
                await context.Database.MigrateAsync(cancellationToken);
            }

            await using (_pg = new NpgsqlConnection(targetBuilder.ConnectionString))
            {
                await _pg.OpenAsync(cancellationToken);
                await RefuseNonEmptyTargetAsync();

                await using var transaction = await _pg.BeginTransactionAsync(cancellationToken);

                await CopyServersAsync(cancellationToken);
                await CopyServerModulesAsync(cancellationToken);
                await CopyServerPlayerAuthAsync(cancellationToken);
                await CopyMapModsAsync(cancellationToken);
                await CopyMapModObjectsAsync(cancellationToken);
                var maxChangeSeq = await CopyMatchesAsync(cancellationToken);
                await CopyRoundsAsync(cancellationToken);
                await CopyRoundPlayersAsync(cancellationToken);
                await CopyPathsAsync(cancellationToken);
                var maxJoinId = await CopyJoinsAsync(cancellationToken);

                await ContinueSequencesAsync(maxChangeSeq, maxJoinId);
                await CountTargetAsync();

                if (_result.Verified)
                {
                    await transaction.CommitAsync(cancellationToken);
                    _result.Committed = true;
                }
                else
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
            }
        }

        _result.Elapsed = stopwatch.Elapsed;
        return _result;
    }

    /// <summary>Writes the per-table counts, the notes and the outcome.</summary>
    public static void WriteSummary(CopyResult result, TextWriter log)
    {
        log.WriteLine();
        log.WriteLine($"{"Source",-28} {"Target",-24} {"source",12} {"skipped",9} {"copied",12} {"target",12}  ");
        foreach (var t in result.Tables)
        {
            log.WriteLine($"{t.Source,-28} {t.Target,-24} {t.SourceRows,12} {t.Skipped,9} {t.Copied,12} {t.TargetRows,12}  {(t.Verified ? "ok" : "MISMATCH")}");
        }

        foreach (var note in result.Notes)
            log.WriteLine("Note: " + note);

        var bytes = result.Tables.Sum(t => t.Bytes);
        var seconds = Math.Max(result.Elapsed.TotalSeconds, 0.001);
        log.WriteLine($"{result.Tables.Sum(t => t.Copied)} rows, {bytes / 1048576.0:F1} MB of paths in {result.Elapsed.TotalSeconds:F1} s ({bytes / 1048576.0 / seconds:F1} MB/s)");

        if (result.Committed)
        {
            log.WriteLine($"Committed. match_change_seq continues at {result.NextChangeSeq}, player_join ids at {result.NextPlayerJoinId}.");
        }
        else
        {
            log.WriteLine("FAILED: the counts do not match; the transaction was rolled back and the target is empty.");
        }
    }

    // ------------------------------------------------------------------ checks

    private async Task ReadSourceColumnsAsync()
    {
        var columns = await _sql.QueryAsync<(string Table, string Column)>(
            "SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo'");
        _sourceColumns = columns
            .GroupBy(c => c.Table, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Column).ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

        var required = new[] { "Server", "ServerModule", "ServerPlayerAuth", "Match", "MatchRound", "MatchRoundPlayer" };
        var missing = required.Where(t => !_sourceColumns.ContainsKey(t)).ToList();
        if (missing.Count > 0)
            throw new DatabaseCopyException($"The source has no table {string.Join(", ", missing.Select(t => "dbo." + t))}: is it WebAdmin's database?");
    }

    private bool SourceHas(string table, string column = null) =>
        _sourceColumns.TryGetValue(table, out var columns) && (column == null || columns.Contains(column));

    private string SourceColumn(string table, string column, string fallback) =>
        SourceHas(table, column) ? $"[{column}]" : $"{fallback} AS [{column}]";

    private static readonly string[] TargetTables =
    {
        "server", "server_module", "server_player_auth", "map_mod", "map_mod_object", "match", "match_round",
        "match_round_player", "match_round_player_path", "player_join",
    };

    private async Task RefuseNonEmptyTargetAsync()
    {
        var nonEmpty = new List<string>();
        foreach (var table in TargetTables)
        {
            var count = await _pg.ExecuteScalarAsync<long>($"SELECT count(*) FROM {table}");
            if (count > 0)
                nonEmpty.Add($"{table} ({count} rows)");
        }

        if (nonEmpty.Count > 0)
        {
            throw new DatabaseCopyException(
                $"The target database is not empty: {string.Join(", ", nonEmpty)}. The copy only goes into a new, empty database " +
                "(drop and recreate it, or point ConnectionStrings:BF2DB at another one).");
        }
    }

    private async Task CountTargetAsync()
    {
        foreach (var table in _result.Tables)
            table.TargetRows = await _pg.ExecuteScalarAsync<long>($"SELECT count(*) FROM {table.Target}");
    }

    private async Task<long> SourceCountAsync(string sql) => await _sql.ExecuteScalarAsync<long>(sql);

    private void Note(string message)
    {
        _result.Notes.Add(message);
        _log.WriteLine("  note: " + message);
    }

    // ------------------------------------------------------------------ tables

    private async Task CopyServersAsync(CancellationToken cancellationToken)
    {
        var table = Start("dbo.Server", "server", await SourceCountAsync("SELECT COUNT_BIG(*) FROM [dbo].[Server]"));
        var sql = $@"SELECT [ServerId], [ServerGroup], [IpAddress], [GamePort], [QueryPort], [RconPort], [RconPassword], [DiscordBotToken],
                [DiscordAdminChannel], [DiscordNotificationChannel], [DiscordMatchResultChannel],
                {SourceColumn("Server", "AdminKey", "CAST(NULL AS nvarchar(128))")}, {SourceColumn("Server", "IsTestServer", "CAST(0 AS bit)")}
            FROM [dbo].[Server]";

        await CopyRowsAsync(table, sql,
            "COPY server (server_id, server_group, ip_address, game_port, query_port, rcon_port, rcon_password, discord_bot_token, " +
            "discord_admin_channel, discord_notification_channel, discord_match_result_channel, admin_key, is_test_server) FROM STDIN (FORMAT BINARY)",
            (r, w) =>
            {
                WriteText(w, r, 0);
                WriteText(w, r, 1);
                WriteText(w, r, 2);
                WriteInt(w, r, 3);
                WriteInt(w, r, 4);
                WriteInt(w, r, 5);
                for (var i = 6; i <= 11; i++)
                    WriteText(w, r, i);
                w.Write(Convert.ToBoolean(r.GetValue(12)), NpgsqlDbType.Boolean);
                return 0;
            }, cancellationToken);
    }

    private async Task CopyServerModulesAsync(CancellationToken cancellationToken)
    {
        var table = Start("dbo.ServerModule", "server_module", await SourceCountAsync("SELECT COUNT_BIG(*) FROM [dbo].[ServerModule]"));
        await CopyRowsAsync(table, "SELECT [ServerGroup], [Module], [IsEnabled] FROM [dbo].[ServerModule]",
            "COPY server_module (server_group, module, is_enabled) FROM STDIN (FORMAT BINARY)",
            (r, w) =>
            {
                WriteText(w, r, 0);
                WriteText(w, r, 1);
                w.Write(Convert.ToBoolean(r.GetValue(2)), NpgsqlDbType.Boolean);
                return 0;
            }, cancellationToken);
    }

    private async Task CopyServerPlayerAuthAsync(CancellationToken cancellationToken)
    {
        var table = Start("dbo.ServerPlayerAuth", "server_player_auth", await SourceCountAsync("SELECT COUNT_BIG(*) FROM [dbo].[ServerPlayerAuth]"));
        await CopyRowsAsync(table, "SELECT [ServerGroup], [PlayerHash], [AuthLevel] FROM [dbo].[ServerPlayerAuth]",
            "COPY server_player_auth (server_group, player_hash, auth_level) FROM STDIN (FORMAT BINARY)",
            (r, w) =>
            {
                WriteText(w, r, 0);
                WriteText(w, r, 1);
                WriteInt(w, r, 2);
                return 0;
            }, cancellationToken);
    }

    private async Task CopyMapModsAsync(CancellationToken cancellationToken)
    {
        if (!SourceHas("MapMod"))
        {
            Start("dbo.MapMod", "map_mod", 0);
            Note("the source has no dbo.MapMod; map_mod stays empty");
            return;
        }

        var table = Start("dbo.MapMod", "map_mod", await SourceCountAsync("SELECT COUNT_BIG(*) FROM [dbo].[MapMod]"));
        await CopyRowsAsync(table, "SELECT [Id], [Name], [CreatedBy], [CreatedDate], [EditedDate] FROM [dbo].[MapMod]",
            "COPY map_mod (id, name, created_by, created_date, edited_date) FROM STDIN (FORMAT BINARY)",
            (r, w) =>
            {
                w.Write(r.GetGuid(0), NpgsqlDbType.Uuid);
                WriteText(w, r, 1);
                WriteText(w, r, 2);
                WriteTime(w, r, 3);
                WriteTime(w, r, 4);
                return 0;
            }, cancellationToken);
    }

    private async Task CopyMapModObjectsAsync(CancellationToken cancellationToken)
    {
        if (!SourceHas("MapModObject") || !SourceHas("MapMod"))
        {
            Start("dbo.MapModObject", "map_mod_object", 0);
            Note("the source has no dbo.MapModObject; map_mod_object stays empty");
            return;
        }

        var table = Start("dbo.MapModObject", "map_mod_object", await SourceCountAsync("SELECT COUNT_BIG(*) FROM [dbo].[MapModObject]"));
        table.Skipped = await SourceCountAsync(
            "SELECT COUNT_BIG(*) FROM [dbo].[MapModObject] o WHERE NOT EXISTS (SELECT 1 FROM [dbo].[MapMod] mm WHERE mm.[Id] = o.[MapModId])");
        await CopyRowsAsync(table,
            @"SELECT o.[Id], o.[TemplateName], o.[Position], o.[Rotation], o.[MapModId]
              FROM [dbo].[MapModObject] o JOIN [dbo].[MapMod] mm ON mm.[Id] = o.[MapModId]",
            "COPY map_mod_object (id, template_name, position, rotation, map_mod_id) FROM STDIN (FORMAT BINARY)",
            (r, w) =>
            {
                w.Write(r.GetGuid(0), NpgsqlDbType.Uuid);
                WriteText(w, r, 1);
                WriteText(w, r, 2);
                WriteText(w, r, 3);
                w.Write(r.GetGuid(4), NpgsqlDbType.Uuid);
                return 0;
            }, cancellationToken);

        if (table.Skipped > 0)
            Note($"{table.Skipped} map mod objects without their map mod (dbo.MapModObject.MapModId) were left out");
    }

    /// <summary>
    /// Matches keep their ChangeSeq. A match without one (a source that never ran WebAdmin 182bf9b or later, which numbers
    /// them at startup) gets the number SqlServerSchemaUpdates would give it: in start order, above the highest
    /// existing one. The source is not changed. Returns the highest ChangeSeq copied.
    /// </summary>
    private async Task<long> CopyMatchesAsync(CancellationToken cancellationToken)
    {
        var hasChangeSeq = SourceHas("Match", "ChangeSeq");
        var table = Start("dbo.Match", "match", await SourceCountAsync("SELECT COUNT_BIG(*) FROM [dbo].[Match]"));

        var changeSeq = hasChangeSeq ? "[ChangeSeq]" : "CAST(NULL AS bigint)";
        var unnumbered = hasChangeSeq
            ? await SourceCountAsync("SELECT COUNT_BIG(*) FROM [dbo].[Match] WHERE [ChangeSeq] IS NULL")
            : table.SourceRows;
        var nullStarts = await SourceCountAsync("SELECT COUNT_BIG(*) FROM [dbo].[Match] WHERE [MatchStart] IS NULL");

        var sql = $@"DECLARE @Base bigint = (SELECT ISNULL(MAX({changeSeq}), 0) FROM [dbo].[Match]);
            SELECT [Id], [ServerId], [ServerName], [Map], [Type], [TeamAHash], [TeamAName], [TeamAScore], [TeamBHash], [TeamBName],
                [TeamBScore], [MatchStart], [MatchEnd],
                COALESCE({changeSeq}, @Base + ROW_NUMBER() OVER (PARTITION BY CASE WHEN {changeSeq} IS NULL THEN 0 ELSE 1 END ORDER BY [MatchStart], [Id])) AS [ChangeSeq]
            FROM [dbo].[Match]";

        long max = 0;
        await CopyRowsAsync(table, sql,
            "COPY match (id, server_id, server_name, map, type, team_a_hash, team_a_name, team_a_score, team_b_hash, team_b_name, " +
            "team_b_score, match_start, match_end, change_seq) FROM STDIN (FORMAT BINARY)",
            (r, w) =>
            {
                w.Write(r.GetGuid(0), NpgsqlDbType.Uuid);
                for (var i = 1; i <= 6; i++)
                    WriteText(w, r, i);
                WriteInt(w, r, 7);
                WriteText(w, r, 8);
                WriteText(w, r, 9);
                WriteInt(w, r, 10);
                WriteTime(w, r, 11);
                WriteTime(w, r, 12);
                var seq = Convert.ToInt64(r.GetValue(13));
                w.Write(seq, NpgsqlDbType.Bigint);
                max = Math.Max(max, seq);
                return 0;
            }, cancellationToken);

        if (!hasChangeSeq)
            Note($"the source has no dbo.Match.ChangeSeq (WebAdmin before 182bf9b): all {unnumbered} matches were numbered in start order, as WebAdmin's startup update would; a consumer must read from 0");
        else if (unnumbered > 0)
            Note($"{unnumbered} matches had no ChangeSeq and were numbered in start order above the highest one, as WebAdmin's startup update would (the source was not changed)");
        if (nullStarts > 0)
            Note($"{nullStarts} matches have no MatchStart (copied as null)");

        // The source's sequence may be ahead of the highest number in use (numbers taken by failed writes)
        if (hasChangeSeq)
        {
            var current = await _sql.ExecuteScalarAsync<long?>(
                "SELECT CAST([current_value] AS bigint) FROM sys.sequences WHERE object_id = OBJECT_ID(N'[dbo].[MatchChangeSeq]')");
            if (current is { } c && c > max)
                max = c;
        }

        return max;
    }

    /// <summary>
    /// Rounds of an existing match. Postgres requires round_start and round_end: a missing start becomes the round's end
    /// (or else the match start), a missing end the start; a round with neither and no match start is left out.
    /// </summary>
    private async Task CopyRoundsAsync(CancellationToken cancellationToken)
    {
        var table = Start("dbo.MatchRound", "match_round", await SourceCountAsync("SELECT COUNT_BIG(*) FROM [dbo].[MatchRound]"));
        var orphans = await SourceCountAsync(
            "SELECT COUNT_BIG(*) FROM [dbo].[MatchRound] r WHERE NOT EXISTS (SELECT 1 FROM [dbo].[Match] m WHERE m.[Id] = r.[MatchId])");
        var timeless = await SourceCountAsync(
            @"SELECT COUNT_BIG(*) FROM [dbo].[MatchRound] r JOIN [dbo].[Match] m ON m.[Id] = r.[MatchId]
              WHERE COALESCE(r.[RoundStart], r.[RoundEnd], m.[MatchStart]) IS NULL");
        table.Skipped = orphans + timeless;

        long filledStart = 0, filledEnd = 0;
        await CopyRowsAsync(table,
            @"SELECT r.[Id], r.[MatchId], r.[WinningTeamId], r.[PositionTrackerInterval], r.[RoundStart], r.[RoundEnd], m.[MatchStart]
              FROM [dbo].[MatchRound] r JOIN [dbo].[Match] m ON m.[Id] = r.[MatchId]
              WHERE COALESCE(r.[RoundStart], r.[RoundEnd], m.[MatchStart]) IS NOT NULL",
            "COPY match_round (id, match_id, winning_team_id, position_tracker_interval, round_start, round_end) FROM STDIN (FORMAT BINARY)",
            (r, w) =>
            {
                w.Write(r.GetGuid(0), NpgsqlDbType.Uuid);
                w.Write(r.GetGuid(1), NpgsqlDbType.Uuid);
                WriteInt(w, r, 2);
                w.Write(r.IsDBNull(3) ? 0m : Convert.ToDecimal(r.GetValue(3)), NpgsqlDbType.Numeric);

                var start = Time(r, 4);
                var end = Time(r, 5);
                if (start == null)
                {
                    start = end ?? Time(r, 6);
                    filledStart++;
                }

                if (end == null)
                {
                    end = start;
                    filledEnd++;
                }

                w.Write(start!.Value, NpgsqlDbType.TimestampTz);
                w.Write(end!.Value, NpgsqlDbType.TimestampTz);
                return 0;
            }, cancellationToken);

        if (orphans > 0)
            Note($"{orphans} rounds without their match (dbo.MatchRound.MatchId) were left out, with their players");
        if (timeless > 0)
            Note($"{timeless} rounds without RoundStart, RoundEnd and match start were left out, with their players");
        if (filledStart > 0)
            Note($"{filledStart} rounds had no RoundStart: set to their RoundEnd (or the match start)");
        if (filledEnd > 0)
            Note($"{filledEnd} rounds had no RoundEnd: set to their RoundStart");
    }

    /// <summary>The players of the copied rounds, without MatchId (a player reaches its match through the round) and without the paths.</summary>
    private async Task CopyRoundPlayersAsync(CancellationToken cancellationToken)
    {
        var table = Start("dbo.MatchRoundPlayer", "match_round_player", await SourceCountAsync("SELECT COUNT_BIG(*) FROM [dbo].[MatchRoundPlayer]"));
        table.Skipped = await SourceCountAsync($"SELECT COUNT_BIG(*) FROM [dbo].[MatchRoundPlayer] p WHERE {NotCopiedRound}");

        if (SourceHas("MatchRoundPlayer", "MatchId"))
        {
            var otherMatch = await SourceCountAsync(
                $"SELECT COUNT_BIG(*) FROM [dbo].[MatchRoundPlayer] p {CopiedRoundJoin} AND p.[MatchId] <> r.[MatchId]");
            if (otherMatch > 0)
                Note($"{otherMatch} round players had a MatchId other than their round's; they stay with their round (match_round_player has no match_id)");
        }

        await CopyRowsAsync(table,
            $@"SELECT p.[RoundId], p.[PlayerHash], p.[PlayerName], p.[TeamId], p.[SubVehicle], p.[SaidGo], p.[StartPosition], p.[DeathPosition],
                p.[DeathTime], p.[KillerHash], p.[KillerWeapon], p.[KillerPosition]
              FROM [dbo].[MatchRoundPlayer] p {CopiedRoundJoin}",
            "COPY match_round_player (round_id, player_hash, player_name, team_id, sub_vehicle, said_go, start_position, death_position, " +
            "death_time, killer_hash, killer_weapon, killer_position) FROM STDIN (FORMAT BINARY)",
            (r, w) =>
            {
                w.Write(r.GetGuid(0), NpgsqlDbType.Uuid);
                WriteText(w, r, 1);
                WriteText(w, r, 2);
                WriteInt(w, r, 3);
                WriteText(w, r, 4);
                w.Write(Convert.ToBoolean(r.GetValue(5)), NpgsqlDbType.Boolean);
                WriteText(w, r, 6);
                WriteText(w, r, 7);
                WriteTime(w, r, 8);
                WriteText(w, r, 9);
                WriteText(w, r, 10);
                WriteText(w, r, 11);
                return 0;
            }, cancellationToken);

        if (table.Skipped > 0)
            Note($"{table.Skipped} round players of rounds that were not copied (missing round or match) were left out");
    }

    /// <summary>
    /// The paths of the copied players: the base64 text of SQL Server is decoded into the gzip bytes it holds. A path
    /// that is not base64 is dropped (reported); a player whose paths are all dropped gets no row.
    /// </summary>
    private async Task CopyPathsAsync(CancellationToken cancellationToken)
    {
        const string hasPath = "(p.[MovementPathJson] IS NOT NULL OR p.[ProjectilePathsJson] IS NOT NULL)";
        var table = Start("dbo.MatchRoundPlayer (paths)", "match_round_player_path",
            await SourceCountAsync($"SELECT COUNT_BIG(*) FROM [dbo].[MatchRoundPlayer] p WHERE {hasPath}"));
        var orphans = await SourceCountAsync($"SELECT COUNT_BIG(*) FROM [dbo].[MatchRoundPlayer] p WHERE {hasPath} AND {NotCopiedRound}");

        long unreadable = 0, dropped = 0;
        await CopyRowsAsync(table,
            $@"SELECT p.[RoundId], p.[PlayerHash], p.[MovementPathJson], p.[ProjectilePathsJson]
              FROM [dbo].[MatchRoundPlayer] p {CopiedRoundJoin} AND {hasPath}",
            "COPY match_round_player_path (round_id, player_hash, movement_path, projectile_paths) FROM STDIN (FORMAT BINARY)",
            (r, w) =>
            {
                // SequentialAccess: the columns are read once, in order
                var roundId = r.GetGuid(0);
                var playerHash = r.GetString(1);
                var movement = Decode(r, 2, ref unreadable);
                var projectiles = Decode(r, 3, ref unreadable);
                if (movement == null && projectiles == null)
                {
                    dropped++;
                    return -1;
                }

                w.StartRow();
                w.Write(roundId, NpgsqlDbType.Uuid);
                w.Write(Clean(playerHash), NpgsqlDbType.Text);
                WriteBytes(w, movement);
                WriteBytes(w, projectiles);
                return (movement?.Length ?? 0) + (projectiles?.Length ?? 0);
            }, cancellationToken, sequential: true, startsOwnRow: true);

        table.Skipped = orphans + dropped;
        if (orphans > 0)
            Note($"{orphans} paths of round players that were not copied were left out");
        if (unreadable > 0)
            Note($"{unreadable} path values were not base64 and were dropped ({dropped} players lost all their paths)");
    }

    /// <summary>JoinHistory keeps its ids; IsLikelySpoof is BF2v2's and stays behind. Returns the highest id.</summary>
    private async Task<long> CopyJoinsAsync(CancellationToken cancellationToken)
    {
        if (!SourceHas("JoinHistory"))
        {
            Start("dbo.JoinHistory", "player_join", 0);
            Note("the source has no dbo.JoinHistory; player_join stays empty");
            return 0;
        }

        var table = Start("dbo.JoinHistory", "player_join", await SourceCountAsync("SELECT COUNT_BIG(*) FROM [dbo].[JoinHistory]"));
        table.Skipped = await SourceCountAsync("SELECT COUNT_BIG(*) FROM [dbo].[JoinHistory] WHERE [JoinDate] IS NULL");
        long emptied = 0, max = 0;
        await CopyRowsAsync(table,
            "SELECT [Id], [Name], [Guid], [IpAddress], [ServerIpAddress], [JoinDate] FROM [dbo].[JoinHistory] WHERE [JoinDate] IS NOT NULL",
            "COPY player_join (id, name, guid, ip_address, server_ip_address, join_date) FROM STDIN (FORMAT BINARY)",
            (r, w) =>
            {
                var id = Convert.ToInt64(r.GetValue(0));
                w.Write(id, NpgsqlDbType.Bigint);
                max = Math.Max(max, id);
                for (var i = 1; i <= 4; i++)
                {
                    // player_join's text columns are NOT NULL
                    if (r.IsDBNull(i))
                    {
                        emptied++;
                        w.Write(string.Empty, NpgsqlDbType.Text);
                    }
                    else
                    {
                        w.Write(Clean(r.GetString(i)), NpgsqlDbType.Text);
                    }
                }

                WriteTime(w, r, 5);
                return 0;
            }, cancellationToken);

        if (table.Skipped > 0)
            Note($"{table.Skipped} joins without a JoinDate were left out");
        if (emptied > 0)
            Note($"{emptied} null text values in joins were written as empty strings (player_join's columns are NOT NULL)");

        return max;
    }

    private async Task ContinueSequencesAsync(long maxChangeSeq, long maxJoinId)
    {
        if (maxChangeSeq > 0)
            await _pg.ExecuteAsync($"SELECT setval('{PostgresBF2Context.ChangeSeqSequence}', @Value, true)", new { Value = maxChangeSeq });
        _result.NextChangeSeq = maxChangeSeq + 1;

        if (maxJoinId > 0)
            await _pg.ExecuteAsync("SELECT setval(pg_get_serial_sequence('player_join', 'id'), @Value, true)", new { Value = maxJoinId });
        _result.NextPlayerJoinId = maxJoinId + 1;
    }

    // ------------------------------------------------------------------ streaming

    private CopyTableResult Start(string source, string target, long sourceRows)
    {
        var table = new CopyTableResult { Source = source, Target = target, SourceRows = sourceRows };
        _result.Tables.Add(table);
        _log.WriteLine($"{source} -> {target}: {sourceRows} rows");
        return table;
    }

    /// <summary>
    /// Streams <paramref name="sourceSql"/> into <paramref name="copyCommand"/>, a new COPY every <see cref="BatchRows"/>
    /// rows or <see cref="BatchBytes"/> bytes. <paramref name="writeRow"/> writes one row and returns its path bytes, or -1
    /// when it wrote none. With <paramref name="startsOwnRow"/> it calls StartRow itself (after deciding to write).
    /// </summary>
    private async Task CopyRowsAsync(CopyTableResult table, string sourceSql, string copyCommand,
        Func<SqlDataReader, NpgsqlBinaryImporter, long> writeRow, CancellationToken cancellationToken,
        bool sequential = false, bool startsOwnRow = false)
    {
        await using var command = new SqlCommand(sourceSql, _sql) { CommandTimeout = 0 };
        await using var reader = await command.ExecuteReaderAsync(sequential ? CommandBehavior.SequentialAccess : CommandBehavior.Default, cancellationToken);

        var progress = Stopwatch.StartNew();
        NpgsqlBinaryImporter importer = null;
        long batchRows = 0, batchBytes = 0;
        try
        {
            // Synchronous reads and writes per row: SqlClient's ReadAsync is much slower with large values (measured 11.6
            // against 18.6 MB/s of paths), and both sides buffer anyway
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                importer ??= await _pg.BeginBinaryImportAsync(copyCommand, cancellationToken);
                if (!startsOwnRow)
                    importer.StartRow();

                var bytes = writeRow(reader, importer);
                if (bytes < 0)
                    continue;

                table.Copied++;
                table.Bytes += bytes;
                batchRows++;
                batchBytes += bytes;

                if (batchRows >= BatchRows || batchBytes >= BatchBytes)
                {
                    await importer.CompleteAsync(cancellationToken);
                    await importer.DisposeAsync();
                    importer = null;
                    batchRows = batchBytes = 0;

                    if (progress.Elapsed > TimeSpan.FromSeconds(10))
                    {
                        _log.WriteLine($"  {table.Target}: {table.Copied} of {table.SourceRows} rows" +
                            (table.Bytes > 0 ? $", {table.Bytes / 1048576.0:F0} MB" : ""));
                        progress.Restart();
                    }
                }
            }

            if (importer != null)
                await importer.CompleteAsync(cancellationToken);
        }
        finally
        {
            if (importer != null)
                await importer.DisposeAsync();
        }
    }

    private static string Clean(string value) =>
        value != null && value.Contains('\0') ? value.Replace("\0", "") : value;

    private static void WriteText(NpgsqlBinaryImporter w, SqlDataReader r, int ordinal)
    {
        // Postgres text cannot hold NUL characters; they are removed
        if (r.IsDBNull(ordinal))
            w.WriteNull();
        else
            w.Write(Clean(r.GetString(ordinal)), NpgsqlDbType.Text);
    }

    private static void WriteInt(NpgsqlBinaryImporter w, SqlDataReader r, int ordinal)
    {
        if (r.IsDBNull(ordinal))
            w.Write(0, NpgsqlDbType.Integer);
        else
            w.Write(Convert.ToInt32(r.GetValue(ordinal)), NpgsqlDbType.Integer);
    }

    /// <summary>SQL Server's times are UTC without a kind; timestamptz needs the kind.</summary>
    private static DateTime? Time(SqlDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) ? null : DateTime.SpecifyKind(r.GetDateTime(ordinal), DateTimeKind.Utc);

    private static void WriteTime(NpgsqlBinaryImporter w, SqlDataReader r, int ordinal)
    {
        if (Time(r, ordinal) is { } value)
            w.Write(value, NpgsqlDbType.TimestampTz);
        else
            w.WriteNull();
    }

    private static void WriteBytes(NpgsqlBinaryImporter w, byte[] value)
    {
        if (value == null)
            w.WriteNull();
        else
            w.Write(value, NpgsqlDbType.Bytea);
    }

    private static byte[] Decode(SqlDataReader r, int ordinal, ref long unreadable)
    {
        if (r.IsDBNull(ordinal))
            return null;

        var text = r.GetString(ordinal);
        try
        {
            return Convert.FromBase64String(text);
        }
        catch (FormatException)
        {
            unreadable++;
            return null;
        }
    }
}
