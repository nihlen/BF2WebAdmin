using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands.BF2;

[Command("score <Name> <TotalScore> <TeamScore> <Kills> <Deaths>", Auth.Admin, Description = "Sets a player's score, team score, kills and deaths.")]
public class ScoreCommand : BaseCommand
{
    public string Name { get; set; }
    public int TotalScore { get; set; }
    public int TeamScore { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
}

[Command("score <Reset>", Auth.Admin, Description = "Resets every player's score with .score reset.")]
public class ScoreResetCommand : BaseCommand
{
    public string Reset { get; set; }
}