using System.Windows.Interop;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

/// <summary>Alt+Tab switcher and dwindle tiling wiring.</summary>
public sealed partial class DesktopController
{
    private SwitcherWindow? switcher;
    private TilingService? tiling;
    internal bool ActivateWindow(AppWindow window) => windowService.Activate(window);
    internal bool SwitcherOpen => switcher?.Open == true;
    internal void Switcher(string action)
    {
        switcher ??= new SwitcherWindow(this, icons);
        switch (action)
        {
            case "switcher.open" or "switcher.openPrev":
                IReadOnlyList<AppWindow> list;
                try { list = windowService.Snapshot(); } catch (System.ComponentModel.Win32Exception) { list = []; }
                switcher.Present(list, action == "switcher.openPrev"); break;
            case "switcher.next": switcher.Move(1); break;
            case "switcher.prev": switcher.Move(-1); break;
            case "switcher.commit": switcher.Commit(); break;
            case "switcher.cancel": switcher.Cancel(); break;
        }
    }
    /// <summary>Starts or stops tiling to match the saved option.</summary>
    private void ConfigureTiling()
    {
        var wanted = !safe && !exiting && committed.Keyboard.Tiling;
        if (!wanted) { tiling?.Dispose(); tiling = null; return; }
        if (tiling is not null) { tiling.Schedule(); return; }
        try { tiling = new(windowService, TilingArea, () => committed.Keyboard.TilingGap * (ViewModel.Monitors.FirstOrDefault()?.Scale ?? 1)); Log.Write("tiling-started"); }
        catch (InvalidOperationException ex) { Log.Write("tiling-failed", ex); ViewModel.Status = "Döşeme başlatılamadı: " + ex.Message; }
    }
    // The monitor's work area minus Noxelyra's own floating bar (top) and visible dock (bottom), in screen pixels.
    private LayoutRect TilingArea(nint monitor)
    {
        var area = WindowOps.WorkArea(monitor);
        double top = area.Y, bottom = area.Y + area.Height;
        foreach (var window in windows)
        {
            if (!window.IsVisible || window is not (PanelWindow or DockWindow)) continue;
            var handle = new WindowInteropHelper(window).Handle;
            var info = WindowOps.Read(handle); if (info is null || info.Monitor != monitor) continue;
            var b = info.Bounds; if (b.Height < 12) continue;
            if (window is PanelWindow && b.Y < area.Y + area.Height / 3) top = Math.Max(top, b.Y + b.Height);
            if (window is DockWindow && b.Y > area.Y + area.Height / 2) bottom = Math.Min(bottom, b.Y);
        }
        return area with { Y = top, Height = Math.Max(200, bottom - top) };
    }
    private void TileCommand(string action)
    {
        if (tiling is null) return;
        if (tiling.Command(action) is { } message) ViewModel.Status = message;
    }
}
