using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using BF2WebAdmin.Data.Repositories;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Services;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>Kicks, warnings, bans and the "!" commands over the admin channel (<see cref="ServerAdmin"/>), and the ban records.</summary>
public class ServerAdminTests
{
    private const string ServerId = "198.51.100.20:16567";
    private const string HashA = "0123456789abcdef0123456789abcdef";
    private const string HashB = "fedcba9876543210fedcba9876543210";

    private readonly IGameServer _server = Substitute.For<IGameServer>();
    private readonly IGameWriter _writer = Substitute.For<IGameWriter>();
    private readonly IServerBanRepository _bans = Substitute.For<IServerBanRepository>();
    private readonly Player _netsky = new() { Index = 3, Name = "[TAG] netsky", Hash = HashA, IpAddress = IPAddress.Parse("203.0.113.7") };
    private readonly Player _other = new() { Index = 4, Name = "other", Hash = "", IpAddress = IPAddress.Parse("203.0.113.8") };
    private readonly ServerAdmin _admin;

    public ServerAdminTests()
    {
        _server.Id.Returns(ServerId);
        _server.GameWriter.Returns(_writer);
        _server.Players.Returns(new[] { _netsky, _other });
        _writer.ConsoleAsync(Arg.Any<string>()).Returns(Array.Empty<string>());
        _bans.GetBansAsync(ServerId).Returns(Array.Empty<ServerBan>());
        _admin = new ServerAdmin(_server, _bans);
    }

