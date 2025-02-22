using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using BF2WebAdmin.Server;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Modules.BF2;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Polly.Registry;
using Xunit;
using Xunit.Abstractions;
using Match = BF2WebAdmin.Server.Modules.BF2.Match;

namespace BF2WebAdmin.Tests;

public class Heli2v2ModuleTests
{
    private Heli2v2Module _cut;

    private DateTimeOffset _startTime;
    private IGameServer _server;
    private ITimeProvider _timeProvider;
    private IDelayProvider _delayProvider;
    private IMatchRepository _matchRepository;
    private FakeTaskRunner _taskRunner;

    public Heli2v2ModuleTests(ITestOutputHelper testOutputHelper)
    {
        _server = Substitute.For<IGameServer>();
        _matchRepository = Substitute.For<IMatchRepository>();
        _timeProvider = Substitute.For<ITimeProvider>();
        _startTime = new DateTimeOffset(new DateTime(2023, 5, 24, 21, 0, 0, 0), TimeSpan.Zero);
        _delayProvider = Substitute.For<IDelayProvider>();

        _server.GameWriter.SendText(Arg.Do<string>(s => testOutputHelper.WriteLine("SendText: " + s)));
        // _server.GameWriter.SendText(Arg.Do<string>(s => Console.WriteLine("SendText: " + s)));
        _timeProvider.UtcNow.Returns(_startTime.DateTime);
        _timeProvider.OffsetUtcNow.Returns(_startTime);
        _server.Map.Returns(new Map { Index = 0, Size = 16, Name = "dalian_2_v_2" });
        _server.Id.Returns("Test server");

        _taskRunner = new FakeTaskRunner();
        _cut = new Heli2v2Module(_server, _matchRepository, _delayProvider, _timeProvider, _taskRunner, Substitute.For<IReadOnlyPolicyRegistry<string>>(), Substitute.For<ILogger<Heli2v2Module>>(), new CancellationTokenSource());
    }

    [Fact]
    public async Task HandleChatMessageEvent_RdyGoWithValidPlayers_StartsMatchAndRound()
    {
        // TODO: test variants: player missing, vehicle missing, wrong chat message, match resume, guid generator interface?
        var (teams, vehicles, players) = Data.Setup2v2Players();
        var readyMessage = new Message { Channel = ChatChannel.Global, Text = "rdy", Type = MessageType.Player, Player = players[1] };
        var goMessage = new Message { Channel = ChatChannel.Global, Text = "go", Type = MessageType.Player, Player = players[3] };

        await _cut.HandleAsync(new ChatMessageEvent(readyMessage, _startTime.AddSeconds(1)));
        await _cut.HandleAsync(new ChatMessageEvent(goMessage, _startTime.AddSeconds(2)));
        await _taskRunner.FinishAsync();

        await _server.ModManager.Mediator.Received(1).PublishAsync(Arg.Is<MatchStartEvent>(e => e.Match.TeamHashes[0] == "AB" && e.Match.TeamHashes[1] == "CD"));
        await _server.ModManager.Mediator.Received(1).PublishAsync(Arg.Is<RoundStartEvent>(e => e.Round.RoundStart != null && e.Round.RoundEnd == null));
    }

