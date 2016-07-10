namespace BF2WebAdmin.Server.Abstractions
{
    public interface IEventParser
    {
        void ParseMessage(string message);
    }
}