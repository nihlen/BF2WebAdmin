namespace BF2WebAdmin.Server.Communication.DTOs;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
//[MessagePackObject(keyAsPropertyName: true)]
public class Vector3
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}