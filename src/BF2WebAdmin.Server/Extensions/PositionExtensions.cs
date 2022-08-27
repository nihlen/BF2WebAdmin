using BF2WebAdmin.Common.Entities.Game;

namespace BF2WebAdmin.Server.Extensions;

public static class PositionExtensions
{
    public static Position NewRelativePosition(this Position position, double xDiff, double heightDiff, double yDiff)
    {
        return new Position(position.X + xDiff, position.Height + heightDiff, position.Y + yDiff);
    }

    public static double Distance(this Position position, Position target)
    {
        var deltaX = position.X - target.X;
        var deltaY = position.Y - target.Y;
        var deltaZ = position.Height - target.Height;

        return Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);
    }

    public static bool IsInArea(this Position pos, Position[] area)
    {
        var result = false;
        var j = area.Length - 1;

        for (var i = 0; i < area.Length; i++)
        {
            if (area[i].Y < pos.Y && area[j].Y >= pos.Y || area[j].Y < pos.Y && area[i].Y >= pos.Y)
            {
                if (area[i].X + (pos.Y - area[i].Y) / (area[j].Y - area[i].Y) * (area[j].X - area[i].X) < pos.X)
                {
                    result = !result;
                }
            }

            j = i;
        }

        return result;
    }
}

public static class RotationExtensions
{
    // Returns the squared length of this vector (RO).
    public static double SqrMagnitude(this Rotation r)
    {
        return r.Yaw * r.Yaw + r.Pitch * r.Pitch + r.Roll * r.Roll;
    }

    //private double SqrMagnitude => Yaw * Yaw + Pitch * Pitch + Roll * Roll;

    // *Undocumented*
    private const float KEpsilonNormalSqrt = 1e-15F;

    // (180d / Math.PI)
    private const double RadToDeg = 57.295779513082323d;

    // Dot Product of two vectors.
    private static double Dot(Rotation lhs, Rotation rhs) { return lhs.Yaw * rhs.Yaw + lhs.Pitch * rhs.Pitch + lhs.Roll * rhs.Roll; }

    public static double AngleUnity(this Rotation from, Rotation to)
    {
        //var fromAdjusted = new Rotation
        //(
        //    from.Yaw < 0 ? from.Yaw + 360 : from.Yaw,
        //    from.Pitch < 0 ? from.Pitch + 360 : from.Pitch,
        //    from.Roll < 0 ? from.Roll + 360 : from.Roll
        //);

        //var toAdjusted = new Rotation
        //(
        //    to.Yaw < 0 ? to.Yaw + 360 : to.Yaw,
        //    to.Pitch < 0 ? to.Pitch + 360 : to.Pitch,
        //    to.Roll < 0 ? to.Roll + 360 : to.Roll
        //);

        // https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Math/Vector3.cs
        // sqrt(a) * sqrt(b) = sqrt(a * b) -- valid for real numbers
        var denominator = (float)Math.Sqrt(from.SqrMagnitude() * to.SqrMagnitude());
        if (denominator < KEpsilonNormalSqrt)
            return 0F;

        var dot = Math.Clamp(Dot(from, to) / denominator, -1F, 1F);
        var result = ((float)Math.Acos(dot)) * RadToDeg;
        if (result > 10)
        {
            return result;
        }

        return result;
    }

    public static double Angle(this Rotation from, Rotation to)
    {
        // TODO: fix calculation
        // Simplified by adding delta of yaw and pitch - doesn't feel correct but it is what it is
        var deltaX = Math.Abs(DeltaAngle(from.Yaw, to.Yaw));
        var deltaY = Math.Abs(DeltaAngle(from.Pitch, to.Pitch));
        return deltaX + deltaY;
    }

    // https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Math/Mathf.cs#L377
    // Calculates the shortest difference between two given angles.
    public static double DeltaAngle(double current, double target)
    {
        var delta = Repeat((target - current), 360.0d);
        if (delta > 180.0F)
            delta -= 360.0F;
        return delta;
    }

    // https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Runtime/Export/Math/Mathf.cs#L356
    // Loops the value t, so that it is never larger than length and never smaller than 0.
    public static double Repeat(double t, double length)
    {
        return Math.Clamp(t - Math.Floor(t / length) * length, 0.0d, length);
    }
}