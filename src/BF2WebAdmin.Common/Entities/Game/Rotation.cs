using System.Globalization;

namespace BF2WebAdmin.Common.Entities.Game;

public class Rotation
{
    public double Yaw { get; set; }
    public double Pitch { get; set; }
    public double Roll { get; set; }

    // BF2: Yaw/Pitch/Roll it seems
    public static Rotation Neutral { get; } = new Rotation(0, 0, 0);

    public Rotation(double yaw, double pitch, double roll)
    {
        Yaw = yaw;
        Pitch = pitch;
        Roll = roll;
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
        //return $"{Yaw:0.000}/{Pitch:0.000}/{Roll:0.000}";
        return string.Format(CultureInfo.InvariantCulture, "{0:0.000}/{1:0.000}/{2:0.000}", Yaw, Pitch, Roll);
    }
}