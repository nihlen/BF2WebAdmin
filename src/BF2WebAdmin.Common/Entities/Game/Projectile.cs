namespace BF2WebAdmin.Common.Entities.Game;

public class Projectile : Entity
{
    public Player Owner { get; set; }
    public double Distance { get; set; }
    public double TurnedDegrees { get; set; }
    public double HorizontalDegrees { get; set; }
}