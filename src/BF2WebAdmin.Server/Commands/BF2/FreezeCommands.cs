using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("freeze", Auth.God)]
public class FreezeAllCommand : BaseCommand
{
    public string Name { get; set; }
}

[Command("freeze <Name>", Auth.God)]
public class FreezeCommand : BaseCommand
{
    public string Name { get; set; }
}