using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands;

/// <summary>
/// An admin command run by <see cref="Services.ServerAdmin"/> (kick, warn, ban, ...): the same commands as "!" in the web
/// chat box and Discord, here as chat commands with "." or "!" in game. <see cref="Args"/> is the rest of the line.
/// </summary>
public abstract class AdminChatCommand : BaseCommand
{
    public string Args { get; set; } = "";

    /// <summary>The <see cref="Services.ServerAdmin.RunAsync"/> line.</summary>
    public abstract string Line { get; }
}

/// <summary>kick &lt;player&gt; [reason]: a player by index or part of the name.</summary>
[Command("kick|k <Args>", Auth.Admin)]
public class KickCommand : AdminChatCommand
{
    public override string Line => "kick " + Args;
}

/// <summary>warn &lt;player&gt; [reason]</summary>
[Command("warn|w <Args>", Auth.Admin)]
public class WarnCommand : AdminChatCommand
{
    public override string Line => "warn " + Args;
}

/// <summary>ban &lt;player&gt; [length] [reason]: length Perm (default), Round, seconds, 30m, 12h, 7d, 2w.</summary>
[Command("ban|b <Args>", Auth.SuperAdmin)]
public class BanCommand : AdminChatCommand
{
    public override string Line => "ban " + Args;
}

/// <summary>unban &lt;CD-key hash or address&gt;</summary>
[Command("unban <Args>", Auth.SuperAdmin)]
public class UnbanCommand : AdminChatCommand
{
    public override string Line => "unban " + Args;
}

[Command("banlist", Auth.Admin)]
public class BanListCommand : AdminChatCommand
{
    public override string Line => "banlist";
}

[Command("clearbans", Auth.God)]
public class ClearBansCommand : AdminChatCommand
{
    public override string Line => "clearbans";
}

/// <summary>map &lt;map&gt; &lt;mode&gt; [size]: a map of the rotation.</summary>
[Command("map|m <Args>", Auth.Admin)]
public class RunMapCommand : AdminChatCommand
{
    public override string Line => "map " + Args;
}

/// <summary>The players (admin.listPlayers).</summary>
[Command("list", Auth.Admin)]
public class ListPlayersCommand : AdminChatCommand
{
    public override string Line => "list";
}
