using BF2WebAdmin.Shared.Communication.DTOs;

namespace BF2WebAdmin.Shared.Communication.Events;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
//[MessagePackObject(keyAsPropertyName: true)]
public class ServerUpdateEvent : ServerDto, IMessagePayload
{
    //public string Id { get; set; }
    //public string Name { get; set; }
    //public string IpAddress { get; set; }
    //public int GamePort { get; set; }
    //public int QueryPort { get; set; }
    //public string Map { get; set; }
    //public int Players { get; set; }
    //public int MaxPlayers { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}