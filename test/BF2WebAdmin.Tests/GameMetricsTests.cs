using System;
using System.Collections.Generic;
using System.Linq;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Communication;
using BF2WebAdmin.Server.Metrics;
using NSubstitute;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>The game servers' activity (<see cref="GameMetrics"/>) and the admin channel's close reasons as metrics.</summary>
public class GameMetricsTests
{
    private static IGameServer Server(string id, string name = "2v2 metrics")
    {
        var server = Substitute.For<IGameServer>();
        server.Id.Returns(id);
        server.Name.Returns(name);
        server.SocketState.Returns(SocketState.Connected);
        return server;
    }

    private static MetricsRecorder Recorder(string serverId) =>
        new(m => Equals(m.Tags.GetValueOrDefault(GameMetrics.ServerIdTag), serverId));

    [Fact]
    public void GameEventsAreCountedWithTheServer()
    {
        const string id = "127.0.0.1:17001";
        using var metrics = Recorder(id);
        var server = Server(id);

        GameMetrics.Kill(server, "kill", "ahe_ah1z_hellfirelauncher");
        GameMetrics.Kill(server, "suicide", null);
        GameMetrics.Join(server, "se");
        GameMetrics.Join(server, null);
        GameMetrics.ChatMessage(server, "Global");
        GameMetrics.TvMissile(server, "hit");
        GameMetrics.Heli2v2Round(server, "dalian_2_v_2", draw: false, TimeSpan.FromSeconds(75));
        GameMetrics.Heli2v2Round(server, "dalian_2_v_2", draw: true, TimeSpan.FromSeconds(300));

        Assert.Equal(1, metrics.Sum("bf2.kills", ("kind", "kill"), ("weapon", "ahe_ah1z_hellfirelauncher")));
        Assert.Equal(1, metrics.Sum("bf2.kills", ("kind", "suicide"), ("weapon", "")));
        Assert.Equal(1, metrics.Sum("bf2.player.joins", ("country", "SE")));
        Assert.Equal(1, metrics.Sum("bf2.player.joins", ("country", "unknown")));
        Assert.Equal(1, metrics.Sum("bf2.chat.messages", ("channel", "Global")));
        Assert.Equal(1, metrics.Sum("bf2.tv_missiles", ("outcome", "hit")));
        Assert.Equal(1, metrics.Sum("bf2.heli2v2.rounds", ("map", "dalian_2_v_2"), ("result", "win")));
        Assert.Equal(1, metrics.Sum("bf2.heli2v2.rounds", ("result", "draw")));
        Assert.Equal([75.0, 300.0], metrics.All("bf2.heli2v2.round.duration").Select(m => m.Value).Order());
        Assert.All(metrics.All("bf2.kills"), m => Assert.Equal("2v2 metrics", m.Tags[GameMetrics.ServerNameTag]));
    }

    [Fact]
    public void ThePlayersAreCountedAsHumansAndBots()
    {
        const string id = "127.0.0.1:17003";
        using var metrics = Recorder(id);
        var server = Server(id);
        server.Players.Returns([new Player { Name = "pilot" }, new Player { Name = "gunner" }, new Player { Name = "bot", IsBot = true }]);
        GameMetrics.ObserveServers(() => [server]);

        var observed = metrics.Observe();
        Assert.Equal(2, observed.Single(m => m.Name == "bf2.players" && Equals(m.Tags["kind"], "human")).Value);
        Assert.Equal(1, observed.Single(m => m.Name == "bf2.players" && Equals(m.Tags["kind"], "bot")).Value);

        // A server that is not connected has no players to count
        server.SocketState.Returns(SocketState.Disconnected);
        Assert.DoesNotContain(metrics.Observe(), m => m.Name == "bf2.players");
    }

    [Theory]
    [InlineData("replaced by a new link from the server", "replaced")]
    [InlineData("nothing received for 46 s", "timeout")]
    [InlineData("the module closed the connection", "closed_by_module")]
    [InlineData("read failed: Connection reset by peer", "io_error")]
    [InlineData("write failed: Broken pipe", "io_error")]
    [InlineData("protocol error UnknownType: x", "protocol_error")]
    [InlineData("server settings changed", "settings_changed")]
    [InlineData("read loop ended (OperationCanceledException)", "other")]
    [InlineData(null, "other")]
    public void ACloseReasonIsOneOfAFewWords(string? reason, string kind) => Assert.Equal(kind, AdminChannelMetrics.CloseKind(reason));
}
