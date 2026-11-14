using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Entities;

namespace BF2WebAdmin.Server.AdminChannel;

/// <summary>
/// The compiled game scripts: GameScripts/src/&lt;name&gt;.ts with its types removed, embedded in this assembly as
/// GameScripts/&lt;name&gt;.js by the project's BuildGameScripts target.
/// </summary>
public static class GameScripts
{
    private const string Prefix = "GameScripts/";
    private static readonly Assembly Assembly = typeof(GameScripts).Assembly;
    private static readonly ConcurrentDictionary<string, string?> Sources = new();

    public static IReadOnlyList<string> Names { get; } = Assembly.GetManifestResourceNames()
        .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(".js", StringComparison.Ordinal))
        .Select(n => n[Prefix.Length..^3])
        .Order(StringComparer.Ordinal)
        .ToList();

    /// <summary>The JavaScript source of a script, or null when there is none of that name.</summary>
    public static string? Source(string name) => Sources.GetOrAdd(name, static n =>
    {
        using var stream = Assembly.GetManifestResourceStream($"{Prefix}{n}.js");
        if (stream == null)
            return null;

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    });
}

/// <summary>
/// <see cref="IScriptRunner"/> over the server's current admin channel link. A run belongs to the link it was started
/// on. The session's apply loop hands it the script events (<see cref="AdminStateApplier"/>), so a run's output and end
/// reach the modules in order with the game's events.
/// </summary>
public sealed class ScriptRunner : IScriptRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly GameServer _server;
    private readonly ILogger _logger;
    private readonly Func<string, string?> _source;
    private readonly Func<IEvent, ValueTask> _publish;
    private readonly ConcurrentDictionary<uint, ScriptRun> _runs = new();
    // Guards the instance counter, the order of StartScript and ListScripts on the link, and the cut-offs
    private readonly object _gate = new();
    private readonly Queue<(AdminSession Session, uint Cutoff)> _lists = new();
    private uint _lastInstance;

    /// <param name="source">A script's JavaScript by name; the embedded <see cref="GameScripts"/> when null.</param>
    /// <param name="publish">Hands an event to the server's modules; <see cref="GameServer.PublishAsync"/> when null.</param>
    public ScriptRunner(GameServer server, ILogger logger, Func<string, string?>? source = null, Func<IEvent, ValueTask>? publish = null)
    {
        _server = server;
        _logger = logger;
        _source = source ?? GameScripts.Source;
        _publish = publish ?? server.PublishAsync;
    }

    public bool Available => _server.Session is { IsOpen: true } session && CanRun(session);

    /// <summary>The runs that have not ended.</summary>
    public IReadOnlyCollection<IScriptRun> Running => _runs.Values.ToList();

    private static bool CanRun(AdminSession session) => (session.Capabilities & Welcome.CapScripts) != 0;

    public async Task<IScriptRun> StartAsync(string name, object? args = null)
    {
        var session = _server.Session;
        if (session is not { IsOpen: true })
            throw new AdminCommandException($"{_server.Id} is not connected");
        if (!CanRun(session))
            throw new AdminCommandException($"{_server.Id}'s module does not run scripts (no script capability, or adminAllowScripts 0)");

        var source = _source(name) ?? throw new ArgumentException($"There is no game script named '{name}'", nameof(name));
        var arguments = args == null ? "" : JsonSerializer.Serialize(args, JsonOptions);
        var size = Encoding.UTF8.GetByteCount(name) + Encoding.UTF8.GetByteCount(arguments) + Encoding.UTF8.GetByteCount(source);
        if (Encoding.UTF8.GetByteCount(source) > CmdStartScript.MaxSourceBytes || size + 32 > AdminProtocol.MaxOutboundPayload)
            throw new ArgumentException($"Game script '{name}' with its arguments is too large to send ({size} bytes)", nameof(name));

        ScriptRun run;
        Task<AdminMessage> reply;
        lock (_gate)
        {
            if (++_lastInstance == 0)
                _lastInstance = 1;

            var instance = _lastInstance;
            // Registered before it is sent: the script's first events can be applied before the reply is awaited
            run = new ScriptRun(this, session, instance, name);
            _runs[instance] = run;
            reply = session.RequestAsync(id => new CmdStartScript(id, instance, CmdStartScript.Api, 0, name, arguments, source));
        }

        try
        {
            await reply;
        }
        catch (AdminCommandException)
        {
            _runs.TryRemove(run.Id, out _);
            throw;
        }

        _logger.LogInformation("Script {Script} ({Instance}) started on {ServerId}", name, run.Id, _server.Id);
        return run;
    }

    internal async Task SendAsync(ScriptRun run, object message)
    {
        if (!run.IsRunning)
            throw new InvalidOperationException($"Script {run.Name} has ended");

        var json = JsonSerializer.Serialize(message, JsonOptions);
        if (Encoding.UTF8.GetByteCount(json) > CmdStartScript.MaxMessageBytes)
            throw new ArgumentException($"A script message is at most {CmdStartScript.MaxMessageBytes} bytes of JSON", nameof(message));

        await run.Session.RequestAsync(id => new CmdScriptMessage(id, run.Id, json));
    }

    internal async Task StopAsync(ScriptRun run)
    {
        if (!run.IsRunning)
            return;

        try
        {
            await run.Session.RequestAsync(id => new CmdStopScript(id, run.Id));
        }
        catch (AdminCommandException e) when (e.Code == AdminErrorCode.Unavailable || !run.Session.IsOpen || !run.IsRunning)
        {
            // It ended by itself meanwhile, or the link went (which ends it too)
        }
    }

    // ---------------------------------------------------------------- from the session's apply loop

    public async ValueTask OnFinishedAsync(EvScriptFinished e, DateTimeOffset time)
    {
        if (!_runs.TryRemove(e.Instance, out var run))
        {
            _logger.LogDebug("Script {Script} ({Instance}) on {ServerId} ended, but WebAdmin has no record of it", e.Name, e.Instance, _server.Id);
            return;
        }

        var reason = e.Reason is >= EvScriptFinished.ReasonFinished and <= EvScriptFinished.ReasonLevelUnload ? (ScriptEndReason)e.Reason : ScriptEndReason.Error;
        var result = new ScriptResult(reason, e.Result, e.Calls, TimeSpan.FromMicroseconds(e.CpuMicroseconds), TimeSpan.FromMicroseconds(e.MaxCallMicroseconds));
        await EndAsync(run, result, time);
    }

    public async ValueTask OnEventAsync(EvScriptEvent e, DateTimeOffset time)
    {
        if (!_runs.TryGetValue(e.Instance, out var run))
        {
            _logger.LogDebug("Script event for unknown script {Instance} on {ServerId}", e.Instance, _server.Id);
            return;
        }

        if (e.Kind == EvScriptEvent.KindLog)
        {
            _logger.LogInformation("Script {Script} on {ServerId}: {Line}", run.Name, _server.Id, e.Payload);
            await _publish(new ScriptLogEvent(run, e.Payload, time));
            return;
        }

        JsonElement data;
        try
        {
            using var document = JsonDocument.Parse(e.Payload);
            data = document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Script {Script} on {ServerId} emitted something that is not JSON ({Error})", run.Name, _server.Id, ex.Message);
            return;
        }

        await _publish(new ScriptDataEvent(run, data, time));
    }

    /// <summary>
    /// After a resync (events were lost, perhaps a script's end): asks the module which scripts run. The answer comes
    /// back through the apply loop (<see cref="OnListAsync"/>), after every event the module sent before it.
    /// </summary>
    public void RequestList(AdminSession session)
    {
        if (!session.IsOpen || !CanRun(session) || !_runs.Values.Any(r => r.Session == session))
            return;

        lock (_gate)
        {
            // Scripts started after this were not yet known to the module when it answers
            _lists.Enqueue((session, _lastInstance));
            session.Post(new CmdListScripts(session.NextRequestId()));
        }
    }

    public async ValueTask OnListAsync(AdminSession session, ScriptList list, DateTimeOffset time)
    {
        uint cutoff;
        lock (_gate)
        {
            while (_lists.Count > 0 && _lists.Peek().Session != session)
                _lists.Dequeue();
            if (_lists.Count == 0)
                return;

            cutoff = _lists.Dequeue().Cutoff;
        }

        var listed = list.Scripts.Select(s => s.Instance).ToHashSet();
        foreach (var run in _runs.Values.Where(r => r.Session == session && r.Id <= cutoff && !listed.Contains(r.Id)).ToList())
        {
            if (_runs.TryRemove(run.Id, out _))
                await EndAsync(run, new ScriptResult(ScriptEndReason.Lost, "", 0, TimeSpan.Zero, TimeSpan.Zero), time);
        }
    }

    /// <summary>Scripts do not outlive their link: ends the record of every run of a closed session.</summary>
    public async ValueTask OnSessionClosedAsync(AdminSession session, DateTimeOffset time)
    {
        foreach (var run in _runs.Values.Where(r => r.Session == session).ToList())
        {
            if (_runs.TryRemove(run.Id, out _))
                await EndAsync(run, new ScriptResult(ScriptEndReason.LinkClosed, "", 0, TimeSpan.Zero, TimeSpan.Zero), time);
        }
    }

    private async ValueTask EndAsync(ScriptRun run, ScriptResult result, DateTimeOffset time)
    {
        if (result.Failed)
        {
            _logger.LogWarning("Script {Script} ({Instance}) on {ServerId} failed ({Reason}) after {Calls} calls: {Error}", run.Name, run.Id, _server.Id,
                result.Reason, result.Calls, result.Text);
        }
        else
        {
            _logger.LogInformation(
                "Script {Script} ({Instance}) on {ServerId} ended ({Reason}): {Calls} calls, {CpuMs:0.###} ms CPU, longest call {MaxMs:0.###} ms",
                run.Name, run.Id, _server.Id, result.Reason, result.Calls, result.CpuTime.TotalMilliseconds, result.LongestCall.TotalMilliseconds);
        }

        run.Complete(result);
        await _publish(new ScriptFinishedEvent(run, result, time));
    }
}

internal sealed class ScriptRun(ScriptRunner runner, AdminSession session, uint id, string name) : IScriptRun
{
    private readonly TaskCompletionSource<ScriptResult> _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public AdminSession Session { get; } = session;
    public uint Id { get; } = id;
    public string Name { get; } = name;
    public bool IsRunning => !_finished.Task.IsCompleted;
    public Task<ScriptResult> Finished => _finished.Task;

    public Task SendAsync(object message) => runner.SendAsync(this, message);
    public Task StopAsync() => runner.StopAsync(this);

    public void Complete(ScriptResult result) => _finished.TrySetResult(result);

    public override string ToString() => $"{Name} ({Id})";
}
