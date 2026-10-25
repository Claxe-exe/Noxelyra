using System.Windows.Threading;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

/// <summary>
/// Hyprland-style dwindle tiling per monitor. New normal windows join the layout, closed or minimized ones leave it.
/// Dialogs, tool windows, non-resizable windows (most games), maximized/fullscreen and user-floated windows stay free.
/// Windows Noxelyra cannot move (elevated apps) are remembered and left alone.
/// </summary>
internal sealed class TilingService : IDisposable
{
    private const int MaxTiles = 8;
    private readonly IWindowService windows;
    private readonly Func<nint, LayoutRect> usableArea;
    private readonly Func<double> gap;
    private readonly Dictionary<nint, List<nint>> order = [];
    private readonly HashSet<nint> floating = [], immovable = [];
    // Where tiling last put each window; a window found elsewhere was moved by the user or by Windows Snap.
    private readonly Dictionary<nint, LayoutRect> placed = [];
    private readonly DispatcherTimer debounce = new() { Interval = TimeSpan.FromMilliseconds(140) };
    private readonly WindowEvents events;
    private bool disposed;
    public TilingService(IWindowService windows, Func<nint, LayoutRect> usableArea, Func<double> gap)
    {
        this.windows = windows; this.usableArea = usableArea; this.gap = gap;
        debounce.Tick += (_, _) => { debounce.Stop(); Retile(); };
        // Foreground changes do not change the layout; everything else (show, hide, destroy, minimize, move end) may.
        // A finished drag or resize (including dragging to a Snap layout) hands that window back to the user.
        events = new((hwnd, type) => { if (type == 0x000B && placed.Remove(hwnd)) floating.Add(hwnd); if (type != 0x0003) Schedule(); });
        Retile();
    }
    internal void Schedule() { if (disposed) return; debounce.Stop(); debounce.Start(); }
    private static bool Tileable(WindowOps.Info info)
    {
        // #32770 is the standard dialog class; tiny or non-resizable windows are popups, splash screens or games.
        if (!info.Resizable || info.Minimized || info.Maximized || info.ClassName is "#32770" or "Windows.UI.Core.CoreWindow") return false;
        if (info.Bounds.Width < 320 || info.Bounds.Height < 240) return false;
        var screen = WindowOps.MonitorBounds(info.Monitor);
        return !(info.Bounds.X <= screen.X && info.Bounds.Y <= screen.Y && info.Bounds.Width >= screen.Width && info.Bounds.Height >= screen.Height);
    }
    internal void Retile()
    {
        if (disposed) return;
        IReadOnlyList<AppWindow> snapshot;
        try { snapshot = windows.Snapshot(); } catch (System.ComponentModel.Win32Exception) { return; }
        // Never fight Windows Snap (Win+arrows, snap layouts) or a manual move: such windows leave tiling.
        foreach (var (handle, rect) in placed.ToArray())
        {
            if (WindowOps.Read(handle) is not { } now) { placed.Remove(handle); continue; }
            var b = now.Bounds;
            if (Math.Abs(b.X - rect.X) > 24 || Math.Abs(b.Y - rect.Y) > 24 || Math.Abs(b.Width - rect.Width) > 32 || Math.Abs(b.Height - rect.Height) > 32) { placed.Remove(handle); floating.Add(handle); }
        }
        var live = new Dictionary<nint, WindowOps.Info>();
        foreach (var window in snapshot)
            if (!floating.Contains(window.Handle) && !immovable.Contains(window.Handle) && WindowOps.Read(window.Handle) is { } info && Tileable(info)) live[window.Handle] = info;
        // Keep existing order, drop vanished windows, follow windows the user dragged to another monitor, append newcomers.
        foreach (var list in order.Values) list.RemoveAll(h => !live.ContainsKey(h));
        foreach (var (handle, info) in live)
        {
            var current = order.FirstOrDefault(p => p.Value.Contains(handle));
            if (current.Value is not null && current.Key == info.Monitor) continue;
            current.Value?.Remove(handle);
            if (!order.TryGetValue(info.Monitor, out var list)) order[info.Monitor] = list = [];
            list.Add(handle);
        }
        foreach (var (monitor, list) in order)
        {
            var tiles = list.Take(MaxTiles).ToList(); if (tiles.Count == 0) continue;
            var rects = Dwindle.Arrange(usableArea(monitor), tiles.Count, gap());
            for (int i = 0; i < tiles.Count; i++)
            {
                var have = live[tiles[i]].Bounds; var want = rects[i];
                if (Math.Abs(have.X - want.X) < 3 && Math.Abs(have.Y - want.Y) < 3 && Math.Abs(have.Width - want.Width) < 16 && Math.Abs(have.Height - want.Height) < 16) { placed[tiles[i]] = have; continue; }
                if (WindowOps.Place(tiles[i], want)) placed[tiles[i]] = WindowOps.Read(tiles[i])?.Bounds ?? want;
                else { immovable.Add(tiles[i]); list.Remove(tiles[i]); }
            }
        }
    }
    private (List<nint> List, int Index, IReadOnlyList<LayoutRect> Rects)? Locate(nint handle)
    {
        foreach (var (monitor, list) in order)
        {
            var index = list.IndexOf(handle);
            if (index < 0 || index >= MaxTiles) continue;
            return (list, index, Dwindle.Arrange(usableArea(monitor), Math.Min(list.Count, MaxTiles), gap()));
        }
        return null;
    }
    internal string? Command(string action)
    {
        var focused = WindowOps.Foreground;
        switch (action)
        {
            case "tile.close": if (focused != 0) WindowOps.Close(focused); return null;
            case "tile.float":
                if (!floating.Remove(focused)) floating.Add(focused);
                else { Retile(); return "Pencere yeniden döşendi."; }
                // A freed window drops to a centred, smaller size so it is visibly out of the layout.
                if (WindowOps.Read(focused) is { } info) { var area = usableArea(info.Monitor); WindowOps.Place(focused, new(area.X + area.Width * .2, area.Y + area.Height * .15, area.Width * .6, area.Height * .7)); }
                Retile(); return "Pencere döşemeden çıkarıldı (tekrar basınca geri döner).";
            case "tile.fullscreen":
                if (WindowOps.Read(focused) is not { } current) return null;
                WindowOps.Maximize(focused, !current.Maximized); Schedule(); return null;
        }
        if (Locate(focused) is not { } spot) return "Odaktaki pencere döşenmiyor.";
        var direction = action switch { "tile.focusLeft" or "tile.moveLeft" => 0, "tile.focusUp" or "tile.moveUp" => 1, "tile.focusRight" or "tile.moveRight" => 2, _ => 3 };
        var neighbour = Dwindle.Neighbour(spot.Rects, spot.Index, direction);
        if (neighbour < 0) return null;
        if (action.StartsWith("tile.focus", StringComparison.Ordinal)) WindowOps.Focus(spot.List[neighbour]);
        else { (spot.List[spot.Index], spot.List[neighbour]) = (spot.List[neighbour], spot.List[spot.Index]); Retile(); }
        return null;
    }
    public void Dispose() { disposed = true; debounce.Stop(); events.Dispose(); }
}
