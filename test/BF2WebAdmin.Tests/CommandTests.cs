using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BF2WebAdmin.Server;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Configuration.Models;
using BF2WebAdmin.Server.Extensions;
using BF2WebAdmin.Server.Modules;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>Chat command parsing (<see cref="ModuleResolver.ParseCommand"/>), handler registration and <see cref="SerialTaskQueue"/>.</summary>
public class CommandTests
{
    private readonly ModuleResolver _resolver = new(NullLogger<ModuleResolver>.Instance);

    private ICommand? Parse(string line)
    {
        var parts = line.TrimStart('.').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return ModuleResolver.ParseCommand(_resolver.CommandParsers[parts[0]], parts[1..], out _);
    }

    [Fact]
    public void ParseCommand_OneCommandPerLine_TheBestFitWins()
    {
        // Before: ".sw 3" ran SwitchLater and also switched the first player with a 3 in the name
        Assert.Equal(3, Assert.IsType<SwitchLaterCommand>(Parse(".sw 3")).Rounds);
        Assert.Equal("bob", Assert.IsType<SwitchCommand>(Parse(".sw bob")).Name);
        Assert.IsType<SwitchAllCommand>(Parse(".sw"));

        // Before: ".tp bob 1 2 3" ran both teleports
        var coordinates = Assert.IsType<TeleportCoordinatesCommand>(Parse(".tp bob 1 2 3"));
        Assert.Equal(("bob", 1d, 2d, 3d), (coordinates.Name, coordinates.X, coordinates.Y, coordinates.Altitude));
        Assert.Equal("dalian pad", Assert.IsType<TeleportLocationCommand>(Parse(".tp bob dalian pad")).Location);

        Assert.Equal(3, Assert.IsType<SetAuthCommand>(Parse(".auth bob 3")).Level);
        Assert.Equal("bob", Assert.IsType<GetAuthCommand>(Parse(".auth bob")).Name);
        Assert.IsType<ScoreResetCommand>(Parse(".score reset"));
        Assert.Equal(4, Assert.IsType<ScoreCommand>(Parse(".score bob 1 2 3 4")).Deaths);
        Assert.Equal("x", Assert.IsType<SetTvMissileTypeCommand>(Parse(".tv x")).Type);
        Assert.Equal("2", Assert.IsType<SetTvMissileValueCommand>(Parse(".tv x 2")).Value);
    }

    [Fact]
    public void ParseCommand_ArgumentsThatFitNoParser_Null()
    {
        Assert.Null(Parse(".score"));
        Assert.Null(Parse(".switchid bob"));
    }

    [Fact]
    public void ParseCommand_NumbersWhateverTheCulture()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
            Assert.Equal(0.5, Assert.IsType<GravityCommand>(Parse(".g 0.5")).Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void CommandParsers_NoEmptyAlias()
    {
        // "tvlog| <Name>" made ". lol" a command
        Assert.DoesNotContain("", _resolver.CommandParsers.Keys);
        Assert.All(_resolver.CommandParsers.Keys, alias => Assert.Equal(alias.ToLowerInvariant(), alias));
    }

    [Fact]
    public void EventHandlers_OneHandlerPerModuleAndEvent()
    {
        // Each module was registered under every event type it handles once for each of its events (7 for LogModule)
        var server = Substitute.For<IGameServer>();
        server.Id.Returns("127.0.0.1:16567");
        var directory = Path.Combine(Path.GetTempPath(), "bf2wa-tests-" + Guid.NewGuid().ToString("N"));
        using var logModule = new LogModule(server, Options.Create(new LogConfig { ServerLogDirectory = directory }));
        _resolver.Modules[typeof(LogModule)] = logModule;
        _resolver.Modules[typeof(QuoteModule)] = new QuoteModule(server, Options.Create(new MashapeConfig()));

        Assert.Single(_resolver.EventHandlers[typeof(ChatMessageEvent)]);
        Assert.Single(_resolver.EventHandlers[typeof(MapChangedEvent)]);
        Assert.Empty(_resolver.EventHandlers[typeof(PlayerPositionEvent)]);
        Assert.Single(_resolver.CommandHandlers[typeof(QuoteCommand)]);
        Assert.Single(_resolver.CommandHandlers[typeof(QuoteCategoryCommand)]);
        Assert.True(_resolver.IsHandled(typeof(QuoteCommand)));
        Assert.False(_resolver.IsHandled(typeof(SwitchCommand)));
    }

    [Fact]
    public async Task SerialTaskQueue_RunsInOrderAndPastFailures()
    {
        var queue = new SerialTaskQueue(NullLogger.Instance);
        var done = new List<int>();
        var slow = new TaskCompletionSource();

        _ = queue.Enqueue("first", async () =>
        {
            await slow.Task;
            lock (done) done.Add(1);
        });
        _ = queue.Enqueue("fails", () => throw new InvalidOperationException("test"));
        var last = queue.Enqueue("third", () =>
        {
            lock (done) done.Add(3);
            return Task.CompletedTask;
        });

        await Task.Delay(50);
        Assert.Empty(done);

        slow.SetResult();
        await last.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal([1, 3], done);
    }
}
