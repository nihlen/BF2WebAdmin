namespace BF2WebAdmin.Server.Communication.DTOs;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
//[MessagePackObject(keyAsPropertyName: true)]
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
    public ScoreDto Score { get; set; } = new();
    public Vector3? Position { get; set; }
    public Vector3? Rotation { get; set; }
}