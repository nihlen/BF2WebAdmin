using BF2WebAdmin.Common.Entities.Game;
using MessagePack;
using ProtoBuf;

namespace BF2WebAdmin.Common.Communication.DTOs
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [MessagePackObject(keyAsPropertyName: true)]
    public class Vector3
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Vector3() { }

        public Vector3(Position position)
        {
            X = position.X;
            Y = position.Y;
            Z = position.Height;
        }

        public Vector3(Rotation rotation)
        {
            X = rotation.Yaw;
            Y = rotation.Pitch;
            Z = rotation.Roll;
        }
    }
}