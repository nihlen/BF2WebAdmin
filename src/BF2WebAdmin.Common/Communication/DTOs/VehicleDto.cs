using BF2WebAdmin.Common.Entities.Game;
using MessagePack;
using ProtoBuf;

namespace BF2WebAdmin.Common.Communication.DTOs
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [MessagePackObject(keyAsPropertyName: true)]
    public class VehicleDto
    {
        public int RootVehicleId { get; set; }
        public string RootVehicleTemplate { get; set; }
        //public int VehicleId { get; set; }
        //public string VehicleTemplate { get; set; }

        public VehicleDto() { }

        public VehicleDto(Vehicle vehicle)
        {
            RootVehicleId = vehicle?.RootVehicleId ?? 0;
            RootVehicleTemplate = vehicle?.RootVehicleTemplate;
        }
    }
}