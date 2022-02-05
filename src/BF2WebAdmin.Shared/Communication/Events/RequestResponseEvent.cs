namespace BF2WebAdmin.Shared.Communication.Events;

public class RequestResponseEvent : IMessagePayload
{
    public string Request { get; set; }
    public string Response { get; set; }
}