    [Fact]
    public async Task HandleChatMessageEvent_RdyGoWithPreviousMatch_ResumesMatch()
    {
        // TODO: test variants: player missing, vehicle missing, wrong chat message, match resume, guid generator interface?
        var (teams, vehicles, players) = Data.Setup2v2Players();
        var readyMessage = new Message { Channel = ChatChannel.Global, Text = "rdy", Type = MessageType.Player, Player = players[1] };
        var goMessage = new Message { Channel = ChatChannel.Global, Text = "go", Type = MessageType.Player, Player = players[3] };
        var previousMatch = new BF2WebAdmin.Data.Entities.Match
        {
            Id = Guid.Parse("5ca17380-3f24-48f0-8210-f48b540e7166"),
            MatchStart = _startTime.DateTime,
            MatchEnd = null,
            Map = _server.Map?.Name,
            ServerId = _server.Id,
            TeamAHash = "AB",
            TeamBHash = "CD",
            MatchRounds = new List<MatchRound>
            {
                new()
                {
                    RoundStart = _startTime.DateTime,
                    RoundEnd = _startTime.DateTime.AddMinutes(9),
                    WinningTeamId = players[0].Team.Id,
                    MatchRoundPlayers = players.Select(p => new MatchRoundPlayer
                    {
                        PlayerHash = p.Hash,
                        TeamId = p.Team.Id,
                    }).ToList()
                }
            }
        };
        _matchRepository.GetMatchesByNewestAsync(0, 1).Returns(new[] { previousMatch });
        _matchRepository.GetMatchAsync(previousMatch.Id).Returns(previousMatch);
        _server.GetPlayerByHash(players[0].Hash).Returns(players[0]);
        _server.GetPlayerByHash(players[1].Hash).Returns(players[1]);
        _server.GetPlayerByHash(players[2].Hash).Returns(players[2]);
        _server.GetPlayerByHash(players[3].Hash).Returns(players[3]);

        await _cut.HandleAsync(new ChatMessageEvent(readyMessage, _startTime.AddSeconds(1)));
        await _cut.HandleAsync(new ChatMessageEvent(goMessage, _startTime.AddSeconds(2)));
        await _taskRunner.FinishAsync();

        await _server.ModManager.Mediator.DidNotReceiveWithAnyArgs().PublishAsync((MatchStartEvent)default);
        await _server.ModManager.Mediator.DidNotReceiveWithAnyArgs().PublishAsync((MatchEndEvent)default);
        await _server.ModManager.Mediator.Received(1).PublishAsync(Arg.Is<RoundStartEvent>(e => e.Round.MatchId == previousMatch.Id));
    }


    [Fact]
    public async Task HandleChatMessageEvent_RdyGoWithNewPlayer_EndsPreviousMatch()
    {
        var (teams, vehicles, players) = Data.Setup2v2Players();
        var readyMessage = new Message { Channel = ChatChannel.Global, Text = "rdy", Type = MessageType.Player, Player = players[1] };
        var goMessage = new Message { Channel = ChatChannel.Global, Text = "go", Type = MessageType.Player, Player = players[3] };

        await _cut.HandleAsync(new ChatMessageEvent(readyMessage, _startTime.AddSeconds(1)));
        await _cut.HandleAsync(new ChatMessageEvent(goMessage, _startTime.AddSeconds(2)));
        await _cut.HandleAsync(new PlayerKillEvent(players[1], players[1].Position, players[2], players[2].Position, "agm114_hellfire_tv", _startTime.AddSeconds(30)));
        await _cut.HandleAsync(new PlayerKillEvent(players[1], players[1].Position, players[3], players[3].Position, "agm114_hellfire_tv", _startTime.AddSeconds(30)));
        await _taskRunner.FinishAsync();
        vehicles[0].Players = new List<Player>
        {
            players[0],
            new() { Id = 5, Index = 5, Hash = "B2", Team = teams[0], Vehicle = vehicles[0], Name = "CH Gunner 2", SubVehicleTemplate = "ahe_z10_cogunner", Position = Data.DalianChPad, Rotation = Rotation.Neutral },
        };
        await _cut.HandleAsync(new ChatMessageEvent(readyMessage, _startTime.AddSeconds(35)));
        await _cut.HandleAsync(new ChatMessageEvent(goMessage, _startTime.AddSeconds(36)));

        await _server.ModManager.Mediator.Received(1).PublishAsync(Arg.Is<MatchStartEvent>(e => e.Match.TeamHashes[0] == "AB" && e.Match.TeamHashes[1] == "CD"));
        await _server.ModManager.Mediator.Received(1).PublishAsync(Arg.Is<RoundStartEvent>(e => e.Round.RoundStart != null && e.Round.RoundEnd != null)); // first round has ended, round is modified after the event is sent, so it's not what we would see at run time 
        await _server.ModManager.Mediator.Received(1).PublishAsync(Arg.Any<RoundEndEvent>());
        await _server.ModManager.Mediator.Received(1).PublishAsync(Arg.Is<MatchEndEvent>(e => e.Match.TeamHashes[0] == "AB" && e.Match.TeamHashes[1] == "CD"));
        await _server.ModManager.Mediator.Received(1).PublishAsync(Arg.Is<MatchStartEvent>(e => e.Match.TeamHashes[0] == "AB2" && e.Match.TeamHashes[1] == "CD"));
        await _server.ModManager.Mediator.Received(1).PublishAsync(Arg.Is<RoundStartEvent>(e => e.Round.RoundStart != null && e.Round.RoundEnd == null));
        // await _server.ModManager.Mediator.Received(1).PublishAsync(Arg.Is<RoundStartEvent>(e => e.Round.RoundStart != null && e.Round.RoundEnd == null));
    }

