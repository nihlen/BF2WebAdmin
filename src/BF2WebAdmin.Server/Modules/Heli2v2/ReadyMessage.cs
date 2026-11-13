using System.Text.RegularExpressions;

namespace BF2WebAdmin.Server.Modules.Heli2v2;

public enum ReadyChat
{
    /// <summary>Not about the round.</summary>
    None,

    /// <summary>"pad" or "jpad": the player goes back to the pad, out of the pending and the active round.</summary>
    Pad,

    /// <summary>"rdy", "go" and their typos: the player's heli is ready.</summary>
    Ready,
}

/// <summary>What a 2v2 player's line in global chat means for the round.</summary>
public static partial class ReadyMessage
{
    public static ReadyChat Parse(string text)
    {
        if (Pad().IsMatch(text))
            return ReadyChat.Pad;

        // "yes" fits the ready pattern ("ye" plus one more character)
        if (text == "yes")
            return ReadyChat.None;

        return Ready().IsMatch(text) || ReadyWithOtherText().IsMatch(text) ? ReadyChat.Ready : ReadyChat.None;
    }

    [GeneratedRegex(@"^j?pad$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Pad();

    // A line of only the letters of "ready" or "go" (typos too), with up to two characters after and symbols around; not "gg".
    // TODO: fix 2 players ready on 1 team then switching
    [GeneratedRegex(@"^j?[^0-9A-Za-z]*(?!gg)(([ready]{1,8}|[go]{1,5})[A-z0-9\?\!\.]{0,2})[^0-9A-Za-z]*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Ready();

    // "rdy" or "go" as the first or the last word of a longer line
    [GeneratedRegex(@"(^(j?rd[y]?|j?g[o]?)[ ]+)|[ ]+(rd[y]?|g[o]?)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ReadyWithOtherText();
}
