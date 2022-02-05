namespace BF2WebAdmin.Shared;

public interface IMessagePayload { }

public interface IMessage
{
    string Type { get; }
    string ServerId { get; }
    //IMessagePayload Payload { get; set; }
    object Payload { get; set; }
}
