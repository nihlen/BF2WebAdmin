using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("record", Auth.Admin)]
public class RecordCommand : BaseCommand
{
}

[Command("playback", Auth.Admin)]
public class PlaybackCommand : BaseCommand
{
}

[Command("loop", Auth.Admin)]
public class LoopCommand : BaseCommand
{
}