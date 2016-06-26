namespace BF2WebAdmin.Server.Entities.Game
{
    public abstract class Entity
    {
        public int Id { get; set; }
        public string Template { get; set; }
        public Position Position { get; set; }
        public Rotation Rotation { get; set; }
    }
}
