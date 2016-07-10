namespace BF2WebAdmin.Server.Abstractions
{
    public interface IGameReader
    {
        void ParseMessage(string message);
    }
}