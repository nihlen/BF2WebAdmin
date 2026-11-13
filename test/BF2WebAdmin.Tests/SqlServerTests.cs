using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data;
using BF2WebAdmin.Data.Entities;
using BF2WebAdmin.Data.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;
using ServerEntity = BF2WebAdmin.Data.Entities.Server;

namespace BF2WebAdmin.Tests;

/// <summary>
/// WebAdmin on SQL Server: the migrations (a new database, and one from before EF Core with the old Schema.sql,
/// Fixtures/sqlserver-schema-0.1.0.sql) and the EF repositories. Each test gets a scratch database, dropped afterwards.
/// Runs only when BF2WA_TEST_SQLSERVER holds a connection string of a server where it may create databases, e.g.
/// "Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true".
/// </summary>
public class SqlServerTests
{
    private static readonly DateTime Start = ExportTests.Start;

    // ---------------------------------------------------------------- schema

    [SqlServerFact]
    public async Task MigrationsCreateTheOldSchema()
    {
        await using var fresh = await ScratchSqlServer.CreateAsync(migrate: true);
        await using (var context = fresh.Context())
        {
            Assert.False(context.Database.HasPendingModelChanges());
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        }

        // The same tables, columns, keys and indexes as a database from the old Schema.sql once migrated
        await using var old = await ScratchSqlServer.CreateAsync();
        await old.RunLegacySchemaAsync();
        await using (var context = old.Context())
            await context.Database.MigrateAsync();

        Assert.Equal(await old.DescribeAsync(), await fresh.DescribeAsync());
        Assert.Contains("JoinHistory.Id int NO", await fresh.DescribeAsync());
        Assert.Contains("index Match.IX_Match_ChangeSeq unique ([ChangeSeq] IS NOT NULL)", await fresh.DescribeAsync());
    }

    /// <summary>
    /// A database of an older WebAdmin: its matches get export numbers in start order, the missing table is created, the
    /// data stays, and the repositories carry on from there. A second start changes nothing.
    /// </summary>
    [SqlServerFact]
    public async Task AnOldDatabaseIsTakenOver()
    {
        await using var db = await ScratchSqlServer.CreateAsync();
        await db.RunLegacySchemaAsync();
        await using (var connection = await db.OpenAsync())
        {
            foreach (var (id, start) in new[] { (Guid.NewGuid(), Start.AddDays(-1)), (Guid.NewGuid(), Start.AddDays(-2)) })
            {
                await connection.ExecuteAsync(
                    @"INSERT INTO [dbo].[Match] ([Id], [ServerId], [ServerName], [Map], [Type], [TeamAHash], [TeamAName], [TeamAScore],
                        [TeamBHash], [TeamBName], [TeamBScore], [MatchStart], [MatchEnd])
                      VALUES (@Id, '1.2.3.4:16567', 'old', 'dalian_plant', 'HELI_2V2', 'a', 'A', 0, 'b', 'B', 0, @Start, @Start)",
                    new { Id = id, Start = start });
            }

            await connection.ExecuteAsync("INSERT INTO [dbo].[Server] ([ServerId], [ServerGroup], [GamePort], [QueryPort], [RconPort]) VALUES ('1.2.3.4:16567', '2v2', 16567, 29900, 0)");
            await connection.ExecuteAsync("DROP TABLE [dbo].[ServerBan]");
        }

        for (var i = 0; i < 2; i++)
        {
            await using var context = db.Context();
            await context.Database.MigrateAsync();
        }

        await using (var connection = await db.OpenAsync())
        {
            var old = (await connection.QueryAsync<long>("SELECT [ChangeSeq] FROM [dbo].[Match] ORDER BY [MatchStart]")).ToList();
            Assert.Equal(new long[] { 1, 2 }, old);
        }

        var settings = new ServerSettingsRepository(db.Factory);
        var server = Assert.Single(await settings.GetServersAsync());
        Assert.False(server.IsTestServer);
        Assert.Null(server.AdminKey);

