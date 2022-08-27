namespace BF2WebAdmin.Common.Entities.Game;

public abstract class Entity
{
    public int Id { get; set; }
    public string Template { get; set; }
    public Position Position { get; set; }
    public Rotation Rotation { get; set; }
}