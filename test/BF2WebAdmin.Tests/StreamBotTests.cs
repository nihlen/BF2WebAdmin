using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Api;
using BF2WebAdmin.Server.StreamBot;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>
/// The stream bot's hub on a test server, with both keys set up as Program.cs does: the bot gets start and stop
/// requests, its reports reach the server's modules, and each key opens only its own door.
/// </summary>
public class StreamBotTests
{
    private const string AdminKey = "admin-key-0123456789";
    private const string BotKey = "stream-bot-key-0123456789";
    private const string ServerId = "198.51.100.20:16567";

    [Fact]
    public async Task TheBotGetsRequestsAndItsReportsReachTheModules()
    {
        await using var host = await TestHost.StartAsync(BotKey);
        var service = host.Services.GetRequiredService<StreamBotService>();
        Assert.False(await service.StartGameStreamAsync(host.GameServer, null));

        var started = new TaskCompletionSource<StartStreamRequest>();
        var stopped = new TaskCompletionSource<StopStreamRequest>();
        await using var bot = host.Connect(BotKey);
        bot.On<StartStreamRequest>(nameof(IStreamBotClient.StartStream), r => started.TrySetResult(r));
        bot.On<StopStreamRequest>(nameof(IStreamBotClient.StopStream), r => stopped.TrySetResult(r));
        await bot.StartAsync();
        await WaitUntil(() => service.IsBotConnected);

        var matchId = Guid.NewGuid();
        Assert.True(await service.StartGameStreamAsync(host.GameServer, matchId));
        var request = await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal((ServerId, "198.51.100.20", 16567, 29900, matchId), (request.ServerId, request.GameServerIp, request.GameServerPort, request.GameQueryPort, request.MatchId));
        // The server as WebAdmin sees it, for the bot's checks: no bots among the players
        Assert.Equal(("dalian_2_v_2", 16), (request.State.Map, request.State.MaxPlayers));
        Assert.Equal(new[] { "A1", "B1" }, request.State.HumanPlayers);

        // What the bot asks while it streams
        var state = await bot.InvokeAsync<StreamServerState?>("GetServerState", ServerId);
        Assert.Equal(new[] { "A1", "B1" }, state!.HumanPlayers);
        Assert.Null(await bot.InvokeAsync<StreamServerState?>("GetServerState", "203.0.113.1:16567"));

        await bot.InvokeAsync("StreamStarted", new StreamStartedReport(ServerId, matchId, "netskyTV", "https://twitch.tv/netsky"));
        await host.Mediator.Received(1).PublishAsync(Arg.Is<GameStreamStartedEvent>(e =>
            e.MatchId == matchId && e.BotName == "netskyTV" && e.StreamUrl == "https://twitch.tv/netsky"));

        Assert.True(await service.StopGameStreamAsync(host.GameServer, matchId));
        Assert.Equal(ServerId, (await stopped.Task.WaitAsync(TimeSpan.FromSeconds(10))).ServerId);
        await bot.InvokeAsync("StreamStopped", new StreamStoppedReport(ServerId, matchId));
        await host.Mediator.Received(1).PublishAsync(Arg.Is<GameStreamStoppedEvent>(e => e.MatchId == matchId));

        // A report on a server that is not connected goes nowhere
        await bot.InvokeAsync("StreamStopped", new StreamStoppedReport("203.0.113.1:16567", null));
        await host.Mediator.Received(1).PublishAsync(Arg.Any<GameStreamStoppedEvent>());

        await bot.StopAsync();
        await WaitUntil(() => !service.IsBotConnected);
        Assert.False(await service.StopGameStreamAsync(host.GameServer, null));
    }

    [Fact]
    public async Task EachKeyOpensOnlyItsOwnDoor()
    {
        await using var host = await TestHost.StartAsync(BotKey);
        Assert.Equal(HttpStatusCode.OK, await host.GetAdminAsync(AdminKey));
        Assert.Equal(HttpStatusCode.Unauthorized, await host.GetAdminAsync(BotKey));

        await using var admin = host.Connect(AdminKey);
        await Assert.ThrowsAsync<HttpRequestException>(() => admin.StartAsync());
        await using var wrong = host.Connect("not-the-key");
        await Assert.ThrowsAsync<HttpRequestException>(() => wrong.StartAsync());
        await using var bot = host.Connect(BotKey);
        await bot.StartAsync();
    }

    [Fact]
    public async Task WithoutAStreamBotKeyNoBotConnects()
    {
        await using var host = await TestHost.StartAsync(streamBotKey: null);
        await using var empty = host.Connect("");
        await Assert.ThrowsAsync<HttpRequestException>(() => empty.StartAsync());
        Assert.False(StreamBotAuthentication.KeyMatches(null, ""));
        Assert.False(StreamBotAuthentication.KeyMatches("", ""));
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(20);
        Assert.True(condition());
    }

    private sealed class TestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private TestHost(WebApplication app, IMediator mediator, IGameServer gameServer)
        {
            _app = app;
            Mediator = mediator;
            GameServer = gameServer;
        }

        public IGameServer GameServer { get; }

        public IServiceProvider Services => _app.Services;
        public IMediator Mediator { get; }
        private TestServer Server => _app.GetTestServer();

        public static async Task<TestHost> StartAsync(string? streamBotKey)
        {
            var mediator = Substitute.For<IMediator>();
            var gameServer = Substitute.For<IGameServer>();
            gameServer.ModManager.Mediator.Returns(mediator);
            gameServer.Id.Returns(ServerId);
            gameServer.IpAddress.Returns(IPAddress.Parse("198.51.100.20"));
            gameServer.GamePort.Returns(16567);
            gameServer.QueryPort.Returns(29900);
            gameServer.Map.Returns(new Map { Name = "dalian_2_v_2" });
            gameServer.MaxPlayers.Returns(16);
            gameServer.Players.Returns(new[] { new Player { Name = "A1" }, new Player { Name = "bot", IsBot = true }, new Player { Name = "B1" } });
            var registry = Substitute.For<IGameServerRegistry>();
            registry.GetGameServer(Arg.Any<string>()).Returns((IGameServer?)null);
            registry.GetGameServer(ServerId).Returns(gameServer);

            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddSignalR();
            builder.Services.AddSingleton(registry);
            builder.Services.AddSingleton<StreamBotService>();
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie()
                .AddAdminApiKey(AdminKey)
                .AddStreamBotKey(streamBotKey);
            builder.Services.AddAuthorization(options =>
            {
                options.AddAdminPolicy();
                options.AddStreamBotPolicy();
            });

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapGet("/admin", () => "ok").RequireAuthorization(ApiEndpoints.AdminPolicy);
            app.MapHub<StreamBotHub>(StreamBotHub.Path);
            await app.StartAsync();
            return new TestHost(app, mediator, gameServer);
        }

        public HubConnection Connect(string key) => new HubConnectionBuilder()
            .WithUrl(new Uri(Server.BaseAddress, StreamBotHub.Path), options =>
            {
                options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.Headers["X-API-KEY"] = key;
            })
            .Build();

        public async Task<HttpStatusCode> GetAdminAsync(string key)
        {
            using var client = Server.CreateClient();
            client.DefaultRequestHeaders.Add("X-API-KEY", key);
            return (await client.GetAsync("/admin")).StatusCode;
        }

        public async ValueTask DisposeAsync() => await _app.DisposeAsync();
    }
}