        var repository = new MatchRepository(db.Factory);
        await ExportTests.AssertCursorBehaviourAsync(() => repository, existingMatches: 2);
        await ServerAdminTests.RepositoryRoundTripsAsync(new ServerBanRepository(db.Factory));
    }

    // ---------------------------------------------------------------- repositories

    [SqlServerFact]
    public async Task WritesGiveIncreasingChangeSeqs()
    {
        await using var db = await ScratchSqlServer.CreateAsync(migrate: true);
        var repository = new MatchRepository(db.Factory);
        await ExportTests.AssertCursorBehaviourAsync(() => repository);

        // A whole match has its players' paths, as stored
        var round = ExportTests.NewRound(Guid.Empty, Start, winningTeam: 1);
        var finished = (await repository.GetMatchesChangedSinceAsync(0, 10)).Single(m => m.MatchEnd != null);
        var whole = await repository.GetMatchAsync(finished.Id);
        Assert.All(whole.MatchRounds.SelectMany(r => r.MatchRoundPlayers), p =>
        {
            Assert.Equal(finished.Id, p.MatchId);
            Assert.Equal(round.MatchRoundPlayers[0].MovementPathJson, p.MovementPathJson);
        });
        Assert.Equal(round.PositionTrackerInterval, whole.MatchRounds[0].PositionTrackerInterval);
        Assert.Null(await repository.GetMatchAsync(Guid.NewGuid()));
    }

    [SqlServerFact]
    public async Task JoinsAreInsertedAndReadById()
    {
        await using var db = await ScratchSqlServer.CreateAsync(migrate: true);
        var joins = new JoinHistoryRepository(db.Factory);

        await joins.InsertAsync(JoinHistoryTests.Join(JoinHistoryTests.Info(name: new string('x', 150)))!);
        await joins.InsertAsync(new PlayerJoin { Name = "p1", Guid = ExportTests.HashA1, IpAddress = "203.0.113.8", ServerIpAddress = "198.51.100.20", JoinDate = Start });
        await joins.InsertAsync(new PlayerJoin { Name = "p2", Guid = ExportTests.HashA1, IpAddress = "203.0.113.9", ServerIpAddress = "198.51.100.20", JoinDate = Start });

        var all = await joins.GetJoinsSinceAsync(0, 10);
        Assert.Equal(new long[] { 1, 2, 3 }, all.Select(j => j.Id));
        Assert.Equal(new string('x', 100), all[0].Name);
        Assert.Equal(JoinHistoryTests.Hash, all[0].Guid);
        Assert.Equal(new DateTime(2026, 11, 8, 13, 30, 15), all[0].JoinDate);
        Assert.All(all, j => Assert.False(j.IsLikelySpoof));
        Assert.Equal(new[] { "p1", "p2" }, (await joins.GetJoinsSinceAsync(1, 10)).Select(j => j.Name));

        await Assert.ThrowsAsync<DbUpdateException>(() => joins.InsertAsync(new PlayerJoin { Name = "p3", JoinDate = Start }));
    }

    [SqlServerFact]
    public async Task BanRecordsRoundTrip()
    {
        await using var db = await ScratchSqlServer.CreateAsync(migrate: true);
        await ServerAdminTests.RepositoryRoundTripsAsync(new ServerBanRepository(db.Factory));
    }

    [SqlServerFact]
    public async Task ServerSettingsAndMapsRoundTrip()
    {
        await using var db = await ScratchSqlServer.CreateAsync(migrate: true);
        var settings = new ServerSettingsRepository(db.Factory);

        await settings.SetServerAsync(new ServerEntity { ServerId = "198.51.100.20:16567", ServerGroup = "2v2", IpAddress = "198.51.100.20", GamePort = 16567, QueryPort = 29900, RconPort = 4711, AdminKey = "ab12" });
        await settings.SetServerAsync(new ServerEntity { ServerId = "198.51.100.21:16567", ServerGroup = "test", GamePort = 16567, QueryPort = 29900, IsTestServer = true });
        await settings.SetServerAsync(new ServerEntity { ServerId = "198.51.100.20:16567", ServerGroup = "2v2", GamePort = 16567, QueryPort = 29901, RconPassword = "secret", AdminKey = "cd34" });

        var servers = (await settings.GetServersAsync()).OrderBy(s => s.ServerId).ToList();
        Assert.Equal(2, servers.Count);
        Assert.Equal(29901, servers[0].QueryPort);
        Assert.Equal("cd34", servers[0].AdminKey);
        Assert.True(servers[1].IsTestServer);
        Assert.Equal(new[] { "2v2", "test" }, (await settings.GetServerGroupsAsync()).OrderBy(g => g));

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

        // The objects go with the map (the foreign key does not cascade)
        await maps.DeleteAsync(map.Id);
        Assert.Empty(await maps.GetAsync());
    }

    // ---------------------------------------------------------------- scratch databases

    private sealed class ScratchSqlServer : IAsyncDisposable
    {
        private readonly string _master;
        private readonly string _database;

        public string ConnectionString { get; }

        public IDbContextFactory<BF2Context> Factory { get; }

        private ScratchSqlServer(string master, string database)
        {
            _master = master;
            _database = database;
            ConnectionString = new SqlConnectionStringBuilder(master) { InitialCatalog = database }.ConnectionString;
            Factory = new DelegateContextFactory(Context);
        }

        public static async Task<ScratchSqlServer> CreateAsync(bool migrate = false)
        {
            var master = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable(SqlServerFactAttribute.Variable)) { InitialCatalog = "master" }.ConnectionString;
            var database = "bf2wa_test_" + Guid.NewGuid().ToString("N")[..12];
            await using (var connection = new SqlConnection(master))
                await connection.ExecuteAsync($"CREATE DATABASE [{database}]");

            var scratch = new ScratchSqlServer(master, database);
            if (migrate)
            {
                await using var context = scratch.Context();
                await context.Database.MigrateAsync();
            }

            return scratch;
        }

        public SqlServerBF2Context Context() => new(DatabaseProviders.SqlServerOptions(ConnectionString));

        public async Task<SqlConnection> OpenAsync()
        {
            var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            return connection;
        }

        /// <summary>The schema of WebAdmin 0.1.0, the last one before EF Core on SQL Server.</summary>
        public async Task RunLegacySchemaAsync()
        {
            var schema = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sqlserver-schema-0.1.0.sql"));
            await using var connection = await OpenAsync();
            foreach (var batch in Regex.Split(schema, @"^\s*GO\s*$", RegexOptions.Multiline).Where(b => !string.IsNullOrWhiteSpace(b)))
                await connection.ExecuteAsync(batch);
        }

        /// <summary>The columns, keys, foreign keys and indexes, one line each, sorted.</summary>
        public async Task<string[]> DescribeAsync()
        {
            await using var connection = await OpenAsync();
            var columns = await connection.QueryAsync<string>(
                @"SELECT t.name + '.' + c.name + ' ' + ty.name
                    + CASE WHEN ty.name IN ('nvarchar', 'decimal', 'datetime2') THEN '(' + CAST(c.max_length AS varchar) + ',' + CAST(c.precision AS varchar) + ',' + CAST(c.scale AS varchar) + ')' ELSE '' END
                    + CASE WHEN c.is_nullable = 1 THEN ' YES' ELSE ' NO' END
                  FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id JOIN sys.types ty ON ty.user_type_id = c.user_type_id
                  WHERE t.name <> '__EFMigrationsHistory'");
            var indexes = await connection.QueryAsync<string>(
                @"SELECT 'index ' + t.name + '.' + i.name + CASE WHEN i.is_unique = 1 THEN ' unique' ELSE '' END
                    + CASE WHEN i.has_filter = 1 THEN ' ' + i.filter_definition ELSE '' END
                  FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id
                  WHERE i.name IS NOT NULL AND t.name <> '__EFMigrationsHistory'");
            var foreignKeys = await connection.QueryAsync<string>("SELECT 'fk ' + name FROM sys.foreign_keys");
            var sequences = await connection.QueryAsync<string>("SELECT 'sequence ' + name FROM sys.sequences");
            return columns.Concat(indexes).Concat(foreignKeys).Concat(sequences).Order(StringComparer.Ordinal).ToArray();
        }

        public async ValueTask DisposeAsync()
        {
            SqlConnection.ClearAllPools();
            await using var connection = new SqlConnection(_master);
            await connection.ExecuteAsync($"ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_database}]");
        }
    }
}

/// <summary>A test that needs a SQL Server: skipped unless BF2WA_TEST_SQLSERVER is set.</summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public const string Variable = "BF2WA_TEST_SQLSERVER";

    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
            Skip = $"Set {Variable} to a SQL Server connection string to run it";
    }
}
