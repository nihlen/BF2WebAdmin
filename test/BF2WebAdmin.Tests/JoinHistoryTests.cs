using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BF2WebAdmin.Data;
using BF2WebAdmin.Data.Entities;
using BF2WebAdmin.Data.Repositories;
using BF2WebAdmin.Server;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Entities;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>The dbo.JoinHistory rows for the BF2v2 site: which connects get one, and what goes in it.</summary>
public class JoinHistoryTests
{
    private const string Hash = "0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset Time = new(2026, 11, 8, 14, 30, 15, TimeSpan.FromHours(1));

    private static AdminPlayerInfo Info(string name = "[TAG] netsky", string ip = "203.0.113.7", string hash = Hash,
        byte flags = AdminPlayerInfo.FlagConnected | AdminPlayerInfo.FlagIdentity, byte id = 3) =>
        new(id, name, flags, 0, ip, hash,
            new AdminPlayerScore(0, 0, 0, 0, 0, -1),
            new AdminPlayerState(1, 0, 1, 0, 0, 0, AdminProtocol.NoConsoleId, 255, default, default, default, 1,
                AdminPlayerState.ViewInside, 0));

    private static PlayerJoin? Join(AdminPlayerInfo info, string? serverIp = "198.51.100.20", IPAddress? resolved = null) =>
        JoinHistoryWriter.CreateJoin(info, serverIp, resolved ?? IPAddress.None, Time);

    [Fact]
    public void HumanPlayerGetsARow()
    {
        var join = Join(Info());

        Assert.NotNull(join);
        Assert.Equal("[TAG] netsky", join.Name);
        Assert.Equal(Hash, join.Guid);
        Assert.Equal("203.0.113.7", join.IpAddress);
        Assert.Equal("198.51.100.20", join.ServerIpAddress);
        Assert.False(join.IsLikelySpoof);
    }

    [Fact]
    public void JoinDateIsUtc()
    {
        var join = Join(Info())!;

        Assert.Equal(DateTimeKind.Utc, join.JoinDate.Kind);
        Assert.Equal(new DateTime(2026, 11, 8, 13, 30, 15, DateTimeKind.Utc), join.JoinDate);
    }

