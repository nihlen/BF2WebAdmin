using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Copy;
using BF2WebAdmin.Data.Entities;
using BF2WebAdmin.Data.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using Match = BF2WebAdmin.Data.Entities.Match;
using ServerEntity = BF2WebAdmin.Data.Entities.Server;

namespace BF2WebAdmin.Tests;

/// <summary>
/// WebAdmin on Postgres (database bf2webadmin): the migrations, the EF repositories and the copier from SQL Server.
/// Each test gets a scratch database, dropped afterwards. Runs only when BF2WA_TEST_POSTGRES holds a connection string of
/// a server where it may create databases; the copier test also needs BF2WA_TEST_SQLSERVER.
/// </summary>
public class PostgresTests
{
    private static readonly DateTime Start = ExportTests.Start;

    // ---------------------------------------------------------------- schema

    [PostgresFact]
    public async Task MigrationsCreateTheSpecifiedSchema()
    {
        await using var db = await ScratchPostgres.CreateAsync();
        await using (var context = db.Context())
        {
            await context.Database.MigrateAsync();
            Assert.False(context.Database.HasPendingModelChanges());
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        }

        await using var connection = await db.OpenAsync();
        var tables = (await connection.QueryAsync<string>(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_name NOT LIKE '\\_\\_%' ORDER BY table_name")).ToList();
        Assert.Equal(new[]
        {
            "map_mod", "map_mod_object", "match", "match_round", "match_round_player", "match_round_player_path", "player_join",
            "server", "server_module", "server_player_auth",
        }, tables);

        var columns = (await connection.QueryAsync<(string Table, string Column, string Type, string Nullable)>(
                "SELECT table_name, column_name, data_type, is_nullable FROM information_schema.columns WHERE table_schema = 'public'"))
            .ToDictionary(c => $"{c.Table}.{c.Column}", c => (c.Type, c.Nullable));

        Assert.DoesNotContain("match_round_player.match_id", columns.Keys);
        Assert.DoesNotContain("match_round_player.movement_path_json", columns.Keys);
        Assert.DoesNotContain("player_join.is_likely_spoof", columns.Keys);
        Assert.Equal(("bytea", "YES"), columns["match_round_player_path.movement_path"]);
        Assert.Equal(("bytea", "YES"), columns["match_round_player_path.projectile_paths"]);
        Assert.Equal(("bigint", "NO"), columns["player_join.id"]);
        foreach (var column in new[] { "name", "guid", "ip_address", "server_ip_address" })
            Assert.Equal(("text", "NO"), columns["player_join." + column]);
        Assert.Equal(("timestamp with time zone", "NO"), columns["player_join.join_date"]);
        Assert.Equal(("bigint", "NO"), columns["match.change_seq"]);
        Assert.Equal(("timestamp with time zone", "YES"), columns["match.match_start"]);
        Assert.Equal(("uuid", "NO"), columns["match_round.match_id"]);
        Assert.Equal(("timestamp with time zone", "NO"), columns["match_round.round_start"]);
        Assert.Equal(("timestamp with time zone", "NO"), columns["match_round.round_end"]);
        Assert.Equal(("numeric", "NO"), columns["match_round.position_tracker_interval"]);
        Assert.Equal("YES", columns["server.discord_bot_token"].Nullable);

        var indexes = (await connection.QueryAsync<string>("SELECT indexdef FROM pg_indexes WHERE schemaname = 'public'")).ToList();
        Assert.Contains(indexes, i => i.Contains("ix_match_match_start") && i.Contains("(match_start DESC)"));
        Assert.Contains(indexes, i => i.Contains("UNIQUE INDEX ix_match_change_seq") && i.Contains("(change_seq)"));
        Assert.Contains(indexes, i => i.Contains("ix_match_round_match_id_round_start") && i.Contains("(match_id, round_start)"));
        Assert.Contains(indexes, i => i.Contains("ix_match_round_player_player_hash") && i.Contains("(player_hash)"));

        Assert.Equal(1, await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM pg_sequences WHERE sequencename = 'match_change_seq'"));
        Assert.Equal("d", await connection.ExecuteScalarAsync<string>(
            "SELECT attidentity::text FROM pg_attribute WHERE attrelid = 'player_join'::regclass AND attname = 'id'"));
    }

    // ---------------------------------------------------------------- repositories

    /// <summary>The same cursor behaviour as on SQLite and SQL Server; the paths go to match_round_player_path as the gzip bytes.</summary>
    [PostgresFact]
    public async Task WritesGiveIncreasingChangeSeqs()
    {
        await using var db = await ScratchPostgres.CreateAsync(migrate: true);
        var repository = new MatchRepository(db.Factory);
        await ExportTests.AssertCursorBehaviourAsync(() => repository);

        await using var connection = await db.OpenAsync();
        Assert.Equal(5, await connection.ExecuteScalarAsync<long>("SELECT last_value FROM match_change_seq"));

        var round = ExportTests.NewRound(Guid.Empty, Start, winningTeam: 1);
        var stored = await connection.QuerySingleAsync<(byte[] Movement, byte[] Projectiles)>(
            "SELECT movement_path, projectile_paths FROM match_round_player_path LIMIT 1");
        Assert.Equal(Convert.FromBase64String(round.MatchRoundPlayers[0].MovementPathJson), stored.Movement);
        Assert.Equal(Convert.FromBase64String(round.MatchRoundPlayers[0].ProjectilePathsJson), stored.Projectiles);
        Assert.Equal(8, await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM match_round_player_path"));

        // A whole match has the paths, the players' MatchId (no column) and UTC times; the unfinished list has no paths
        var all = await repository.GetMatchesChangedSinceAsync(0, 10);
        var finished = all.Single(m => m.MatchEnd != null);
        var whole = await repository.GetMatchAsync(finished.Id);
        Assert.All(whole.MatchRounds.SelectMany(r => r.MatchRoundPlayers), p =>
        {
            Assert.Equal(finished.Id, p.MatchId);
            Assert.NotNull(p.MovementPathJson);
        });
        Assert.Equal(DateTimeKind.Utc, whole.MatchStart!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, whole.MatchRounds[0].RoundStart!.Value.Kind);

        var withRounds = all.Single(m => m.MatchEnd == null);
        await repository.InsertRoundAsync(ExportTests.NewRound(withRounds.Id, Start.AddMinutes(40), winningTeam: 2));
        var unfinished = Assert.Single(await repository.GetUnfinishedMatchesAsync());
        var players = unfinished.MatchRounds.Single().MatchRoundPlayers;
        Assert.Equal(4, players.Count);
        Assert.All(players, p =>
        {
            Assert.Null(p.MovementPathJson);
            Assert.Equal(withRounds.Id, p.MatchId);
        });

        Assert.Equal(withRounds.Id, (await repository.GetMatchesByNewestAsync(0, 1)).Single().Id);
    }

    [PostgresFact]
    public async Task JoinsAreInsertedAndReadById()
    {
        await using var db = await ScratchPostgres.CreateAsync(migrate: true);
        var joins = new JoinHistoryRepository(db.Factory);

        await joins.InsertAsync(JoinHistoryTests.Join(JoinHistoryTests.Info())!);
        await joins.InsertAsync(new PlayerJoin { Name = "p1", Guid = ExportTests.HashA1, IpAddress = "203.0.113.8", ServerIpAddress = "198.51.100.20", JoinDate = Start });
        await joins.InsertAsync(new PlayerJoin { Name = "p2", Guid = ExportTests.HashA1, IpAddress = "203.0.113.9", ServerIpAddress = "198.51.100.20", JoinDate = DateTime.SpecifyKind(Start, DateTimeKind.Local) });

        var all = await joins.GetJoinsSinceAsync(0, 10);
        Assert.Equal(new long[] { 1, 2, 3 }, all.Select(j => j.Id));
        Assert.Equal(JoinHistoryTests.Hash, all[0].Guid);
        Assert.Equal(new DateTime(2026, 11, 8, 13, 30, 15, DateTimeKind.Utc), all[0].JoinDate);
        Assert.Equal(DateTimeKind.Utc, all[0].JoinDate.Kind);
        Assert.Equal(Start, all[1].JoinDate);
        Assert.Equal(DateTime.SpecifyKind(Start, DateTimeKind.Local).ToUniversalTime(), all[2].JoinDate);

        Assert.Equal(new[] { "p1", "p2" }, (await joins.GetJoinsSinceAsync(1, 10)).Select(j => j.Name));
        Assert.Single(await joins.GetJoinsSinceAsync(0, 1));

        // The text columns are NOT NULL, as in production's JoinHistory
        await Assert.ThrowsAsync<DbUpdateException>(() => joins.InsertAsync(new PlayerJoin { Name = "p3", JoinDate = Start }));
    }

    [PostgresFact]
    public async Task ServerSettingsAndMapsRoundTrip()
    {
        await using var db = await ScratchPostgres.CreateAsync(migrate: true);
        var settings = new ServerSettingsRepository(db.Factory);

        await settings.SetServerAsync(new ServerEntity { ServerId = "198.51.100.20:16567", ServerGroup = "2v2", IpAddress = "198.51.100.20", GamePort = 16567, QueryPort = 29900, RconPort = 4711, AdminKey = "ab12" });
        await settings.SetServerAsync(new ServerEntity { ServerId = "198.51.100.21:16567", ServerGroup = "test", GamePort = 16567, QueryPort = 29900, IsTestServer = true });
        await settings.SetServerAsync(new ServerEntity { ServerId = "198.51.100.20:16567", ServerGroup = "2v2", GamePort = 16567, QueryPort = 29901, RconPassword = "secret", AdminKey = "cd34" });

        var servers = (await settings.GetServersAsync()).OrderBy(s => s.ServerId).ToList();
        Assert.Equal(2, servers.Count);
        Assert.Equal(29901, servers[0].QueryPort);
        Assert.Equal("cd34", servers[0].AdminKey);
        Assert.Equal("198.51.100.20", servers[0].IpAddress);
        Assert.True(servers[1].IsTestServer);
        Assert.Equal(new[] { "2v2", "test" }, (await settings.GetServerGroupsAsync()).OrderBy(g => g));
        Assert.Equal("secret", (await settings.GetServerAsync("198.51.100.20:16567")).RconPassword);

        await settings.SetModulesAsync("2v2", new[] { "BF2Module", "Heli2v2Module" });
        await settings.SetModulesAsync("2v2", new[] { "BF2Module", "WebModule" });
        Assert.Equal(new[] { "BF2Module", "WebModule" }, (await settings.GetModulesAsync("2v2")).OrderBy(m => m));

        await settings.SetPlayerAuthAsync("2v2", ExportTests.HashA1, 50);
        await settings.SetPlayerAuthAsync("2v2", ExportTests.HashA1, 100);
        Assert.Equal(100, Assert.Single(await settings.GetPlayerAuthAsync("2v2")).AuthLevel);

        await settings.RemoveServerAsync("198.51.100.21:16567");
        Assert.Single(await settings.GetServersAsync());

        var maps = new MapRepository(db.Factory);
        var map = new MapMod { Id = Guid.NewGuid(), Name = "ramps", CreatedBy = "test", CreatedDate = Start, EditedDate = Start };
        map.MapModObjects.Add(new MapModObject { Id = Guid.NewGuid(), MapModId = map.Id, TemplateName = "ramp", Position = new Position(1, 2, 3), Rotation = new Rotation(90, 0, 0) });
        await maps.CreateAsync(map);
        var read = await maps.GetAsync(map.Id);
        Assert.Equal("ramps", read.Name);
        Assert.Equal(new Position(1, 2, 3).ToString(), Assert.Single(read.MapModObjects).Position.ToString());
        Assert.Single(await maps.GetAsync());
    }

    // ---------------------------------------------------------------- the copier

    /// <summary>
    /// A SQL Server database from Schema.sql, filled through the Dapper repositories plus the quirks of an old database
    /// (a match without ChangeSeq, an orphan round, a round without an end, a path that is not base64, join ids with a
    /// gap), copied into an empty Postgres database: ids, ChangeSeqs and path bytes survive, the sequences continue
    /// above, and a second copy is refused.
    /// </summary>
    [SqlServerAndPostgresFact]
    public async Task CopierMovesASqlServerDatabase()
    {
        await using var source = await ScratchSqlServer.CreateAsync();
        await using var target = await ScratchPostgres.CreateAsync();

        // Settings and a map mod
        var settings = new SqlServerSettingsRepository(source.ConnectionString);
        await settings.SetServerAsync(new ServerEntity { ServerId = "198.51.100.20:16567", ServerGroup = "2v2", IpAddress = "198.51.100.20", GamePort = 16567, QueryPort = 29900, RconPort = 4711, RconPassword = "secret", AdminKey = "ab12", DiscordBotToken = "token" });
        await settings.SetServerAsync(new ServerEntity { ServerId = "198.51.100.21:16567", ServerGroup = "test", GamePort = 16567, QueryPort = 29900, IsTestServer = true });
        await settings.SetModulesAsync("2v2", new[] { "BF2Module", "Heli2v2Module" });
        await settings.SetPlayerAuthAsync("2v2", ExportTests.HashA1, 100);
        var map = new MapMod { Id = Guid.NewGuid(), Name = "ramps", CreatedBy = "test", CreatedDate = Start, EditedDate = Start };
        map.MapModObjects.Add(new MapModObject { Id = Guid.NewGuid(), MapModId = map.Id, TemplateName = "ramp", Position = new Position(1, 2, 3), Rotation = new Rotation(90, 0, 0) });
        await new SqlMapRepository(source.ConnectionString).CreateAsync(map);

        // An old match without a ChangeSeq, then matches written by WebAdmin
        var oldMatch = Guid.NewGuid();
        await source.ExecuteAsync(
            @"INSERT INTO [dbo].[Match] ([Id], [ServerId], [ServerName], [Map], [Type], [TeamAHash], [TeamAName], [TeamAScore],
                [TeamBHash], [TeamBName], [TeamBScore], [MatchStart], [MatchEnd])
              VALUES (@Id, '198.51.100.20:16567', 'old', 'dalian_plant', 'HELI_2V2', 'a', 'A', 0, 'b', 'B', 0, @Start, @Start)",
            new { Id = oldMatch, Start = Start.AddDays(-30) });

        var matches = new SqlMatchRepository(source.ConnectionString);
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var match = ExportTests.NewMatch(Start.AddHours(i));
            await matches.InsertMatchAsync(match);
            await matches.InsertRoundAsync(ExportTests.NewRound(match.Id, Start.AddHours(i).AddMinutes(1), winningTeam: 1));
            await matches.InsertRoundAsync(ExportTests.NewRound(match.Id, Start.AddHours(i).AddMinutes(4), winningTeam: 2));
            if (i < 2)
            {
                match.TeamAScore = 1;
                match.TeamBScore = 1;
                match.MatchEnd = Start.AddHours(i).AddMinutes(6);
                await matches.UpdateMatchAsync(match);
            }

            ids.Add(match.Id);
        }

        // A bigger path, a path that is not base64, a round without an end and an orphan round (constraints off, as in old databases)
        var bigPath = Compression.CompressText("[" + string.Join(",", Enumerable.Range(0, 20000).Select(t => $"\"{t * 50}|{t}.000/2.000/3.000|0.000/0.000/0.000|40\"")) + "]");
        await source.ExecuteAsync("UPDATE TOP (1) [dbo].[MatchRoundPlayer] SET [MovementPathJson] = @Path WHERE [PlayerHash] = @Hash", new { Path = bigPath, Hash = ExportTests.HashB2 });
        await source.ExecuteAsync("UPDATE TOP (1) [dbo].[MatchRoundPlayer] SET [MovementPathJson] = 'not base64', [ProjectilePathsJson] = NULL WHERE [PlayerHash] = @Hash", new { Hash = ExportTests.HashA2 });
        await source.ExecuteAsync("ALTER TABLE [dbo].[MatchRound] ALTER COLUMN [RoundEnd] [datetime2](3) NULL");
        await source.ExecuteAsync("UPDATE TOP (1) [dbo].[MatchRound] SET [RoundEnd] = NULL WHERE [MatchId] = @Id", new { Id = ids[2] });
        await source.ExecuteAsync("ALTER TABLE [dbo].[MatchRound] NOCHECK CONSTRAINT [FK_MatchRound_Match]; ALTER TABLE [dbo].[MatchRoundPlayer] NOCHECK CONSTRAINT [FK_MatchRoundPlayer_Match]");
        var orphan = ExportTests.NewRound(Guid.NewGuid(), Start, winningTeam: 1);
        await matches.InsertRoundAsync(orphan);

        // Joins: ids 1-3 from WebAdmin, then 1000 (a gap) with BF2v2's spoof flag and a NUL in the name
        var joins = new SqlJoinHistoryRepository(source.ConnectionString);
        for (var i = 0; i < 3; i++)
            await joins.InsertAsync(new PlayerJoin { Name = $"p{i}", Guid = ExportTests.HashA1, IpAddress = "203.0.113.7", ServerIpAddress = "198.51.100.20", JoinDate = Start.AddMinutes(i) });
        await source.ExecuteAsync(
            @"SET IDENTITY_INSERT [dbo].[JoinHistory] ON;
              INSERT INTO [dbo].[JoinHistory] ([Id], [Name], [Guid], [IpAddress], [ServerIpAddress], [JoinDate], [IsLikelySpoof])
              VALUES (1000, @Name, 'pbguid', '203.0.113.9', '198.51.100.20', @Date, 1);
              SET IDENTITY_INSERT [dbo].[JoinHistory] OFF;", new { Name = "nul\0name", Date = Start });

        // What the copy must keep
        var sourceSeqs = (await source.QueryAsync<(Guid Id, long? Seq)>("SELECT [Id], [ChangeSeq] FROM [dbo].[Match]")).ToDictionary(m => m.Id, m => m.Seq);
        Assert.Null(sourceSeqs[oldMatch]);
        var maxSeq = sourceSeqs.Values.Max()!.Value;
        var sourcePaths = (await source.QueryAsync<(Guid RoundId, string Hash, string Movement, string Projectiles)>(
                "SELECT [RoundId], [PlayerHash], [MovementPathJson], [ProjectilePathsJson] FROM [dbo].[MatchRoundPlayer] WHERE [RoundId] <> @Orphan", new { Orphan = orphan.Id }))
            .ToDictionary(p => (p.RoundId, p.Hash));
        var sourceExport = await matches.GetMatchesChangedSinceAsync(0, 100);

        // Copy, in small batches so that a table needs several COPY commands
        var log = new StringWriter();
        var result = await new SqlServerToPostgresCopier(source.ConnectionString, target.ConnectionString, log) { BatchRows = 7, BatchBytes = 100_000 }.CopyAsync();
        SqlServerToPostgresCopier.WriteSummary(result, log);
        Assert.True(result.Committed, log.ToString());
        Assert.True(result.Verified, log.ToString());

        void Counts(string table, long source, long skipped, long copied)
        {
            var t = result[table];
            Assert.Equal((source, skipped, copied, copied), (t.SourceRows, t.Skipped, t.Copied, t.TargetRows));
        }

        Counts("server", 2, 0, 2);
        Counts("server_module", 2, 0, 2);
        Counts("server_player_auth", 1, 0, 1);
        Counts("map_mod", 1, 0, 1);
        Counts("map_mod_object", 1, 0, 1);
        Counts("match", 4, 0, 4);
        Counts("match_round", 7, 1, 6);
        Counts("match_round_player", 28, 4, 24);
        Counts("match_round_player_path", 28, 5, 23);
        Counts("player_join", 4, 0, 4);
        Assert.Contains(result.Notes, n => n.Contains("1 matches had no ChangeSeq"));
        Assert.Contains(result.Notes, n => n.Contains("1 rounds without their match"));
        Assert.Contains(result.Notes, n => n.Contains("1 rounds had no RoundEnd"));
        Assert.Contains(result.Notes, n => n.Contains("1 path values were not base64"));
        Assert.Contains("Committed.", log.ToString());

        await using var pg = await target.OpenAsync();

        // Ids and cursors
        var targetSeqs = (await pg.QueryAsync<(Guid Id, long Seq)>("SELECT id, change_seq FROM match")).ToDictionary(m => m.Id, m => m.Seq);
        foreach (var (id, seq) in sourceSeqs.Where(s => s.Value != null))
            Assert.Equal(seq, targetSeqs[id]);
        Assert.Equal(maxSeq + 1, targetSeqs[oldMatch]);
        Assert.Equal(new long[] { 1, 2, 3, 1000 }, (await pg.QueryAsync<long>("SELECT id FROM player_join ORDER BY id")).ToArray());
        Assert.Equal("nulname", await pg.ExecuteScalarAsync<string>("SELECT name FROM player_join WHERE id = 1000"));
        var settingsRead = new ServerSettingsRepository(target.Factory);
        Assert.Equal("ab12", (await settingsRead.GetServerAsync("198.51.100.20:16567")).AdminKey);
        Assert.True((await settingsRead.GetServerAsync("198.51.100.21:16567")).IsTestServer);
        Assert.Equal(Start, (await pg.ExecuteScalarAsync<DateTime>("SELECT join_date FROM player_join WHERE id = 1")));

        // The path bytes are the base64 text's bytes
        var targetPaths = (await pg.QueryAsync<(Guid RoundId, string Hash, byte[] Movement, byte[] Projectiles)>(
            "SELECT round_id, player_hash, movement_path, projectile_paths FROM match_round_player_path")).ToList();
        Assert.Equal(23, targetPaths.Count);
        foreach (var path in targetPaths)
        {
            var original = sourcePaths[(path.RoundId, path.Hash)];
            Assert.Equal(Convert.FromBase64String(original.Movement), path.Movement);
            Assert.Equal(original.Projectiles == null ? null : Convert.FromBase64String(original.Projectiles), path.Projectiles);
        }

        Assert.Contains(targetPaths, p => p.Movement.Length > 10_000);

        // The export reads the same on both, but for the round without an end (now its start) and the dropped path
        var postgres = new MatchRepository(target.Factory);
        var targetExport = (await postgres.GetMatchesChangedSinceAsync(0, 100)).ToDictionary(m => m.Id);
        foreach (var match in sourceExport)
        {
            var copied = targetExport[match.Id];
            Assert.Equal(match.ChangeSeq, copied.ChangeSeq);
            Assert.Equal(match.MatchStart, copied.MatchStart);
            Assert.Equal(match.MatchRounds.Select(r => r.Id), copied.MatchRounds.Select(r => r.Id));
            foreach (var (round, copiedRound) in match.MatchRounds.Zip(copied.MatchRounds))
            {
                Assert.Equal(round.RoundEnd ?? round.RoundStart, copiedRound.RoundEnd);
                foreach (var player in round.MatchRoundPlayers)
                {
                    var copiedPlayer = copiedRound.MatchRoundPlayers.Single(p => p.PlayerHash == player.PlayerHash);
                    Assert.Equal(player.StartPosition?.ToString(), copiedPlayer.StartPosition?.ToString());
                    Assert.Equal(player.MovementPathJson == "not base64" ? null : player.MovementPathJson, copiedPlayer.MovementPathJson);
                    Assert.Equal(player.ProjectilePathsJson, copiedPlayer.ProjectilePathsJson);
                }
            }
        }

        // The sequences continue above the copied values
        Assert.Equal(maxSeq + 2, result.NextChangeSeq);
        var next = ExportTests.NewMatch(Start.AddDays(1));
        await postgres.InsertMatchAsync(next);
        Assert.Equal(maxSeq + 2, next.ChangeSeq);
        await new JoinHistoryRepository(target.Factory).InsertAsync(new PlayerJoin { Name = "new", Guid = ExportTests.HashA1, IpAddress = "203.0.113.7", ServerIpAddress = "198.51.100.20", JoinDate = Start });
        Assert.Equal(1001, await pg.ExecuteScalarAsync<long>("SELECT max(id) FROM player_join"));

        // A second copy into the now filled database is refused
        var refused = await Assert.ThrowsAsync<DatabaseCopyException>(() => new SqlServerToPostgresCopier(source.ConnectionString, target.ConnectionString, null).CopyAsync());
        Assert.Contains("not empty", refused.Message);
    }

    [SqlServerAndPostgresFact]
    public async Task CopierNumbersASourceWithoutTheCursorColumn()
    {
        await using var source = await ScratchSqlServer.CreateAsync();
        await using var target = await ScratchPostgres.CreateAsync();

        // A database from before the export cursor: no ChangeSeq column at all
        await source.ExecuteAsync("DROP INDEX [IX_Match_ChangeSeq] ON [dbo].[Match]; ALTER TABLE [dbo].[Match] DROP COLUMN [ChangeSeq]; DROP SEQUENCE [dbo].[MatchChangeSeq]");
        foreach (var (id, start) in new[] { ("c", Start.AddHours(2)), ("a", Start), ("b", Start.AddHours(1)) })
        {
            await source.ExecuteAsync(
                @"INSERT INTO [dbo].[Match] ([Id], [ServerId], [ServerName], [Map], [Type], [TeamAHash], [TeamAName], [TeamAScore],
                    [TeamBHash], [TeamBName], [TeamBScore], [MatchStart], [MatchEnd])
                  VALUES (@Id, '198.51.100.20:16567', 'old', 'dalian_plant', 'HELI_2V2', 'a', 'A', 0, 'b', 'B', 0, @Start, @Start)",
                new { Id = Guid.Parse(new string(id[0], 8) + "-0000-0000-0000-000000000000"), Start = start });
        }

        var log = new StringWriter();
        var result = await new SqlServerToPostgresCopier(source.ConnectionString, target.ConnectionString, log).CopyAsync();
        Assert.True(result.Committed, log.ToString());
        Assert.Contains(result.Notes, n => n.Contains("no dbo.Match.ChangeSeq"));

        await using var pg = await target.OpenAsync();
        Assert.Equal(new long[] { 1, 2, 3 }, (await pg.QueryAsync<long>("SELECT change_seq FROM match ORDER BY match_start")).ToArray());
        Assert.Equal(4, result.NextChangeSeq);
        Assert.Equal(0, result["player_join"].Copied);
    }

    // ---------------------------------------------------------------- scratch databases

    private sealed class ScratchPostgres : IAsyncDisposable
    {
        private readonly string _server;
        private readonly string _database;

        public string ConnectionString { get; }

        public IDbContextFactory<BF2Context> Factory { get; }

        private ScratchPostgres(string server, string database)
        {
            _server = server;
            _database = database;
            ConnectionString = new NpgsqlConnectionStringBuilder(server) { Database = database }.ConnectionString;
            Factory = new DelegateContextFactory(Context);
        }

        public static async Task<ScratchPostgres> CreateAsync(bool migrate = false)
        {
            var server = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(PostgresFactAttribute.Variable)) { Database = "postgres" }.ConnectionString;
            var database = "bf2wa_test_" + Guid.NewGuid().ToString("N")[..12];
            await using (var connection = new NpgsqlConnection(server))
                await connection.ExecuteAsync($"CREATE DATABASE {database}");

            var scratch = new ScratchPostgres(server, database);
            if (migrate)
            {
                await using var context = scratch.Context();
                await context.Database.MigrateAsync();
            }

            return scratch;
        }

        public PostgresBF2Context Context() => new(DatabaseProviders.PostgresOptions(ConnectionString));

        public async Task<NpgsqlConnection> OpenAsync()
        {
            var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            return connection;
        }

        public async ValueTask DisposeAsync()
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(_server);
            await connection.ExecuteAsync($"DROP DATABASE IF EXISTS {_database} WITH (FORCE)");
        }
    }

