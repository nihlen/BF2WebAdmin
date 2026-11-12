using System.Text.RegularExpressions;

namespace BF2WebAdmin.Server.Abstractions;

/// <summary>
/// Collects what a chat command says in game (<see cref="IGameWriter.SendText"/> and private messages) while a web user's
/// command runs, so the web UI can show the answer. The capture follows the async flow of the command (into the handlers'
/// <c>Task.Run</c>, which copies it); the caller waits for the handlers (<see cref="IModManager.HandleFakeChatMessageAsync"/>),
/// and what they send after it has read <see cref="Lines"/> is not shown.
/// </summary>
public sealed partial class GameTextCapture
{
    private static readonly AsyncLocal<GameTextCapture?> Current = new();

    private readonly List<string> _lines = [];

    /// <summary>Start collecting for the current async flow. Dispose (or leave the calling async method) to stop.</summary>
    public static GameTextCapture Begin()
    {
        var capture = new GameTextCapture();
        Current.Value = capture;
        return capture;
    }

    /// <summary>Add a line to the capture of the current flow, if there is one. BF2's colour codes are removed.</summary>
    public static void Add(string text)
    {
        if (Current.Value is not { } capture)
            return;

        var line = ColourCode().Replace(text, "").Trim();
        if (line.Length == 0)
            return;

        lock (capture._lines)
            capture._lines.Add(line);
    }

    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (_lines)
                return _lines.ToArray();
        }
    }

    public static void End() => Current.Value = null;

    // §C1001 colours and §3 line breaks
    [GeneratedRegex(@"§(C\d{4}|\d)")]
    private static partial Regex ColourCode();
}
