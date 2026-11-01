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
/// <summary>
/// Whether a running window belongs to a dock icon. An exact path is not enough: a Store app is pinned by its app id while its window
/// runs from WindowsApps, a launcher starts a different executable, and an updater moves the app to a new versioned folder.
/// </summary>
public static class DockMatch
{
    /// <summary>The window runs exactly what the icon points at (or what its shortcut resolves to).</summary>
    public static bool Exact(string target, string? executable) => executable is not null && string.Equals(target, executable, StringComparison.OrdinalIgnoreCase);
    /// <summary>The same app by a looser rule. Exact owners go first, so a window pinned on its own never lands here.</summary>
    public static bool Likely(string name, string target, string? executable)
    {
        if (string.IsNullOrWhiteSpace(executable) || Path.GetFileName(executable).Equals("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase)) return false;
        if (LaunchPolicy.IsPackagedApp(target)) return PackagedApp(target, executable);
        // A newer version of the same app: same file name, same folder two levels up (…\Discord\app-1.0.9266\Discord.exe).
        if (string.Equals(Path.GetFileName(target), Path.GetFileName(executable), StringComparison.OrdinalIgnoreCase) && Path.GetDirectoryName(Path.GetDirectoryName(target)) is { Length: > 3 } family && string.Equals(family, Path.GetDirectoryName(Path.GetDirectoryName(executable)), StringComparison.OrdinalIgnoreCase)) return true;
        // A launcher: the icon is named after the program, and the program or its folder carries that name. Two levels at most, so a game under Steam\steamapps never counts as Steam.
        var wanted = Compact(name); if (wanted.Length < 3) return false;
        if (Compact(Path.GetFileNameWithoutExtension(executable)) == wanted) return true;
        var parent = Path.GetDirectoryName(executable);
        return parent is not null && (Compact(Path.GetFileName(parent)) == wanted || Compact(Path.GetFileName(Path.GetDirectoryName(parent) ?? "")) == wanted);
    }
    // shell:AppsFolder\Name_hash!App runs from WindowsApps\Name_version_arch__hash\…
    private static bool PackagedApp(string target, string executable)
    {
        var family = target[LaunchPolicy.AppsFolderPrefix.Length..].Split('!')[0]; var split = family.LastIndexOf('_'); if (split <= 0) return false;
        string package = family[..split], publisher = family[(split + 1)..];
        var marker = executable.IndexOf(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase); if (marker < 0) return false;
        var folder = executable[(marker + 13)..].Split('\\')[0];
        return folder.StartsWith(package + "_", StringComparison.OrdinalIgnoreCase) && folder.EndsWith("__" + publisher, StringComparison.OrdinalIgnoreCase);
    }
    private static string Compact(string text) => new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}
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