    private sealed class ScratchSqlServer : IAsyncDisposable
    {
        private readonly string _master;
        private readonly string _database;

        public string ConnectionString { get; }

        private ScratchSqlServer(string master, string database)
        {
            _master = master;
            _database = database;
            ConnectionString = new SqlConnectionStringBuilder(master) { InitialCatalog = database }.ConnectionString;
        }

        /// <summary>A new database with Schema.sql, as in ExportTests.</summary>
        public static async Task<ScratchSqlServer> CreateAsync()
        {
            var master = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable(SqlServerFactAttribute.Variable)).ConnectionString;
            var database = "bf2wa_copy_" + Guid.NewGuid().ToString("N")[..12];
            await using (var connection = new SqlConnection(master))
                await connection.ExecuteAsync($"CREATE DATABASE [{database}]");

            var scratch = new ScratchSqlServer(master, database);
            var schema = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Schema.sql"));
            foreach (var batch in Regex.Split(schema, @"^\s*GO\s*$", RegexOptions.Multiline).Where(b => !string.IsNullOrWhiteSpace(b)))
                await scratch.ExecuteAsync(batch);
            return scratch;
        }

        public async Task ExecuteAsync(string sql, object parameters = null)
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.ExecuteAsync(sql, parameters);
        }

        public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object parameters = null)
        {
            await using var connection = new SqlConnection(ConnectionString);
            return await connection.QueryAsync<T>(sql, parameters);
        }

        public async ValueTask DisposeAsync()
        {
            SqlConnection.ClearAllPools();
            await using var connection = new SqlConnection(_master);
            await connection.ExecuteAsync($"ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_database}]");
        }
    }
}

/// <summary>A test that needs a Postgres server: skipped unless BF2WA_TEST_POSTGRES is set (e.g. "Host=127.0.0.1;Port=5432;Username=postgres;Password=dev").</summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public const string Variable = "BF2WA_TEST_POSTGRES";

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
            Skip = $"Set {Variable} to a Postgres connection string to run it";
    }
}

/// <summary>A test that needs both a SQL Server (BF2WA_TEST_SQLSERVER) and a Postgres server (BF2WA_TEST_POSTGRES).</summary>
public sealed class SqlServerAndPostgresFactAttribute : FactAttribute
{
    public SqlServerAndPostgresFactAttribute()
    {
        var missing = new[] { SqlServerFactAttribute.Variable, PostgresFactAttribute.Variable }
            .Where(v => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(v)))
            .ToList();
        if (missing.Count > 0)
            Skip = $"Set {string.Join(" and ", missing)} to run it";
    }
}
