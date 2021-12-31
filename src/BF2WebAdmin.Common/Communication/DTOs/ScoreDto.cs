using BF2WebAdmin.Common.Entities.Game;
using MessagePack;
using ProtoBuf;

namespace BF2WebAdmin.Common.Communication.DTOs
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [MessagePackObject(keyAsPropertyName: true)]
    public class ScoreDto
    {
        public int Total { get; set; }
        public int Team { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Ping { get; set; }

        public ScoreDto() { }

        public ScoreDto(Score score)
        {
            Total = score.Total;
            Team = score.Team;
            Kills = score.Kills;
            Deaths = score.Deaths;
            Ping = score.Ping;
        }
    }
}