using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ClaxeDesktop.Core;

namespace ClaxeDesktop.Platform.Win;
public sealed class WindowsWindowService : IWindowService
{
    public nint Foreground => Api.GetForegroundWindow();
    public IReadOnlyList<AppWindow> Snapshot()
    {
        var windows = new List<AppWindow>();
        if (!Api.EnumWindows((hwnd, _) =>
        {
            Api.GetWindowThreadProcessId(hwnd, out var pid);
            var title = new StringBuilder(512); Api.GetWindowText(hwnd, title, title.Capacity);
            var style = IntPtr.Size == 8 ? Api.GetWindowLongPtr(hwnd, -20).ToInt64() : Api.GetWindowLong(hwnd, -20);
            Api.DwmGetWindowAttribute(hwnd, 14, out var cloaked, sizeof(int));
            var candidate = new WindowCandidate(Api.IsWindowVisible(hwnd), cloaked != 0, (style & 0x80) != 0, (style & 0x08000000) != 0, Api.GetWindow(hwnd, 4) != 0, (style & 0x40000) != 0, pid == Environment.ProcessId, title.ToString());
            if (!WindowFilter.Include(candidate)) return true;
            windows.Add(new(hwnd, pid, title.ToString(), PathOf(pid), Api.IsIconic(hwnd))); return true;
        }, 0)) throw new Win32Exception();
        // Forget processes that have no windows any more so a reused id cannot inherit an old path.
        foreach (var gone in Paths.Keys.Except(windows.Select(w => w.ProcessId)).ToArray()) Paths.TryRemove(gone, out _);
        return windows;
    }
    // Process image paths are cached per id and read with QueryFullProcessImageName (limited query rights), which is
    // much faster than Process.MainModule and also works for elevated processes. Alt+Tab snapshots run on key press.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint, string?> Paths = new();
    private static string? PathOf(uint pid)
    {
        if (Paths.TryGetValue(pid, out var cached)) return cached;
        string? path = null; var process = Api.OpenProcess(0x1000, false, pid);
        if (process != 0)
        {
            try { var buffer = new StringBuilder(1024); var size = buffer.Capacity; if (Api.QueryFullProcessImageName(process, 0, buffer, ref size)) path = buffer.ToString(); }
            finally { Api.CloseHandle(process); }
        }
        Paths[pid] = path; return path;
    }
    public bool Activate(AppWindow window)
    {
        if (!Api.IsWindow(window.Handle)) return false;
        Api.GetWindowThreadProcessId(window.Handle, out var pid);
        if (pid != window.ProcessId) return false;
        if (Api.IsIconic(window.Handle)) Api.ShowWindowAsync(window.Handle, 9);
        if (Api.SetForegroundWindow(window.Handle)) return true;
        // Windows only lets the process that sent the last input change the foreground window. An unassigned key (0xE8)
        // makes Noxelyra that process without any visible effect, then the switch is allowed.
        KeyboardHook.Chord(0xE8);
        return Api.SetForegroundWindow(window.Handle);
    }
    public bool IsFullscreenOn(MonitorInfo monitor)
    {
        var hwnd = Foreground;
        if (hwnd == 0 || !Api.IsWindowVisible(hwnd) || Api.IsIconic(hwnd)) return false;
        Api.GetWindowThreadProcessId(hwnd, out var pid); if (pid == Environment.ProcessId) return false;
        var cls = new StringBuilder(128); Api.GetClassName(hwnd, cls, cls.Capacity);
        if (cls.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;
        var handle = Api.MonitorFromWindow(hwnd, 2);
        var data = new Native.MonitorData { Size = Marshal.SizeOf<Native.MonitorData>() };
        if (!Native.GetMonitorInfo(handle, ref data) || !Native.GetWindowRect(hwnd, out var rect)) return false;
        // Compare against complete monitor bounds, not work area: maximized windows are not fullscreen.
        return data.Work.Left == monitor.X && data.Work.Top == monitor.Y && rect.Left <= data.Monitor.Left + 1 && rect.Top <= data.Monitor.Top + 1 && rect.Right >= data.Monitor.Right - 1 && rect.Bottom >= data.Monitor.Bottom - 1;
    }
    private static class Api
    {
        internal delegate bool EnumCallback(nint hwnd, nint data);
        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumCallback callback, nint data);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern nint OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder name, ref int size);
        [DllImport("kernel32.dll")] internal static extern bool CloseHandle(nint handle);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint hwnd);
        [DllImport("user32.dll")] internal static extern bool IsWindow(nint hwnd);
        [DllImport("user32.dll")] internal static extern bool IsIconic(nint hwnd);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowText(nint hwnd, StringBuilder text, int count);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(nint hwnd, StringBuilder text, int count);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern nint GetWindowLongPtr(nint hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] internal static extern int GetWindowLong(nint hwnd, int index);
        [DllImport("user32.dll")] internal static extern nint GetWindow(nint hwnd, uint cmd);
        [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint hwnd);
        [DllImport("user32.dll")] internal static extern bool ShowWindowAsync(nint hwnd, int cmd);
        [DllImport("user32.dll")] internal static extern nint MonitorFromWindow(nint hwnd, uint flags);
        [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
    }
}
