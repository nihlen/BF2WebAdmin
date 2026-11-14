using System.Text.Json;

namespace BF2WebAdmin.Server.Abstractions;

/// <summary>
/// Runs game scripts on the game server: small TypeScript programs (src/BF2WebAdmin.Server/GameScripts) that the
/// server's module runs every tick, for what cannot wait for a round trip to WebAdmin. See docs/scripts.md.
/// </summary>
public interface IScriptRunner
{
    /// <summary>
    /// Whether the server can run scripts now: it is connected and its module says so. Check it before
    /// <see cref="StartAsync"/> and keep another way for servers that cannot.
    /// </summary>
    bool Available { get; }

    /// <summary>
    /// Starts the script <paramref name="name"/> (GameScripts/src/&lt;name&gt;.ts) with <paramref name="args"/> as its
    /// <c>script.args</c> (serialized to JSON, property names in camel case). Returns when the script's top-level code
    /// has run on the game server. Throws <see cref="AdminChannel.AdminCommandException"/> when the server cannot run
    /// scripts or refuses this one; a script that does not compile or throws at once is refused with its error and line.
    /// </summary>
    Task<IScriptRun> StartAsync(string name, object? args = null);
}

/// <summary>
/// One run of a script on the game server. What it emits and logs, and its end, also reach the modules as
/// <see cref="ScriptDataEvent"/>, <see cref="ScriptLogEvent"/> and <see cref="ScriptFinishedEvent"/>.
/// </summary>
public interface IScriptRun
{
    /// <summary>WebAdmin's id of this run on its server.</summary>
    uint Id { get; }

    string Name { get; }

    bool IsRunning { get; }

    /// <summary>Completes when the script has ended, for whatever reason; never fails.</summary>
    Task<ScriptResult> Finished { get; }

    /// <summary>Sends a message (serialized to JSON) to the script's <c>script.onMessage</c> handlers.</summary>
    Task SendAsync(object message);

    /// <summary>Stops the script (its stop handlers run first). Does nothing when it has ended already.</summary>
    Task StopAsync();
}

public enum ScriptEndReason
{
    /// <summary>The script called <c>script.finish</c>.</summary>
    Finished = 1,
    /// <summary>WebAdmin stopped it.</summary>
    Stopped = 2,
    /// <summary>A handler threw, a promise was rejected and not handled, or the script ran out of memory.</summary>
    Error = 3,
    /// <summary>The script used more CPU time than a script may.</summary>
    OverBudget = 4,
    /// <summary>The level unloaded (a map change or a restarted round).</summary>
    LevelUnload = 5,
    /// <summary>The link to the game server closed; scripts do not outlive it.</summary>
    LinkClosed = 101,
    /// <summary>Events from the game server were lost and the script no longer runs; why it ended is unknown.</summary>
    Lost = 102,
}

/// <summary>
/// How a script ended. <paramref name="Text"/> is the JSON text of the value the script finished with (empty without
/// one) for <see cref="ScriptEndReason.Finished"/>, and the error with its stack for a failure. The CPU times are the
/// game server's main thread's.
/// </summary>
public sealed record ScriptResult(ScriptEndReason Reason, string Text, uint Calls, TimeSpan CpuTime, TimeSpan LongestCall)
{
    public bool Failed => Reason is ScriptEndReason.Error or ScriptEndReason.OverBudget;

    /// <summary>The value the script finished with, or null (no value, or it did not finish by itself).</summary>
    public JsonElement? Value
    {
        get
        {
            if (Reason != ScriptEndReason.Finished || Text.Length == 0)
                return null;

            try
            {
                using var document = JsonDocument.Parse(Text);
                return document.RootElement.Clone();
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