    [Fact]
    public async Task HandlePlayerKillEvent_AllTeam2PlayersKilled_Team1Wins()
    {
        // TODO: test variants: player missing, vehicle missing, wrong chat message, match resume, guid generator interface?
        var (teams, vehicles, players) = Data.Setup2v2Players();
        var readyMessage = new Message { Channel = ChatChannel.Global, Text = "rdy", Type = MessageType.Player, Player = players[1] };
        var goMessage = new Message { Channel = ChatChannel.Global, Text = "go", Type = MessageType.Player, Player = players[3] };

        await _cut.HandleAsync(new ChatMessageEvent(readyMessage, _startTime.AddSeconds(1)));
        await _cut.HandleAsync(new ChatMessageEvent(goMessage, _startTime.AddSeconds(2)));
        await _cut.HandleAsync(new PlayerKillEvent(players[1], players[1].Position, players[2], players[2].Position, "agm114_hellfire_tv", _startTime.AddSeconds(30)));
        await _cut.HandleAsync(new PlayerKillEvent(players[1], players[1].Position, players[3], players[3].Position, "agm114_hellfire_tv", _startTime.AddSeconds(30)));
        await _taskRunner.FinishAsync();

        await _delayProvider.Received(1).DelayAsync(2000, Arg.Any<CancellationToken>());
        await _server.ModManager.Mediator.Received(1)
            .PublishAsync(Arg.Is<RoundEndEvent>(e =>
                e.Round.RoundEnd != null &&
                e.Round.WinningTeamId == players[1].Team.Id &&
                e.Round.WinningTeamHash == "AB" &&
                e.Round.LosingTeamHash == "CD"
            ));
    }

    [Fact]
    public async Task HandlePlayerKillEvent_AllPlayersDeadWithinLimit_ClassicDraw()
    {
        // TODO: test variants: player missing, vehicle missing, wrong chat message, match resume, guid generator interface?
        var (teams, vehicles, players) = Data.Setup2v2Players();
        var readyMessage = new Message { Channel = ChatChannel.Global, Text = "rdy", Type = MessageType.Player, Player = players[1] };
        var goMessage = new Message { Channel = ChatChannel.Global, Text = "go", Type = MessageType.Player, Player = players[3] };

        await _cut.HandleAsync(new ChatMessageEvent(readyMessage, _startTime.AddSeconds(1)));
        await _cut.HandleAsync(new ChatMessageEvent(goMessage, _startTime.AddSeconds(2)));
        await _cut.HandleAsync(new PlayerKillEvent(players[1], players[1].Position, players[2], players[2].Position, "agm114_hellfire_tv", _startTime.AddSeconds(30)));
        await _cut.HandleAsync(new PlayerKillEvent(players[1], players[1].Position, players[3], players[3].Position, "agm114_hellfire_tv", _startTime.AddSeconds(30)));
        await _cut.HandleAsync(new PlayerKillEvent(players[3], players[3].Position, players[0], players[0].Position, "agm114_hellfire_tv", _startTime.AddSeconds(31)));
        await _cut.HandleAsync(new PlayerKillEvent(players[3], players[3].Position, players[1], players[1].Position, "agm114_hellfire_tv", _startTime.AddSeconds(31)));
        await _taskRunner.FinishAsync();

        await _delayProvider.Received(1).DelayAsync(2000, Arg.Any<CancellationToken>());
        await _server.ModManager.Mediator.Received(1)
            .PublishAsync(Arg.Is<RoundEndEvent>(e =>
                e.Round.RoundEnd != null &&
                e.Round.WinningTeamId == -1 &&
                e.Round.WinningTeamHash == null &&
                e.Round.LosingTeamHash == null
            ));
    }

    // tests
    // ends previous match

    private class FakeTaskRunner : ITaskRunner
    {
        private readonly List<Func<Task>> _tasks = new();

        public void RunBackgroundTask(string description, Func<Task> func, CancellationToken ct) => _tasks.Add(func);

        public async Task FinishAsync()
        {
            await Task.WhenAll(_tasks.Select(t => t.Invoke()));
            _tasks.Clear();
        }
    }
}