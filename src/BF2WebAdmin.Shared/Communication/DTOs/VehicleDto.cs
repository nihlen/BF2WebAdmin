namespace BF2WebAdmin.Shared.Communication.DTOs;

//[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
//[MessagePackObject(keyAsPropertyName: true)]
public class VehicleDto
{
    public int RootVehicleId { get; set; }
    public string RootVehicleTemplate { get; set; }
    public string SubVehicleTemplate { get; set; }
    //public int VehicleId { get; set; }
    //public string VehicleTemplate { get; set; }
}