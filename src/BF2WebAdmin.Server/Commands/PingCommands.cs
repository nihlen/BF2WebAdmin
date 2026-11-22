using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands;

[Command("ping", Auth.All, Description = "Shows your average and median ping.")]
public class PingSelfCommand : BaseCommand
{
        
}

[Command("ping <Name>", Auth.All, Description = "Shows a player's average and median ping.")]
public class PingCommand : BaseCommand
{
    public string Name { get; set; }
}