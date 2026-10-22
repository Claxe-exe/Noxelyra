using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.Platform.Win;

public sealed class WindowsDesktopMonitor : IDisposable
{
    private readonly WinEventProc callback;
    private readonly Action changed;
    private nint hook;
    public WindowsDesktopMonitor(Action changed)
    {
        this.changed = changed; callback = OnEvent;
        hook = SetWinEventHook(3, 3, 0, callback, 0, 0, 2); // OUTOFCONTEXT | SKIPOWNPROCESS, no DLL mapping.
        if (hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    private void OnEvent(nint eventHook, uint eventId, nint window, int objectId, int childId, uint thread, uint time)
    {
        if (hook == 0) return;
        changed(); // Consumer queues/debounces on its UI dispatcher.
    }
    public static DesktopPresence Presence()
    {
        var foreground = GetForegroundWindow();
        if (foreground == 0 || !IsWindowVisible(foreground)) return DesktopPresence.Unknown;
        if (IsDesktopWindow(foreground)) return DesktopPresence.Desktop;
        GetWindowThreadProcessId(foreground, out var pid);
        if (pid == Environment.ProcessId) return DesktopPresence.OwnApplication;
        var cls = Class(foreground);
        if (cls is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "#32768") return DesktopPresence.Unknown;
        return DesktopPresence.Application;
    }
    public static string DescribeForeground() { var window = GetForegroundWindow(); GetWindowThreadProcessId(window, out var pid); return Class(window) + ", pid=" + pid + ", presence=" + Presence(); }
    private static string Class(nint window) { var text = new StringBuilder(128); GetClassName(window, text, text.Capacity); return text.ToString(); }
    private static bool IsDesktopWindow(nint window)
    {
        var shell = GetShellWindow(); if (shell == 0) return false;
        if (window == shell) return true;
        GetWindowThreadProcessId(shell, out var shellPid); GetWindowThreadProcessId(window, out var pid);
        return pid == shellPid && Class(window) is "Progman" or "WorkerW";
    }
    public static bool PlaceBehindApplications(nint surface)
    {
        nint after = 0;
        EnumWindows((window, _) =>
        {
            if (window == surface || !IsWindowVisible(window) || IsIconic(window)) return true;
            if (IsDesktopWindow(window)) return false; // Read-only shell boundary detection; never reparent/modify it.
            DwmGetWindowAttribute(window, 14, out int cloaked, sizeof(int)); if (cloaked != 0) return true;
            var style = IntPtr.Size == 8 ? GetWindowLongPtr(window, -20).ToInt64() : GetWindowLong(window, -20);
            if ((style & 0x8) == 0) after = window; // Never insert after a topmost HWND and promote the surface.
            return true;
        }, 0);
        return SetWindowPos(surface, after, 0, 0, 0, 0, 0x13);
    }
    public void Dispose() { var previous = hook; hook = 0; if (previous != 0) UnhookWinEvent(previous); GC.KeepAlive(callback); }
    private delegate void WinEventProc(nint hook, uint eventId, nint window, int objectId, int childId, uint thread, uint time);
    private delegate bool EnumProc(nint window, nint parameter);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWinEventHook(uint first, uint last, nint module, WinEventProc callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll")] private static extern nint GetShellWindow();
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, nint parameter);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(nint window, int index);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, int attribute, out int value, int size);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
}
