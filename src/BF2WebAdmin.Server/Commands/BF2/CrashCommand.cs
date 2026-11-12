using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

/// <summary>Crashes the game server (an object it cannot spawn). God only: .restartserver is the clean restart for admins.</summary>
[Command("crash", Auth.God)]
public class CrashCommand : BaseCommand
{
}