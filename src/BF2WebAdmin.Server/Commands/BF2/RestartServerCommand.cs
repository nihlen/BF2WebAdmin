using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

/// <summary>Quit the game server cleanly (console quit) so its container's restart policy starts it again. Replaces .crash for restarts.</summary>
[Command("restartserver", Auth.Admin)]
public class RestartServerCommand : BaseCommand
{
}
