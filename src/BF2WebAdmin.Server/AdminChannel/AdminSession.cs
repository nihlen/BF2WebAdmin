using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Threading.Channels;

namespace BF2WebAdmin.Server.AdminChannel;

/// <summary>Receives a session's state messages and events, one at a time, in order.</summary>
public interface IAdminSessionHandler
{
    ValueTask OnMessageAsync(AdminSession session, AdminMessage message);
    ValueTask OnClosedAsync(AdminSession session);
}

/// <summary>A command the module refused (an <c>Error</c> reply), or a link that went away before the reply.</summary>
public class AdminCommandException(string message, AdminErrorCode? code = null) : Exception(message)
{
    public AdminErrorCode? Code { get; } = code;
}

/// <summary>
/// One authenticated admin channel link (after <see cref="AdminHandshake"/>).
/// - The read loop answers <c>Ping</c>, completes pending requests and queues everything else.
/// - The apply loop hands state and events to the handler in order. It checks event sequence
///   numbers and sends <c>RequestSnapshot</c> on a gap. Stream frames are latest-wins.
/// - The write loop sends queued frames in order; commands therefore reach the module in call order.
/// - A keepalive sends <c>Ping</c> every 15 s and closes the link after 45 s without receiving anything.
/// </summary>
public sealed class AdminSession
{
    public static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(45);
    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(10);

    private readonly Stream _stream;
    private readonly AdminFrameDecoder _decoder;
    private readonly IAdminSessionHandler _handler;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts;
    private readonly Channel<byte[]> _outbound = Channel.CreateUnbounded<byte[]>(); // Count needs the multi-reader channel
    private readonly Channel<AdminMessage?> _inbound = Channel.CreateUnbounded<AdminMessage?>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly ConcurrentDictionary<uint, TaskCompletionSource<AdminMessage>> _pending = new();
    private readonly AdminMessage?[] _latestStreams = new AdminMessage?[5];
    private readonly object _recordingLock = new();
    private StreamWriter? _recording;
    private int _nextRequestId;
    private long _lastReceived = Stopwatch.GetTimestamp();
    private uint? _expectedSeq;
    private uint _pendingSnapshotRequest;
    private string? _closeReason;
    private volatile bool _errorQueued;

    public string ServerId { get; }
    public IPEndPoint RemoteEndPoint { get; }
    public uint SessionNumber { get; private set; }
    public byte Capabilities { get; private set; }
    public int SequenceGaps { get; private set; }
    public DateTimeOffset ConnectedAt { get; } = DateTimeOffset.UtcNow;

    public AdminSession(string serverId, IPEndPoint remoteEndPoint, Stream stream, AdminFrameDecoder decoder, IAdminSessionHandler handler, ILogger logger,
        CancellationToken cancellationToken)
    {
        ServerId = serverId;
        RemoteEndPoint = remoteEndPoint;
        _stream = stream;
        _decoder = decoder;
        _handler = handler;
        _logger = logger;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    }

    public bool IsOpen => !_cts.IsCancellationRequested;
    public string? CloseReason => _closeReason;

    public uint NextRequestId()
    {
        // Request id 0 is the Snapshot that follows Welcome and connection-level errors
        var id = (uint)Interlocked.Increment(ref _nextRequestId);
        return id == 0 ? (uint)Interlocked.Increment(ref _nextRequestId) : id;
    }

    /// <summary>Completes when <see cref="RunAsync"/> has ended: the loops have stopped and the close handler has run.</summary>
    public Task Closed => _closed.Task;

    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Runs the link until it closes. Never throws.</summary>
    public async Task RunAsync()
    {
        try
        {
            await RunLoopsAsync();
        }
        finally
        {
            _closed.TrySetResult();
        }
    }

