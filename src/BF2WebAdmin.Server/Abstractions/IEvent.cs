namespace BF2WebAdmin.Server.Abstractions;

public interface IEvent
{
    DateTimeOffset TimeStamp { get; }
}
