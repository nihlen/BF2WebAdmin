using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using BF2WebAdmin.Data.Repositories;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using Match = BF2WebAdmin.Data.Entities.Match;
using ServerEntity = BF2WebAdmin.Data.Entities.Server;

namespace BF2WebAdmin.Tests;

/// <summary>
/// WebAdmin on Postgres (database bf2webadmin): the migrations and the EF repositories.
/// Each test gets a scratch database, dropped afterwards. Runs only when BF2WA_TEST_POSTGRES holds a connection string of
/// a server where it may create databases.
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
            "server", "server_ban", "server_module", "server_player_auth",
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
    public async Task BanRecordsRoundTrip()
    {
        await using var db = await ScratchPostgres.CreateAsync(migrate: true);
        await ServerAdminTests.RepositoryRoundTripsAsync(new ServerBanRepository(db.Factory));
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