    private async Task RunLoopsAsync()
    {
        var token = _cts.Token;
        var writeTask = Task.Run(() => WriteLoopAsync(token), CancellationToken.None);
        var tasks = new[]
        {
            Task.Run(() => ReadLoopAsync(token), CancellationToken.None),
            writeTask,
            Task.Run(() => ApplyLoopAsync(token), CancellationToken.None),
            Task.Run(() => KeepaliveLoopAsync(token), CancellationToken.None),
        };

        await Task.WhenAny(tasks);
        if (_errorQueued)
        {
            // Let the write loop send the queued Error before the link closes
            _outbound.Writer.TryComplete();
            await Task.WhenAny(writeTask, Task.Delay(TimeSpan.FromSeconds(2)));
        }

        Close(_closeReason ?? "link closed");
        try
        {
            await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException or IOException or ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin channel {ServerId}: session task failed", ServerId);
        }

        foreach (var (_, tcs) in _pending)
            tcs.TrySetException(new AdminCommandException($"the admin channel link closed ({_closeReason})"));
        _pending.Clear();

        StopRecording();
        await _stream.DisposeAsync();

        try
        {
            await _handler.OnClosedAsync(this);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin channel {ServerId}: close handler failed", ServerId);
        }

        _logger.LogInformation("Admin channel {ServerId}: session {Session} from {Remote} closed: {Reason}", ServerId, SessionNumber, RemoteEndPoint, _closeReason);
    }

    public void Close(string reason)
    {
        _closeReason ??= reason;
        if (!_cts.IsCancellationRequested)
            _cts.Cancel();
    }

    // ---------------------------------------------------------------- sending

    /// <summary>Queues a frame without waiting for a reply.</summary>
    public void Post(AdminMessage message)
    {
        if (!_outbound.Writer.TryWrite(AdminProtocol.Encode(message)))
            _logger.LogDebug("Admin channel {ServerId}: dropped {Type}, the link is closed", ServerId, message.Type);
    }

    /// <summary>Sends a command and waits for its reply. An <c>Error</c> reply throws <see cref="AdminCommandException"/>.</summary>
    public async Task<AdminMessage> RequestAsync(Func<uint, AdminMessage> create, TimeSpan? timeout = null)
    {
        var requestId = NextRequestId();
        var message = create(requestId);
        var tcs = new TaskCompletionSource<AdminMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[requestId] = tcs;
        if (!IsOpen)
            throw new AdminCommandException("the admin channel link is closed");

        Post(message);
        try
        {
            var reply = await tcs.Task.WaitAsync(timeout ?? DefaultRequestTimeout, _cts.Token);
            if (reply is ErrorMessage error)
                throw new AdminCommandException($"{message.Type} refused: {error.Code} {error.Message}", error.Code);

            return reply;
        }
        catch (TimeoutException)
        {
            throw new AdminCommandException($"{message.Type} {requestId}: no reply");
        }
        catch (OperationCanceledException)
        {
            throw new AdminCommandException($"{message.Type} {requestId}: the admin channel link closed ({_closeReason})");
        }
        finally
        {
            _pending.TryRemove(requestId, out _);
        }
    }

    /// <summary>Sends a command whose reply is only logged when it is an error.</summary>
    public void Fire(Func<uint, AdminMessage> create)
    {
        var requestId = NextRequestId();
        var message = create(requestId);
        var tcs = new TaskCompletionSource<AdminMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[requestId] = tcs;
        Post(message);
        _ = tcs.Task.WaitAsync(DefaultRequestTimeout * 3).ContinueWith(t =>
        {
            _pending.TryRemove(requestId, out _);
            if (t.IsCompletedSuccessfully && t.Result is ErrorMessage error)
                _logger.LogWarning("Admin channel {ServerId}: {Command} refused: {Code} {Message}", ServerId, message, error.Code, error.Message);
            else if (t.Exception?.InnerException is TimeoutException)
                _logger.LogWarning("Admin channel {ServerId}: no reply to {Type} {RequestId}", ServerId, message.Type, requestId);
        }, TaskScheduler.Default);
    }

