namespace ClaxeDesktop.Core;

public sealed record AppWindow(nint Handle, uint ProcessId, string Title, string? Executable, bool Minimized)
{
    public string GroupKey => string.IsNullOrWhiteSpace(Executable) ? $"pid:{ProcessId}" : Path.GetFileName(Executable).Equals("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase) ? $"hosted:{Handle}" : Executable.ToUpperInvariant();
}
public sealed record WindowCandidate(bool Visible, bool Cloaked, bool ToolWindow, bool NoActivate, bool Owned, bool AppWindow, bool OwnProcess, string Title);
public static class WindowFilter
{
    public static bool Include(WindowCandidate w) => w.Visible && !w.Cloaked && !w.OwnProcess && !w.ToolWindow && !w.NoActivate && (!w.Owned || w.AppWindow) && !string.IsNullOrWhiteSpace(w.Title);
}
public sealed record WindowGroup(string Key, IReadOnlyList<AppWindow> Windows);
public static class WindowGrouping
{
    public static IReadOnlyList<WindowGroup> Build(IEnumerable<AppWindow> windows, bool group) => windows
        .GroupBy(w => group ? w.GroupKey : $"hwnd:{w.Handle}", StringComparer.OrdinalIgnoreCase)
        .Select(g => new WindowGroup(g.Key, g.OrderBy(w => w.Handle).ToArray())).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase).ToArray();
    public static AppWindow? Next(IReadOnlyList<AppWindow> windows, nint previous)
    {
        if (windows.Count == 0) return null;
        var index = windows.ToList().FindIndex(w => w.Handle == previous);
        return windows[(index + 1) % windows.Count];
    }
}
public interface IWindowService
{
    IReadOnlyList<AppWindow> Snapshot();
    bool Activate(AppWindow window);
    nint Foreground { get; }
    bool IsFullscreenOn(MonitorInfo monitor);
}
public static class DockVisibilityPolicy
{
    public static bool Collapse(bool autoHide, string edge, bool pointerInside, bool keyboardInside, bool menuOpen, bool dragging, TimeSpan away, int delayMs = 650)
        => autoHide && edge != "floating" && !pointerInside && !keyboardInside && !menuOpen && !dragging && away >= TimeSpan.FromMilliseconds(delayMs);
}
