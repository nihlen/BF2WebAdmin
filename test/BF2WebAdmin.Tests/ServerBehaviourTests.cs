using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Modules.BF2;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>Chat text truncation, the database retry rule and JailModule.</summary>
public class ServerBehaviourTests
{
    [Theory]
    [InlineData(10, "0123456789", "0123456789")]
    [InlineData(5, "0123456789", "01234...")]
    // A colour code that would be cut goes whole
    [InlineData(8, "abcdef§C1001xyz", "abcdef...")]
    [InlineData(12, "abcdef§C1001xyz", "abcdef§C1001...")]
    [InlineData(4, "§C1001xyz", "...")]
    public void Truncate_NeverCutsAColourCode(int maxLength, string text, string expected)
    {
        Assert.Equal(expected, AdminGameWriter.Truncate(text, maxLength));
    }

    [Fact]
    public void RetryableDatabaseErrors_AllProviders()
    {
        Assert.True(ModManager.IsRetryableDatabaseError(new SqliteException("busy", 5)));
        Assert.True(ModManager.IsRetryableDatabaseError(new SqliteException("locked", 6)));
        Assert.False(ModManager.IsRetryableDatabaseError(new SqliteException("constraint", 19)));
        Assert.True(ModManager.IsRetryableDatabaseError(new DbUpdateException("save", new SqliteException("busy", 5))));
        Assert.False(ModManager.IsRetryableDatabaseError(new DbUpdateException("save", new SqliteException("constraint", 19))));
        Assert.True(ModManager.IsRetryableDatabaseError(new TimeoutException()));
        Assert.False(ModManager.IsRetryableDatabaseError(new InvalidOperationException()));
        Assert.False(ModManager.IsRetryableDatabaseError(new NullReferenceException()));
    }

    [Fact]
    public async Task Jail_OneGuardForSeveralPrisoners_AndASpawnGoesToJail()
    {
        var server = Substitute.For<IGameServer>();
        var bob = new Player { Index = 1, Name = "bob", Hash = "B" };
        var tom = new Player { Index = 2, Name = "tom", Hash = "T" };
        server.GetPlayerByName("bob").Returns(bob);
        server.GetPlayerByName("tom").Returns(tom);
        var runner = new CountingTaskRunner();
        var jail = new JailModule(server, runner, NullLogger<JailModule>.Instance, new CancellationTokenSource());

        await jail.HandleAsync(new JailPlayerCommand { Name = "bob" });
        await jail.HandleAsync(new JailPlayerCommand { Name = "tom" });
        Assert.Equal(1, runner.Started);

        // Leaving and rejoining no longer frees a prisoner: there is no handler that would
        Assert.False(jail is IHandleEventAsync<PlayerLeftEvent>);
        await jail.HandleAsync(new PlayerSpawnEvent(bob, new Position(0, 0, 0), new Rotation(0, 0, 0), DateTimeOffset.UtcNow));
        server.GameWriter.Received(1).SendTeleport(bob, Arg.Any<Position>());

        await jail.HandleAsync(new FreePlayerCommand { Name = "bob" });
        server.GameWriter.ClearReceivedCalls();
        await jail.HandleAsync(new PlayerSpawnEvent(bob, new Position(0, 0, 0), new Rotation(0, 0, 0), DateTimeOffset.UtcNow));
        server.GameWriter.DidNotReceive().SendTeleport(bob, Arg.Any<Position>());
    }

    private sealed class CountingTaskRunner : ITaskRunner
    {
        public int Started;

        public void RunBackgroundTask(string description, Func<Task> func, CancellationToken ct) => Started++;
    }
}