    [Theory]
    [InlineData("Perm", BanKind.Permanent, 0)]
    [InlineData("permanent", BanKind.Permanent, 0)]
    [InlineData("Round", BanKind.Round, 0)]
    [InlineData("3600", BanKind.Timed, 3600)]
    [InlineData("30m", BanKind.Timed, 1800)]
    [InlineData("12h", BanKind.Timed, 43200)]
    [InlineData("7d", BanKind.Timed, 604800)]
    [InlineData("2W", BanKind.Timed, 1209600)]
    public void BanLengthsParse(string text, BanKind kind, int seconds)
    {
        Assert.True(BanLength.TryParse(text, out var length));
        Assert.Equal(kind, length.Kind);
        Assert.Equal(TimeSpan.FromSeconds(seconds), length.Duration);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1y")]
    [InlineData("spam")]
    [InlineData("99999999w")]
    public void OtherBanLengthsDoNot(string? text)
    {
        Assert.False(BanLength.TryParse(text, out _));
    }

    [Fact]
    public void TheEnginesBanListParses()
    {
        var entries = ServerAdmin.ParseBanList(new[]
        {
            $"Key: {HashA} Perm\nKey: {HashB} Time Left: 3599\n",
            "IP: 203.0.113.9 Round",
            "",
            "something else",
        }).ToList();

        Assert.Equal(new[]
        {
            (HashA, true, BanKind.Permanent, (int?)null),
            (HashB, true, BanKind.Timed, 3599),
            ("203.0.113.9", false, BanKind.Round, null),
        }, entries);
    }

    [Fact]
    public async Task KickGoesOverTheAdminChannel()
    {
        var answer = await _admin.RunAsync("!k 3 bad language", "alex");

        await _writer.Received(1).KickAsync(_netsky, "bad language");
        _writer.Received(1).SendText("[TAG] netsky was kicked: bad language");
        Assert.Equal("Kicked [TAG] netsky", answer);
    }

    [Fact]
    public async Task KickWithoutAReasonSaysWho()
    {
        await _admin.RunAsync("!kick netsky", "alex");

        await _writer.Received(1).KickAsync(_netsky, "Kicked by alex");
    }

    [Fact]
    public async Task BanIsByCdKeyAndRecorded()
    {
        var answer = await _admin.RunAsync("!ban NETSKY 7d \"spawn camping\"", "alex");

        await _writer.Received(1).BanAsync(_netsky, true, BanLength.For(TimeSpan.FromDays(7)), "spawn camping");
        await _bans.Received(1).SetBanAsync(Arg.Is<ServerBan>(b =>
            b.ServerId == ServerId && b.Identifier == HashA && b.ByCdKey && b.Name == "[TAG] netsky" && b.BannedBy == "alex" &&
            b.Reason == "spawn camping" && b.BannedAt.Kind == DateTimeKind.Utc));
        Assert.Equal("Banned [TAG] netsky for 1 week", answer);
    }

    [Fact]
    public async Task BanWithoutALengthIsPermanentAndWithoutAHashByAddress()
    {
        await _admin.RunAsync("!b 4 cheating", "alex");

        await _writer.Received(1).BanAsync(_other, false, BanLength.Permanent, "cheating");
        await _bans.Received(1).SetBanAsync(Arg.Is<ServerBan>(b => b.Identifier == "203.0.113.8" && !b.ByCdKey));
    }

    [Fact]
    public async Task ARefusedBanIsNotRecorded()
    {
        _writer.BanAsync(default!, default, default, default!).ReturnsForAnyArgs(Task.FromException(new AdminCommandException("bot", AdminErrorCode.Refused)));

        var e = await Assert.ThrowsAsync<AdminCommandException>(() => _admin.RunAsync("!ban 3", "alex"));

        Assert.Equal("The server refused: bot", ServerAdmin.Describe(e));
        await _bans.DidNotReceiveWithAnyArgs().SetBanAsync(default!);
    }

    [Theory]
    [InlineData("!kick", "Usage: !kick <player> [reason]")]
    [InlineData("!kick 9", "No player 9")]
    [InlineData("!kick nobody", "No player matches 'nobody'")]
    [InlineData("!kick e", "2 players match 'e': 3 [TAG] netsky, 4 other")]
    [InlineData("!unban 203.0.113.9 x", "Not a CD-key hash or an address: '203.0.113.9 x'")]
    [InlineData("!map dalian", "Usage: !map <map> <mode> [size]")]
    public async Task BadCommandsSayWhy(string command, string message)
    {
        var e = await Assert.ThrowsAsync<AdminCommandException>(() => _admin.RunAsync(command, "alex"));
        Assert.Equal(message, e.Message);
        await _writer.DidNotReceiveWithAnyArgs().KickAsync(default!, default!);
        await _writer.DidNotReceiveWithAnyArgs().ConsoleAsync(default!);
    }

    [Fact]
    public async Task WarnIsAChatMessage()
    {
        Assert.Equal("Warned [TAG] netsky", await _admin.RunAsync("!w 3 stop spawn camping", "alex"));
        _writer.Received(1).SendText("Warning [TAG] netsky: stop spawn camping");
    }

    [Fact]
    public async Task UnbanRemovesTheKeyAndTheRecord()
    {
        _writer.ConsoleAsync("admin.listBannedKeys").Returns(new[] { $"Key: {HashA} Perm" });

        Assert.Equal($"Unbanned {HashA}", await _admin.RunAsync($"!unban {HashA}", "alex"));

        await _writer.Received(1).ConsoleAsync($"admin.removeKeyFromBanList {HashA}");
        await _bans.Received(1).RemoveBanAsync(ServerId, HashA);
    }

    [Fact]
    public async Task UnbanOfAnAddress()
    {
        Assert.Equal("203.0.113.9 was not banned", await _admin.UnbanAsync("203.0.113.9"));
        await _writer.Received(1).ConsoleAsync("admin.removeAddressFromBanList 203.0.113.9");
    }

    [Fact]
    public async Task TheBanListIsTheEnginesWithTheRecords()
    {
        _writer.ConsoleAsync("admin.listBannedKeys").Returns(new[] { $"Key: {HashA} Perm", $"Key: {HashB} Time Left: 60" });
        _writer.ConsoleAsync("admin.listBannedAddresses").Returns(new[] { "IP: 203.0.113.9 Round" });
        _bans.GetBansAsync(ServerId).Returns(new[]
        {
            new ServerBan { ServerId = ServerId, Identifier = HashB, ByCdKey = true, Name = "b", BannedBy = "alex", Reason = "r", BannedAt = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc) },
            // Lifted elsewhere (or expired): not shown
            new ServerBan { ServerId = ServerId, Identifier = "203.0.113.10", Name = "gone", BannedAt = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc) },
        });

