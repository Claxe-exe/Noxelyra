using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ClaxeDesktop.Core;

namespace ClaxeDesktop.App;

/// <summary>The panel's AppBar work-area reservation and the optional launcher / quick settings shortcuts (WM_HOTKEY ids 741, 742).</summary>
internal sealed class DesktopBindings : IDisposable
{
    private readonly Window panel;
    private readonly HwndSource source;
    private readonly nint hwnd;
    private readonly Action launcher, controls;
    private readonly Action<string> log;
    private bool positioning, reRegistered;
    private int height;
    private Rect? reserved;
    private const int Callback = 0x8000 + 517;
    public bool Registered { get; private set; }
    public bool Hotkeys { get; private set; }
    public DesktopBindings(Window panel, Action launcher, Action controls, Action<string>? log = null)
    {
        this.panel = panel; this.launcher = launcher; this.controls = controls; this.log = log ?? (_ => { });
        hwnd = new WindowInteropHelper(panel).Handle; source = HwndSource.FromHwnd(hwnd)!; source.AddHook(Hook);
    }
    public void Configure(bool integrated, bool shortcuts, int heightDip, HotkeyGesture? launcherKey = null, HotkeyGesture? controlsKey = null)
    {
        if (!integrated && Registered) { Registered = false; reserved = null; var data = Data(); SHAppBarMessage(1, ref data); panel.Topmost = false; }
        if (integrated && !Registered)
        {
            var data = Data(); data.Callback = Callback;
            if (SHAppBarMessage(0, ref data) == 0) throw new InvalidOperationException("Windows panel için çalışma alanı ayıramadı.");
            Registered = true;
        }
        height = heightDip;
        if (Registered) { panel.Topmost = true; Position(); }
        if (Hotkeys) { UnregisterHotKey(hwnd, 741); UnregisterHotKey(hwnd, 742); Hotkeys = false; }
        if (!shortcuts) return;
        var first = launcherKey ?? HotkeyGesture.Parse(KeyboardOptions.Defaults["launcher"]);
        var second = controlsKey ?? HotkeyGesture.Parse(KeyboardOptions.Defaults["controls"]);
        if (!RegisterHotKey(hwnd, 741, HotkeyText.Modifiers(first), (uint)first.Key)) throw new InvalidOperationException(HotkeyText.Format(first) + " başka bir uygulama tarafından kullanılıyor.");
        if (!RegisterHotKey(hwnd, 742, HotkeyText.Modifiers(second), (uint)second.Key)) { UnregisterHotKey(hwnd, 741); throw new InvalidOperationException(HotkeyText.Format(second) + " başka bir uygulama tarafından kullanılıyor."); }
        Hotkeys = true;
    }
    public void Position()
    {
        if (!Registered || positioning) return;
        positioning = true;
        try
        {
            var monitor = MonitorFromWindow(hwnd, 2); var info = new MonitorData { Size = Marshal.SizeOf<MonitorData>() };
            if (!GetMonitorInfo(monitor, ref info)) return;
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(panel).DpiScaleY; var pixels = (int)Math.Ceiling(height * dpi);
            var data = Data(); data.Edge = 1; data.Rect = info.Monitor; data.Rect.Bottom = data.Rect.Top + pixels;
            SHAppBarMessage(2, ref data); data.Rect.Bottom = data.Rect.Top + pixels;
            // A top offset with no other top appbar (seen once as a 48 px gap) is usually a stale registration: start the reservation over, once.
            if (data.Rect.Top > info.Monitor.Top && !reRegistered && !TaskbarOnTop(info.Monitor))
            {
                log($"appbar-offset-{data.Rect.Top - info.Monitor.Top}px-reregister");
                reRegistered = true; reserved = null;
                var remove = Data(); SHAppBarMessage(1, ref remove);
                var again = Data(); again.Callback = Callback; SHAppBarMessage(0, ref again);
                data = Data(); data.Edge = 1; data.Rect = info.Monitor; data.Rect.Bottom = data.Rect.Top + pixels;
                SHAppBarMessage(2, ref data); data.Rect.Bottom = data.Rect.Top + pixels;
            }
            if (reserved is not { } previous || !previous.Equals(data.Rect)) { SHAppBarMessage(3, ref data); reserved = data.Rect; }
            panel.Width = (data.Rect.Right - data.Rect.Left) / dpi; panel.Height = (data.Rect.Bottom - data.Rect.Top) / dpi;
            var current = ClaxeDesktop.Platform.Win.WindowPlacement.Position(hwnd);
            if (current.X != data.Rect.Left || current.Y != data.Rect.Top) SetWindowPos(hwnd, 0, data.Rect.Left, data.Rect.Top, 0, 0, 0x15);
        }
        finally { positioning = false; }
    }
    /// <summary>Top edge of the reserved strip in screen pixels, for verification.</summary>
    internal int? ReservedTop => reserved?.Top;
    private bool TaskbarOnTop(Rect monitor)
    {
        var data = Data(); data.Window = FindWindow("Shell_TrayWnd", null);
        return SHAppBarMessage(5, ref data) != 0 && data.Edge == 1 && data.Rect.Top <= monitor.Top && data.Rect.Bottom > monitor.Top;
    }
    public void Resize(int heightDip) { height = heightDip; Position(); }
    private nint Hook(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x312 && (wParam == 741 || wParam == 742)) { if (wParam == 741) launcher(); else controls(); handled = true; }
        else if (message == Callback && wParam == 1 && !positioning) panel.Dispatcher.BeginInvoke(Position);
        else if (message == Callback && wParam == 2) panel.Topmost = lParam == 0;
        else if (message == 0x6 && Registered) { var data = Data(); data.Parameter = (wParam.ToInt64() & 0xffff) != 0 ? 1 : 0; SHAppBarMessage(6, ref data); }
        else if (message == 0x46 && Registered && reserved is { } strip && !positioning)
        {
            // WM_WINDOWPOSCHANGING: whatever tries to move the docked panel, it stays on its reserved strip.
            var pos = Marshal.PtrToStructure<WindowPos>(lParam);
            if ((pos.Flags & 0x0002) == 0 && (pos.X != strip.Left || pos.Y != strip.Top)) { pos.X = strip.Left; pos.Y = strip.Top; Marshal.StructureToPtr(pos, lParam, false); }
        }
        else if (message == 0x47 && Registered && !positioning) { var data = Data(); SHAppBarMessage(9, ref data); }
        return 0;
    }
    private AppBarData Data() => new() { Size = (uint)Marshal.SizeOf<AppBarData>(), Window = hwnd };
    public void Dispose() { Configure(false, false, height); source.RemoveHook(Hook); }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorData { public int Size; public Rect Monitor, Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct AppBarData { public uint Size; public nint Window; public uint Callback; public uint Edge; public Rect Rect; public nint Parameter; }
    [StructLayout(LayoutKind.Sequential)] private struct WindowPos { public nint Window, After; public int X, Y, Width, Height; public uint Flags; }
    [DllImport("shell32.dll")] private static extern nuint SHAppBarMessage(uint message, ref AppBarData data);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(nint monitor, ref MonitorData data);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string className, string? title);
}
