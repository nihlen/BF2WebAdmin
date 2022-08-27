using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands;

[Command("quote", Auth.All)]
public class QuoteCommand : BaseCommand
{
}

[Command("quote <Category>", Auth.All)]
public class QuoteCategoryCommand : BaseCommand
{
    public string Category { get; set; }
}