    public void RequestSnapshot()
    {
        if (_pendingSnapshotRequest != 0)
            return;

        _pendingSnapshotRequest = NextRequestId();
        Post(new RequestSnapshot(_pendingSnapshotRequest));
    }

    private async Task WriteLoopAsync(CancellationToken token)
    {
        try
        {
            await foreach (var frame in _outbound.Reader.ReadAllAsync(token))
            {
                await _stream.WriteAsync(frame, token);
                if (_outbound.Reader.Count == 0)
                    await _stream.FlushAsync(token);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException or NotSupportedException)
        {
            Close(ex is IOException ? $"write failed: {ex.Message}" : $"write loop ended ({ex.GetType().Name}: {ex.Message})");
        }
        finally
        {
            _outbound.Writer.TryComplete();
        }
    }

    private async Task KeepaliveLoopAsync(CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            var lastPing = Stopwatch.GetTimestamp();
            while (await timer.WaitForNextTickAsync(token))
            {
                var idle = Stopwatch.GetElapsedTime(Interlocked.Read(ref _lastReceived));
                if (idle > ReceiveTimeout)
                {
                    Close($"nothing received for {idle.TotalSeconds:0} s");
                    return;
                }

                if (Stopwatch.GetElapsedTime(lastPing) >= PingInterval)
                {
                    lastPing = Stopwatch.GetTimestamp();
                    Fire(id => new Ping(id, (ulong)Environment.TickCount64));
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    // ---------------------------------------------------------------- receiving

    private async Task ReadLoopAsync(CancellationToken token)
    {
        var buffer = new byte[64 * 1024];
        try
        {
            // Frames that arrived with the handshake (Welcome, Snapshot) are already in the decoder
            DrainDecoder();
            while (!token.IsCancellationRequested)
            {
                var n = await _stream.ReadAsync(buffer, token);
                if (n == 0)
                {
                    Close("the module closed the connection");
                    return;
                }

                Interlocked.Exchange(ref _lastReceived, Stopwatch.GetTimestamp());
                _decoder.Append(buffer.AsSpan(0, n));
                DrainDecoder();
            }
        }
        catch (AdminProtocolException ex)
        {
            _logger.LogWarning("Admin channel {ServerId}: protocol error {Code}: {Message}", ServerId, ex.Code, ex.Message);
            SendErrorAndClose(ex.Code, ex.Message);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
        {
            Close(ex is IOException ? $"read failed: {ex.Message}" : $"read loop ended ({ex.GetType().Name})");
        }
        finally
        {
            _inbound.Writer.TryComplete();
        }
    }

    private void SendErrorAndClose(AdminErrorCode code, string message)
    {
        // RunAsync drains the write loop (with this Error last) before it closes the link
        Post(new ErrorMessage(0, code, message));
        _closeReason ??= $"protocol error {code}: {message}";
        _errorQueued = true;
    }

    private void DrainDecoder()
    {
        while (_decoder.TryRead(out var type, out var payload))
        {
            var message = AdminProtocol.Decode(type, payload);
            Record(message);
            Dispatch(message);
        }
    }

    private void Dispatch(AdminMessage message)
    {
        switch (message)
        {
            case Ping ping:
                Post(new Pong(ping.RequestId, ping.Clock));
                break;

            case ErrorMessage error when error.RequestId == 0 || !_pending.ContainsKey(error.RequestId):
                _logger.LogWarning("Admin channel {ServerId}: the module reports error {Code} for request {RequestId}: {Message}", ServerId, error.Code,
                    error.RequestId, error.Message);
                break;

            case OkReply or ConsoleResult or ObjectList or ObjectSpawned or Pong or ErrorMessage:
                var requestId = ((IAdminRequest)message).RequestId;
                if (_pending.TryGetValue(requestId, out var tcs))
                    tcs.TrySetResult(message);
                else
                    _logger.LogDebug("Admin channel {ServerId}: reply {Type} to unknown request {RequestId}", ServerId, message.Type, requestId);
                break;

            case PlayerStream or VehicleStream or ProjectileStream or GameStream or AimStream:
                // Latest wins: only queue a marker when the slot was empty
                var slot = message.Type - AdminMessageType.PlayerStream;
                if (Interlocked.Exchange(ref _latestStreams[slot], message) == null)
                    _inbound.Writer.TryWrite(null);
                break;

            case Welcome or Snapshot or AdminEvent:
                if (message is Welcome welcome)
                {
                    SessionNumber = welcome.Session;
                    Capabilities = welcome.Capabilities;
                }

                _inbound.Writer.TryWrite(message);
                break;

            default:
                // Hello, Challenge, Auth after the handshake, or a WebAdmin -> module message
                throw new AdminProtocolException(AdminErrorCode.UnknownType, $"{message.Type} is not expected from the module here");
        }
    }

    private async Task ApplyLoopAsync(CancellationToken token)
    {
        try
        {
            await foreach (var queued in _inbound.Reader.ReadAllAsync(token))
            {
                // ReadAllAsync drains what is queued without looking at the token; a closed link applies nothing more
                // (a new link for the server may already be running)
                if (token.IsCancellationRequested)
                    break;

                if (queued == null)
                {
                    for (var i = 0; i < _latestStreams.Length; i++)
                    {
                        var stream = Interlocked.Exchange(ref _latestStreams[i], null);
                        if (stream != null)
                            await ApplyAsync(stream);
                    }

                    continue;
                }

                switch (queued)
                {
                    case Snapshot snapshot:
                        if (snapshot.RequestId != 0 && snapshot.RequestId != _pendingSnapshotRequest)
                            _logger.LogDebug("Admin channel {ServerId}: snapshot for request {RequestId}", ServerId, snapshot.RequestId);

                        if (snapshot.RequestId == _pendingSnapshotRequest)
                            _pendingSnapshotRequest = 0;

                        _expectedSeq = snapshot.LastSeq + 1;
                        await ApplyAsync(snapshot);
                        break;

                    case AdminEvent e:
                        if (_expectedSeq == null)
                        {
                            _logger.LogWarning("Admin channel {ServerId}: event {Type} {Seq} before the first snapshot, ignored", ServerId, e.Type, e.Seq);
                            break;
                        }

                        if (e.Seq < _expectedSeq)
                            break; // Already in the snapshot

                        if (e.Seq > _expectedSeq)
                        {
                            SequenceGaps++;
                            _logger.LogWarning("Admin channel {ServerId}: sequence gap, expected {Expected}, got {Seq}; requesting a snapshot", ServerId,
                                _expectedSeq, e.Seq);
                            RequestSnapshot();
                        }

                        _expectedSeq = e.Seq + 1;
                        await ApplyAsync(e);
                        break;

                    default:
                        await ApplyAsync(queued);
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async ValueTask ApplyAsync(AdminMessage message)
    {
        try
        {
            await _handler.OnMessageAsync(this, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin channel {ServerId}: failed to apply {Type}", ServerId, message.Type);
        }
    }

    // ---------------------------------------------------------------- recording

    public void StartRecording(string path)
    {
        lock (_recordingLock)
        {
            _recording?.Dispose();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            _recording = new StreamWriter(path, append: true) { AutoFlush = true };
            _recording.WriteLine($"# admin channel {ServerId} session {SessionNumber} from {RemoteEndPoint}, {DateTimeOffset.UtcNow:O}");
        }
    }

    public void StopRecording()
    {
        lock (_recordingLock)
        {
            _recording?.Dispose();
            _recording = null;
        }
    }

    private void Record(AdminMessage message)
    {
        if (_recording == null)
            return;

        lock (_recordingLock)
            _recording?.WriteLine($"{DateTimeOffset.UtcNow:O}\t{message}");
    }
}
