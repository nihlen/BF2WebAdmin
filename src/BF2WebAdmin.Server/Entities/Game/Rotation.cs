using System.Globalization;

namespace BF2WebAdmin.Server.Entities.Game
{
    public class Rotation
    {
        public double Yaw { get; set; }
        public double Pitch { get; set; }
        public double Roll { get; set; }

        public Rotation(double yaw, double roll, double pitch)
        {
            Yaw = yaw;
            Roll = roll;
            Pitch = pitch;
        }

        public static implicit operator Rotation(string rotation)
        {
            return Parse(rotation);
        }

        public static Rotation Parse(string rotation)
        {
            var parts = rotation.Split('/');
            return new Rotation(ParseDouble(parts[0]), ParseDouble(parts[1]), ParseDouble(parts[2]));
        }

        private static double ParseDouble(string part)
        {
            return double.Parse(part, NumberStyles.Any, CultureInfo.InvariantCulture);
        }

        public override string ToString()
        {
            return $"{Yaw}/{Roll}/{Pitch}";
        }
    }
}
