using System.Collections.Generic;

namespace BF2WebAdmin.Common.Entities.Game;

public class Team
{
    public int Id { get; set; }
    public string Name { get; set; }
    public IList<Player> Players { get; } = new List<Player>();
}