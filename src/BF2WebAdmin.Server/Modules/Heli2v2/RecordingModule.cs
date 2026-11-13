using System.Diagnostics;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Commands.BF2;

namespace BF2WebAdmin.Server.Modules.Heli2v2;

/// <summary>
/// Records a player's flight (.record) and plays it back on them (.playback, .loop) until .stop. Work in progress.
/// Loaded with <see cref="Heli2v2Module"/>.
/// </summary>
[LoadedWith(typeof(Heli2v2Module))]
public class RecordingModule : BaseModule,
    IHandleEventAsync<PlayerPositionEvent>,
    IHandleCommand<RecordCommand>,
    IHandleCommand<StopCommand>,
    IHandleCommandAsync<PlaybackCommand>,
    IHandleCommandAsync<LoopCommand>
{
    private Player _playerRecordingTarget;
    private Stopwatch _playerRecordingWatch;
    private IList<(long, Position, Rotation)>? _playerRecordingPositions;
    private CancellationTokenSource _playbackCancellation;

    public RecordingModule(IGameServer server, ILogger<RecordingModule> logger, CancellationTokenSource cts) : base(server, logger, cts)
    {
    }

    public ValueTask HandleAsync(PlayerPositionEvent e)
    {
        if (_playerRecordingWatch != null && _playerRecordingWatch.IsRunning)
        {
            Logger.LogInformation("Recorded player {Position} {Rotation}", e.Position, e.Rotation);
            _playerRecordingPositions.Add((_playerRecordingWatch.ElapsedMilliseconds, e.Position, e.Rotation));
        }

        return ValueTask.CompletedTask;
    }

    public void Handle(RecordCommand command)
    {
        if (_playerRecordingWatch is { IsRunning: true })
            StopRecording();
        else
            StartRecording(command);
    }

    public void Handle(StopCommand command)
    {
        StopRecording();
        _playbackCancellation?.Cancel();
    }

    private void StartRecording(RecordCommand command)
    {
        _playerRecordingPositions = new List<(long, Position, Rotation)>(1000);
        _playerRecordingTarget = command.Message.Player;
        _playerRecordingWatch = Stopwatch.StartNew();
        GameServer.GameWriter.SendText("Recording started");
    }

    private void StopRecording()
    {
        _playerRecordingWatch?.Stop();
        GameServer.GameWriter.SendText("Recording stopped");
    }

    public async ValueTask HandleAsync(PlaybackCommand command)
    {
        if (_playerRecordingPositions == null)
        {
            GameServer.GameWriter.SendText("No recording found");
            return;
        }

        _playbackCancellation?.Cancel();
        _playbackCancellation = new CancellationTokenSource();
        // A busy loop: on the thread pool, not the module thread
        await Task.Run(() =>
        {
            GameServer.GameWriter.SendText("Started playback");

            StartPlayback();

            GameServer.GameWriter.SendText("Stopped playback");
        }, _playbackCancellation.Token);
    }

    public async ValueTask HandleAsync(LoopCommand command)
    {
        if (_playerRecordingPositions == null)
        {
            GameServer.GameWriter.SendText("No recording found");
            return;
        }

        _playbackCancellation?.Cancel();
        _playbackCancellation = new CancellationTokenSource();
        await Task.Run(() =>
        {
            GameServer.GameWriter.SendText("Started looping playback");

            while (!_playbackCancellation.IsCancellationRequested)
            {
                StartPlayback();
            }

            GameServer.GameWriter.SendText("Stopped looping playback");
        }, _playbackCancellation.Token);
    }

    private void StartPlayback()
    {
        // TODO: get new timestamps from events
        var sw = Stopwatch.StartNew();
        foreach (var snapshot in _playerRecordingPositions)
        {
            if (ModuleCancellationToken.IsCancellationRequested)
                return;

            SpinWait.SpinUntil(() => sw.ElapsedMilliseconds > snapshot.Item1, 1000);
            Logger.LogError("Variance: {VarianceMs}", sw.ElapsedMilliseconds - snapshot.Item1);
            GameServer.GameWriter.SendTeleport(_playerRecordingTarget, snapshot.Item2, snapshot.Item3);
            if (_playbackCancellation.IsCancellationRequested)
                break;
        }
    }
}
