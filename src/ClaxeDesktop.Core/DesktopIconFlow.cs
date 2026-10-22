namespace ClaxeDesktop.Core;

public readonly record struct LayoutRect(double X, double Y, double Width, double Height)
{
    public bool Intersects(LayoutRect other) => X < other.X + other.Width && other.X < X + Width && Y < other.Y + other.Height && other.Y < Y + Height;
    public LayoutRect Inflate(double amount) => new(X - amount, Y - amount, Width + amount * 2, Height + amount * 2);
}

/// <summary>Column-major desktop icon flow that keeps automatic icons out of widget, dock and header areas.</summary>
public static class DesktopIconFlow
{
    public static IReadOnlyList<LayoutRect> Arrange(int count, double width, double height, LayoutRect content, double cellWidth, double cellHeight, IReadOnlyList<LayoutRect> reserved, IReadOnlyList<LayoutRect> occupied)
    {
        if (count < 0 || count > 10000) throw new ArgumentOutOfRangeException(nameof(count));
        if (!(cellWidth > 0 && cellHeight > 0 && double.IsFinite(width) && double.IsFinite(height))) throw new ArgumentOutOfRangeException(nameof(cellWidth));
        var columns = Math.Max(1, (int)(content.Width / cellWidth));
        var rows = Math.Max(1, (int)(content.Height / cellHeight));
        var result = new List<LayoutRect>(count);
        var taken = occupied.ToList();
        // Pages continue below the visible band, so overflow scrolls instead of piling onto widgets.
        for (int page = 0; result.Count < count && page < 1000; page++)
        {
            for (int column = 0; column < columns && result.Count < count; column++)
                for (int row = 0; row < rows && result.Count < count; row++)
                {
                    var cell = new LayoutRect(content.X + column * cellWidth, content.Y + page * rows * cellHeight + row * cellHeight, cellWidth, cellHeight);
                    if (cell.X + cell.Width > width) continue;
                    if (page == 0 && reserved.Any(r => r.Intersects(cell))) continue;
                    if (taken.Any(r => r.Intersects(cell))) continue;
                    result.Add(cell); taken.Add(cell);
                }
        }
        return result;
    }
}