    [Theory]
    [InlineData(AdminPlayerInfo.FlagAi)]
    [InlineData(AdminPlayerInfo.FlagBotChannel)]
    [InlineData(AdminPlayerInfo.FlagAi | AdminPlayerInfo.FlagBotChannel)]
    public void BotsAreSkipped(byte botFlags)
    {
        Assert.Null(Join(Info(flags: (byte)(AdminPlayerInfo.FlagConnected | AdminPlayerInfo.FlagIdentity | botFlags))));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("name:[TAG] netsky")]
    [InlineData("0123456789abcdef0123456789abcdef0")] // longer than the column
    public void PlayersWithoutACdKeyHashAreSkipped(string hash)
    {
        Assert.Null(Join(Info(hash: hash)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not an ip")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("0.0.0.0")]
    [InlineData("255.255.255.255")]
    public void PlayersWithoutARemoteIpAreSkipped(string ip)
    {
        Assert.Null(Join(Info(ip: ip)));
    }

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("203.0.113.7:16567", "203.0.113.7")]
    [InlineData(" 203.0.113.7 ", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("[2001:db8::7]:16567", "2001:db8::7")]
    public void IpAddressHasNoPort(string ip, string expected)
    {
        Assert.Equal(expected, Join(Info(ip: ip))!.IpAddress);
    }

    [Fact]
    public void LongNamesAreTruncatedTo100()
    {
        var name = new string('x', 150);

        Assert.Equal(new string('x', PlayerJoin.NameLength), Join(Info(name: name))!.Name);
        Assert.Equal("netsky", Join(Info(name: "  netsky "))!.Name);
    }

    [Theory]
    [InlineData("198.51.100.20", null, "198.51.100.20")]
    [InlineData("198.51.100.20:16567", null, "198.51.100.20")]
    [InlineData("bf2.example.com", "198.51.100.21", "198.51.100.21")]
    [InlineData("bf2.example.com", null, "bf2.example.com")]
    [InlineData(null, null, "")]
    public void ServerIpIsTheConfiguredIp(string? configured, string? resolved, string expected)
    {
        Assert.Equal(expected, Join(Info(), configured, resolved != null ? IPAddress.Parse(resolved) : null)!.ServerIpAddress);
    }

    // ---------------------------------------------------------------- applier

    private sealed class RecordingWriter : IJoinHistoryWriter
    {
        public readonly List<PlayerJoin> Joins = new();
        public void Enqueue(PlayerJoin join) => Joins.Add(join);
    }

    private sealed class ThrowingWriter : IJoinHistoryWriter
    {
        public void Enqueue(PlayerJoin join) => throw new InvalidOperationException("database down");
    }

    private static GameServer Server(IJoinHistoryWriter writer)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(writer)
            .BuildServiceProvider();
        var info = new ServerInfo { ServerId = "198.51.100.20:16567", IpAddress = "198.51.100.20" };
        return new GameServer(info, IPAddress.Parse("198.51.100.20"), services, CancellationToken.None);
    }

    private static readonly AdminGameState Game = new(1, 3, -1, 100, 100, 10, 0, 0, 0);

    [Fact]
    public async Task PlayerConnectQueuesOneRowForHumansOnly()
    {
        var writer = new RecordingWriter();
        var server = Server(writer);

        await server.Applier.OnMessageAsync(null!, new EvPlayerConnect(1, 1, Info(id: 3)));
        await server.Applier.OnMessageAsync(null!, new EvPlayerConnect(2, 2, Info(name: "bot", id: 4, hash: "",
            flags: AdminPlayerInfo.FlagAi | AdminPlayerInfo.FlagBotChannel | AdminPlayerInfo.FlagConnected)));

        var join = Assert.Single(writer.Joins);
        Assert.Equal("[TAG] netsky", join.Name);
        Assert.Equal("198.51.100.20", join.ServerIpAddress);
        Assert.Equal(2, server.Players.Count());
    }

    [Fact]
    public async Task SnapshotIsNotAJoin()
    {
        var writer = new RecordingWriter();
        var server = Server(writer);

        await server.Applier.OnMessageAsync(null!, new Snapshot(0, 0, 100, 3.3, Game, [Info(id: 3)], [], [], []));

        Assert.Empty(writer.Joins);
        Assert.Single(server.Players);
    }

    [Fact]
    public async Task AWriterErrorDoesNotBreakTheConnect()
    {
        var server = Server(new ThrowingWriter());

        await server.Applier.OnMessageAsync(null!, new EvPlayerConnect(1, 1, Info(id: 3)));

        Assert.Single(server.Players);
    }

    // ---------------------------------------------------------------- repositories

    [Fact]
    public async Task SqliteMigrationCreatesTheTable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BF2Context>().UseSqlite(connection).Options;
        await using (var context = new BF2Context(options))
        {
            await context.Database.MigrateAsync();
            await new JoinHistoryRepository(context).InsertAsync(Join(Info())!);
        }

        await using (var context = new BF2Context(options))
        {
            var row = Assert.Single(context.JoinHistory);
            Assert.Equal(Hash, row.Guid);
            Assert.Equal("203.0.113.7", row.IpAddress);
            Assert.Equal(new DateTime(2026, 11, 8, 13, 30, 15), row.JoinDate);
        }
    }

    /// <summary>
    /// Against a real SQL Server: a scratch database gets Schema.sql, a row goes in through the repository, and the
    /// database is dropped. Runs only when BF2WA_TEST_SQLSERVER holds a connection string (to master) of a server it may
    /// create databases on, e.g. "Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true".
    /// </summary>
    [SqlServerFact]
    public async Task SqlServerInsertMatchesTheSchema()
    {
        var master = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable(SqlServerFactAttribute.Variable));
        var database = "bf2wa_joinhistory_" + Guid.NewGuid().ToString("N")[..12];
        await using (var connection = new SqlConnection(master.ConnectionString))
            await connection.ExecuteAsync($"CREATE DATABASE [{database}]");

        try
        {
            var scratch = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = database }.ConnectionString;
            await using (var connection = new SqlConnection(scratch))
            {
                var schema = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Schema.sql"));
                foreach (var batch in Regex.Split(schema, @"^\s*GO\s*$", RegexOptions.Multiline).Where(b => !string.IsNullOrWhiteSpace(b)))
                    await connection.ExecuteAsync(batch);
                // Schema.sql is idempotent
                foreach (var batch in Regex.Split(schema, @"^\s*GO\s*$", RegexOptions.Multiline).Where(b => !string.IsNullOrWhiteSpace(b)))
                    await connection.ExecuteAsync(batch);
            }

            var repository = new SqlJoinHistoryRepository(scratch);
            await repository.InsertAsync(Join(Info(name: new string('x', 150)))!);
            await repository.InsertAsync(Join(Info(ip: "203.0.113.8:16567"))!);

            await using (var connection = new SqlConnection(scratch))
            {
                var rows = (await connection.QueryAsync<PlayerJoin>(
                    "SELECT [Id], [Name], [Guid], [IpAddress], [ServerIpAddress], [JoinDate], [IsLikelySpoof] FROM [dbo].[JoinHistory] ORDER BY [Id]")).ToList();
                Assert.Equal(2, rows.Count);
                Assert.Equal(new string('x', 100), rows[0].Name);
                Assert.Equal(Hash, rows[0].Guid);
                Assert.Equal("203.0.113.8", rows[1].IpAddress);
                Assert.Equal("198.51.100.20", rows[1].ServerIpAddress);
                Assert.Equal(new DateTime(2026, 11, 8, 13, 30, 15), rows[1].JoinDate);
                Assert.All(rows, r => Assert.False(r.IsLikelySpoof));
            }
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await using var connection = new SqlConnection(master.ConnectionString);
            await connection.ExecuteAsync($"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]");
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