        var bans = await _admin.GetBansAsync();

        Assert.Equal(new[] { HashB, HashA, "203.0.113.9" }, bans.Select(b => b.Identifier));
        Assert.Equal(("b", "alex", "r", BanKind.Timed), (bans[0].Name, bans[0].BannedBy, bans[0].Reason, bans[0].Kind));
        Assert.InRange(bans[0].ExpiresAt!.Value, DateTime.UtcNow.AddSeconds(50), DateTime.UtcNow.AddSeconds(61));
        Assert.Null(bans[1].Name);
        Assert.Equal((BanKind.Permanent, (DateTime?)null), (bans[1].Kind, bans[1].ExpiresAt));
        Assert.Equal((false, BanKind.Round), (bans[2].ByCdKey, bans[2].Kind));
        Assert.Equal($"b | {HashB} | until", ServerAdmin.FormatBans(bans).Split('\n')[0][..(HashB.Length + 12)]);
    }

    [Fact]
    public async Task OtherCommandsAreConsoleLines()
    {
        _writer.ConsoleAsync("sv.serverName").Returns(new[] { "netsky 2v2" });

        Assert.Equal("netsky 2v2", await _admin.RunAsync("!sv.serverName", "alex"));
        Assert.Equal("Done", await _admin.RunAsync("!exec game.sayAll hi", "alex"));
        await _admin.RunAsync("!list", "alex");
        await _admin.RunAsync("!m dalian_2_v_2 gpm_cq 16", "alex");
        await _admin.RunAsync("!clearbans", "alex");

        await _writer.Received(1).ConsoleAsync("game.sayAll hi");
        await _writer.Received(1).ConsoleAsync("admin.listPlayers");
        await _writer.Received(1).RunMapAsync("dalian_2_v_2", "gpm_cq", 16);
        await _writer.Received(1).ConsoleAsync("admin.clearBanList");
        await _bans.Received(1).RemoveBansAsync(ServerId);
    }

    [Theory]
    [InlineData(".kick 3", true)]
    [InlineData("!k 3", true)]
    [InlineData("!B netsky", true)]
    [InlineData(".banlist", true)]
    [InlineData("!switch", false)]
    [InlineData("!sv.serverName", false)]
    public void AdminCommandsAreKnownWithEitherPrefix(string text, bool expected)
    {
        Assert.Equal(expected, ServerAdmin.IsAdminCommand(text));
    }

    [Theory]
    [InlineData(".kick 3", true)]
    [InlineData("!k 3", true)]
    [InlineData("!", false)]
    [InlineData(".", false)]
    [InlineData("hello", false)]
    public void ChatCommandsStartWithADotOrABang(string text, bool expected)
    {
        Assert.Equal(expected, BF2WebAdmin.Server.ModManager.IsCommandText(text));
    }

    [Fact]
    public async Task ADotWorksLikeABang()
    {
        await _admin.RunAsync(".kick netsky spam", "alex");

        await _writer.Received(1).KickAsync(_netsky, "spam");
    }

    [Theory]
    [InlineData(typeof(BF2WebAdmin.Server.Commands.KickCommand), "kick|k", "kick netsky spam")]
    [InlineData(typeof(BF2WebAdmin.Server.Commands.WarnCommand), "warn|w", "warn netsky spam")]
    [InlineData(typeof(BF2WebAdmin.Server.Commands.BanCommand), "ban|b", "ban netsky spam")]
    [InlineData(typeof(BF2WebAdmin.Server.Commands.UnbanCommand), "unban", "unban netsky spam")]
    [InlineData(typeof(BF2WebAdmin.Server.Commands.RunMapCommand), "map|m", "map netsky spam")]
    [InlineData(typeof(BF2WebAdmin.Server.Commands.BanListCommand), "banlist", "banlist")]
    [InlineData(typeof(BF2WebAdmin.Server.Commands.ClearBansCommand), "clearbans", "clearbans")]
    [InlineData(typeof(BF2WebAdmin.Server.Commands.ListPlayersCommand), "list", "list")]
    public void TheAdminCommandsAreChatCommands(Type type, string aliases, string line)
    {
        var attribute = Assert.Single(type.GetCustomAttributes(typeof(BF2WebAdmin.Server.Extensions.CommandAttribute), false)
            .Cast<BF2WebAdmin.Server.Extensions.CommandAttribute>());
        Assert.Equal(aliases.Split('|'), attribute.Aliases);
        var command = (BF2WebAdmin.Server.Commands.AdminChatCommand)Activator.CreateInstance(type)!;
        command.Args = "netsky spam";
        Assert.Equal(line, command.Line);
        Assert.True(ServerAdmin.IsAdminCommand("!" + line));
    }

    // ---------------------------------------------------------------- repositories

    private static ServerBan Record(string identifier, string reason) => new()
    {
        ServerId = ServerId, Identifier = identifier, ByCdKey = true, Name = "netsky", BannedBy = "alex", Reason = reason,
        BannedAt = new DateTime(2026, 11, 9, 1, 2, 3, DateTimeKind.Utc),
    };

    /// <summary>The same behaviour on every provider: an upsert by server and identifier, and the two removes.</summary>
    internal static async Task RepositoryRoundTripsAsync(IServerBanRepository repository)
    {
        await repository.SetBanAsync(Record(HashA, "first"));
        await repository.SetBanAsync(Record(HashA, "second"));
        await repository.SetBanAsync(Record(HashB, "other"));
        var elsewhere = Record(HashB, "elsewhere");
        elsewhere.ServerId = "198.51.100.21:16567";
        await repository.SetBanAsync(elsewhere);

        var bans = (await repository.GetBansAsync(ServerId)).OrderBy(b => b.Identifier).ToList();
        Assert.Equal(new[] { (HashA, "second"), (HashB, "other") }, bans.Select(b => (b.Identifier, b.Reason)));
        Assert.Equal(("netsky", "alex", true), (bans[0].Name, bans[0].BannedBy, bans[0].ByCdKey));
        Assert.Equal(new DateTime(2026, 11, 9, 1, 2, 3), bans[0].BannedAt);

        await repository.RemoveBanAsync(ServerId, HashA);
        Assert.Equal(new[] { HashB }, (await repository.GetBansAsync(ServerId)).Select(b => b.Identifier));

        await repository.RemoveBansAsync(ServerId);
        Assert.Empty(await repository.GetBansAsync(ServerId));
        Assert.Single(await repository.GetBansAsync("198.51.100.21:16567"));
    }

    [Fact]
    public async Task SqliteRepositoryRoundTrips()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BF2Context>().UseSqlite(connection).Options;
        await using (var context = new BF2Context(options))
            await context.Database.MigrateAsync();

        await RepositoryRoundTripsAsync(new ServerBanRepository(new DelegateContextFactory(() => new BF2Context(options))));
    }

    [SqlServerFact]
    public async Task SqlServerRepositoryRoundTrips()
    {
        var master = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable(SqlServerFactAttribute.Variable));
        var database = "bf2wa_bans_" + Guid.NewGuid().ToString("N")[..12];
        await using (var connection = new SqlConnection(master.ConnectionString))
            await connection.ExecuteAsync($"CREATE DATABASE [{database}]");

        try
        {
            var scratch = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = database }.ConnectionString;
            var schema = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Schema.sql"));
            await using (var connection = new SqlConnection(scratch))
            {
                foreach (var batch in Regex.Split(schema, @"^\s*GO\s*$", RegexOptions.Multiline).Where(b => !string.IsNullOrWhiteSpace(b)))
                    await connection.ExecuteAsync(batch);
            }

            // The startup updates run on a database that already has the table
            await SqlServerSchemaUpdates.ApplyAsync(scratch);
            await RepositoryRoundTripsAsync(new SqlServerBanRepository(scratch));

            // ... and create it on one that has not
            await using (var connection = new SqlConnection(scratch))
                await connection.ExecuteAsync("DROP TABLE [dbo].[ServerBan]");
            await SqlServerSchemaUpdates.ApplyAsync(scratch);
            await RepositoryRoundTripsAsync(new SqlServerBanRepository(scratch));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await using var connection = new SqlConnection(master.ConnectionString);
            await connection.ExecuteAsync($"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]");
        }
    }
}
