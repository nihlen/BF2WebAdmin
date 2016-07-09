using System.Globalization;

namespace BF2WebAdmin.Common.Entities.Game
{
    public class Position
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Height { get; set; }

        public Position(double x, double height, double y)
        {
            X = x;
            Height = height;
            Y = y;
        }

        public static implicit operator Position(string position)
        {
            return Parse(position);
        }

        public static Position Parse(string position)
        {
            var parts = position.Split('/');
            return new Position(ParseDouble(parts[0]), ParseDouble(parts[1]), ParseDouble(parts[2]));
        }

        private static double ParseDouble(string part)
        {
            return double.Parse(part, NumberStyles.Any, CultureInfo.InvariantCulture);
        }

        public override string ToString()
        {
            return $"{X}/{Height}/{Y}";
        }
    }
}
