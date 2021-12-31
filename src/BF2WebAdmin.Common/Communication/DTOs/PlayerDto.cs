using BF2WebAdmin.Common.Entities.Game;
using MessagePack;
using ProtoBuf;

namespace BF2WebAdmin.Common.Communication.DTOs
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [MessagePackObject(keyAsPropertyName: true)]
    public class PlayerDto
    {
        public string Name { get; set; }
        public int Index { get; set; }
        public string IpAddress { get; set; }
        public string Hash { get; set; }
        public string Country { get; set; }
        public int Rank { get; set; }
        public int Team { get; set; }
        public bool IsAlive { get; set; }
        public ScoreDto Score { get; set; }

        public PlayerDto() { }

        public PlayerDto(Player player)
        {
            Name = player.Name;
            Index = player.Index;
            IpAddress = player.IpAddress.ToString();
            Hash = player.Hash;
            Country = player.Country.Code;
            Rank = (int)player.Rank;
            Team = player.Team.Id;
            IsAlive = player.IsAlive;
            Score = new ScoreDto(player.Score);
        }
    }
}