using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands;

[Command("ping", Auth.All)]
public class PingSelfCommand : BaseCommand
{
        
}

[Command("ping <Name>", Auth.All)]
public class PingCommand : BaseCommand
{
    public string Name { get; set; }
}