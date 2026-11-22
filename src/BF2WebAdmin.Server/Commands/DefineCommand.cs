using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands;

[Command("define <Term>", Auth.All, Description = "Looks a word up in Urban Dictionary.")]
public class DefineCommand : BaseCommand
{
    public string Term { get; set; }
}