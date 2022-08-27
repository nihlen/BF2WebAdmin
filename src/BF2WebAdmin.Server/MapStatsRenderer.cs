using BF2WebAdmin.Common.Entities.Game;
using SkiaSharp;

namespace BF2WebAdmin.Server;

public class MapStatsRenderer
{
    public static Stream GetMapMovementPathImage(string map, IEnumerable<(Position, Rotation)> movementPath1, IEnumerable<(Position, Rotation)> movementPath2)
    {
        if (movementPath1 == null || !movementPath1.Any())
            return null;
        if (movementPath2 == null || !movementPath2.Any())
            return null;

        // crate a surface
        var info = new SKImageInfo(700, 700);
        using var surface = SKSurface.Create(info);

        // the the canvas and properties
        var canvas = surface.Canvas;

        // make sure the canvas is blank
        canvas.Clear(SKColors.White);

        var mapBackground = SKBitmap.Decode($"Assets/{map}.png");
        var customOffset = GetCustomMapOffset(map);
        var imageOffset = new SKPoint(
            (info.Width / 2f) - (mapBackground.Width / 2f) + customOffset.x,
            (info.Height / 2f) - (mapBackground.Height / 2f) + customOffset.y
        );
        canvas.DrawBitmap(mapBackground, imageOffset);

        // US heli path
        var paint1 = new SKPaint
        {
            Color = SKColors.Blue,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
        };
        var path1 = new SKPath();
        path1.MoveTo(ToImageCoordinates(movementPath1.First().Item1));
        foreach (var (pos, _) in movementPath1)
        {
            path1.LineTo(ToImageCoordinates(pos));
        }
        canvas.DrawPath(path1, paint1);

        // China heli path
        var paint2 = new SKPaint
        {
            Color = SKColors.Red,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
        };
        var path2 = new SKPath();
        path2.MoveTo(ToImageCoordinates(movementPath2.First().Item1));
        foreach (var (pos, _) in movementPath2)
        {
            path1.LineTo(ToImageCoordinates(pos));
        }
        canvas.DrawPath(path2, paint2);

        // draw some text
        var paint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            TextAlign = SKTextAlign.Center,
            TextSize = 32
        };
        var textPosition = new SKPoint(info.Width / 2, 40);
        //var coord = new SKPoint(info.Width / 2, (info.Height + paint.TextSize) / 2);
        canvas.DrawText("Round movements", textPosition, paint);

        // save the file
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite("output.png");
        data.SaveTo(stream);

        var result = new MemoryStream();
        data.SaveTo(result);
        return result;

        SKPoint ToImageCoordinates(Position pos)
        {
            return new SKPoint(
                (float)pos.X + (info.Width / 2f) + customOffset.x,
                (float)pos.Y + (info.Height / 2f) + customOffset.y
            );
        }
    }

    private static (float x, float y) GetCustomMapOffset(string map)
    {
        return map switch
        {
            //"dalian_2_v_2" => (30, -130),
            _ => (0, 0)
        };
    }
}