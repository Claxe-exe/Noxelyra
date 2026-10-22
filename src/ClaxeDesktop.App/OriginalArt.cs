using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace ClaxeDesktop.App;

internal static class OriginalArt
{
    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
    internal static DrawingImage Wallpaper()
    {
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            // Mountain ridges overshoot the canvas; clipping keeps the image bounds at 16:9 so no black band appears when stretched.
            dc.PushClip(new RectangleGeometry(new(0, 0, 1600, 900)));
            dc.DrawRectangle(new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#100E20"), (Color)ColorConverter.ConvertFromString("#30203E"), 55), null, new(0, 0, 1600, 900));
            var random = new Random(1709);
            for (int i = 0; i < 150; i++) { double r = random.NextDouble() * 1.7 + .4; dc.DrawEllipse(Brush("#AD9BCB"), null, new(random.Next(1600), random.Next(620)), r, r); }
            dc.DrawEllipse(Brush("#AD8CDE"), null, new(1160, 235), 108, 108);
            dc.DrawEllipse(Brush("#22182F"), null, new(1193, 211), 94, 94);
            dc.DrawEllipse(null, new Pen(Brush("#644879"), 1.5), new(1160, 235), 155, 47);
            for (int layer = 0; layer < 3; layer++)
            {
                var mountain = new StreamGeometry();
                using (var g = mountain.Open())
                {
                    g.BeginFigure(new(0, 900), true, true); g.LineTo(new(0, 570 + layer * 75), true, false);
                    for (int x = 0; x <= 1700; x += 100) g.LineTo(new(x, 540 + layer * 80 + random.Next(140)), true, false);
                    g.LineTo(new(1600, 900), true, false);
                }
                dc.DrawGeometry(Brush(new[] { "#21182F", "#191424", "#110F1B" }[layer]), null, mountain);
            }
        }
        drawing.Freeze(); return new(drawing);
    }
    internal static DrawingImage Mascot(bool happy = false)
    {
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            dc.DrawRectangle(Brushes.Transparent, null, new(0, 0, 256, 256));
            dc.DrawEllipse(Brush("#241C39"), new Pen(Brush("#B38CE8"), 4), new(128, 143), 87, 84);
            foreach (var shape in new[] { "M 49,105 L 44,27 L 108,67 Z", "M 150,66 L 213,27 L 207,108 Z" }) dc.DrawGeometry(Brush("#59416F"), new Pen(Brush("#B38CE8"), 4), Geometry.Parse(shape));
            dc.DrawGeometry(Brush("#E0C4F7"), null, Geometry.Parse("M 137,78 A 22,22 0 1 1 112,50 A 18,18 0 0 0 137,78 Z"));
            if (happy)
            {
                dc.DrawGeometry(null, new Pen(Brush("#CEB3F0"), 6), Geometry.Parse("M 77,143 Q 91,120 106,143 M 150,143 Q 165,120 179,143"));
            }
            else
            {
                foreach (double x in new[] { 91d, 165d }) { dc.DrawEllipse(Brush("#B4DDE7"), null, new(x, 139), 13, 20); dc.DrawEllipse(Brush("#251934"), null, new(x + 2, 141), 5, 13); dc.DrawEllipse(Brushes.White, null, new(x - 3, 132), 3, 4); }
            }
            dc.DrawGeometry(null, new Pen(Brush("#CEB3F0"), 3), Geometry.Parse("M 114,169 Q 128,184 142,169"));
            dc.DrawEllipse(Brush("#AE72AA"), null, new(67, 165), 12, 5); dc.DrawEllipse(Brush("#AE72AA"), null, new(189, 165), 12, 5);
        }
        group.Freeze(); return new(group);
    }
    internal static void Save(ImageSource source, string path, int width, int height)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) dc.DrawImage(source, new(0, 0, width, height));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(path); png.Save(file);
    }
}
