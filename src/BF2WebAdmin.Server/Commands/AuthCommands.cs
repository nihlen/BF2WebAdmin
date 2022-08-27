using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands;

[Command("auth <Name> <Level>", Auth.Admin)]
public class SetAuthCommand : BaseCommand
{
    public string Name { get; set; }
    public int Level { get; set; }
}

[Command("auth <Name>", Auth.Admin)]
public class GetAuthCommand : BaseCommand
{
    public string Name { get; set; }
}