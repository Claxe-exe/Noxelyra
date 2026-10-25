namespace ClaxeDesktop.Core;

/// <summary>
/// Hyprland-style "dwindle": the first window takes the whole area; every later window splits the last remaining
/// region in half along its longer side, so windows spiral inward. Gaps sit between windows and around the edge.
/// </summary>
public static class Dwindle
{
    public static IReadOnlyList<LayoutRect> Arrange(LayoutRect area, int count, double gap)
    {
        if (count < 0 || count > 64) throw new ArgumentOutOfRangeException(nameof(count));
        if (!(area.Width > 0 && area.Height > 0) || !double.IsFinite(gap) || gap < 0) throw new ArgumentOutOfRangeException(nameof(area));
        var result = new List<LayoutRect>(count);
        if (count == 0) return result;
        var free = new LayoutRect(area.X + gap, area.Y + gap, Math.Max(1, area.Width - gap * 2), Math.Max(1, area.Height - gap * 2));
        for (int i = 0; i < count; i++)
        {
            if (i == count - 1) { result.Add(free); break; }
            if (free.Width >= free.Height)
            {
                var half = (free.Width - gap) / 2;
                result.Add(free with { Width = half });
                free = free with { X = free.X + half + gap, Width = free.Width - half - gap };
            }
            else
            {
                var half = (free.Height - gap) / 2;
                result.Add(free with { Height = half });
                free = free with { Y = free.Y + half + gap, Height = free.Height - half - gap };
            }
        }
        return result;
    }

    /// <summary>Index of the nearest tile in a direction from tile <paramref name="from"/>, or -1. Direction: 0 left, 1 up, 2 right, 3 down.</summary>
    public static int Neighbour(IReadOnlyList<LayoutRect> tiles, int from, int direction)
    {
        if (from < 0 || from >= tiles.Count) return -1;
        var a = tiles[from]; double ax = a.X + a.Width / 2, ay = a.Y + a.Height / 2;
        int best = -1; double bestScore = double.MaxValue;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (i == from) continue;
            var b = tiles[i]; double dx = b.X + b.Width / 2 - ax, dy = b.Y + b.Height / 2 - ay;
            var ahead = direction switch { 0 => -dx, 1 => -dy, 2 => dx, _ => dy };
            if (ahead <= 1) continue;
            var side = direction is 0 or 2 ? Math.Abs(dy) : Math.Abs(dx);
            var score = ahead + side * 2;
            if (score < bestScore) { bestScore = score; best = i; }
        }
        return best;
    }
}
