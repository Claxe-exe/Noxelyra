using System.Runtime.InteropServices;
using System.Text;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.Platform.Win;

/// <summary>Live window previews through the documented DWM thumbnail API (the same one the taskbar and Alt+Tab use).</summary>
public static class DwmThumbnail
{
    public static nint Register(nint destination, nint source) => DwmRegisterThumbnail(destination, source, out var handle) == 0 ? handle : 0;
    public static (int Width, int Height) SourceSize(nint thumbnail) => DwmQueryThumbnailSourceSize(thumbnail, out var size) == 0 ? (size.X, size.Y) : (0, 0);
    /// <summary>Places the preview at <paramref name="area"/> in destination client pixels.</summary>
    public static void Show(nint thumbnail, int left, int top, int right, int bottom, byte opacity = 255)
    {
        var props = new Properties { Flags = 0x1 | 0x4 | 0x8 | 0x10, Destination = new Rect { Left = left, Top = top, Right = right, Bottom = bottom }, Opacity = opacity, Visible = true, SourceClientAreaOnly = false };
        DwmUpdateThumbnailProperties(thumbnail, ref props);
    }
    /// <summary>Shows only the <paramref name="source"/> part of the window (window-rect pixels) at the destination rectangle.</summary>
    public static void ShowCropped(nint thumbnail, (int Left, int Top, int Right, int Bottom) destination, (int Left, int Top, int Right, int Bottom) source)
    {
        var props = new Properties { Flags = 0x1 | 0x2 | 0x4 | 0x8 | 0x10, Destination = new Rect { Left = destination.Left, Top = destination.Top, Right = destination.Right, Bottom = destination.Bottom },
            Source = new Rect { Left = source.Left, Top = source.Top, Right = source.Right, Bottom = source.Bottom }, Opacity = 255, Visible = true, SourceClientAreaOnly = false };
        DwmUpdateThumbnailProperties(thumbnail, ref props);
    }
    public static void Hide(nint thumbnail) { var props = new Properties { Flags = 0x8, Visible = false }; DwmUpdateThumbnailProperties(thumbnail, ref props); }
    public static void Unregister(nint thumbnail) { if (thumbnail != 0) DwmUnregisterThumbnail(thumbnail); }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Size { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Properties { public uint Flags; public Rect Destination, Source; public byte Opacity; [MarshalAs(UnmanagedType.Bool)] public bool Visible; [MarshalAs(UnmanagedType.Bool)] public bool SourceClientAreaOnly; }
    [DllImport("dwmapi.dll")] private static extern int DwmRegisterThumbnail(nint destination, nint source, out nint thumbnail);
    [DllImport("dwmapi.dll")] private static extern int DwmUnregisterThumbnail(nint thumbnail);
    [DllImport("dwmapi.dll")] private static extern int DwmQueryThumbnailSourceSize(nint thumbnail, out Size size);
    [DllImport("dwmapi.dll")] private static extern int DwmUpdateThumbnailProperties(nint thumbnail, ref Properties properties);
}

/// <summary>Reading and placing other apps' top-level windows with documented user32/DWM calls, for tiling.</summary>
public static class WindowOps
{
    public sealed record Info(nint Handle, LayoutRect Bounds, nint Monitor, bool Resizable, bool Minimized, bool Maximized, bool Captioned, string ClassName);
    public static Info? Read(nint hwnd)
    {
        if (!IsWindow(hwnd) || !GetWindowRect(hwnd, out var rect)) return null;
        var style = GetWindowLongPtr(hwnd, -16).ToInt64();
        var name = new StringBuilder(128); GetClassName(hwnd, name, name.Capacity);
        return new(hwnd, new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top), MonitorFromWindow(hwnd, 2), (style & 0x00040000) != 0, IsIconic(hwnd), IsZoomed(hwnd), (style & 0x00C00000) == 0x00C00000, name.ToString());
    }
    /// <summary>Work area of a monitor handle in screen pixels.</summary>
    public static LayoutRect WorkArea(nint monitor)
    {
        var info = new MonitorData { Size = Marshal.SizeOf<MonitorData>() };
        return GetMonitorInfo(monitor, ref info) ? new(info.Work.Left, info.Work.Top, info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top) : new(0, 0, 0, 0);
    }
    public static LayoutRect MonitorBounds(nint monitor)
    {
        var info = new MonitorData { Size = Marshal.SizeOf<MonitorData>() };
        return GetMonitorInfo(monitor, ref info) ? new(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right - info.Monitor.Left, info.Monitor.Bottom - info.Monitor.Top) : new(0, 0, 0, 0);
    }
    /// <summary>
    /// Moves a window so its visible frame (DWM extended frame bounds) fills <paramref name="target"/>; Windows 10/11 windows
    /// carry invisible resize borders that would otherwise leave uneven gaps. Restores maximized windows first.
    /// </summary>
    public static bool Place(nint hwnd, LayoutRect target)
    {
        if (IsZoomed(hwnd) || IsIconic(hwnd)) ShowWindow(hwnd, 9);
        if (!GetWindowRect(hwnd, out var outer)) return false;
        int dl = 0, dt = 0, dr = 0, db = 0;
        if (DwmGetWindowAttribute(hwnd, 9, out var frame, Marshal.SizeOf<Rect>()) == 0) { dl = frame.Left - outer.Left; dt = frame.Top - outer.Top; dr = outer.Right - frame.Right; db = outer.Bottom - frame.Bottom; }
        return SetWindowPos(hwnd, 0, (int)target.X - dl, (int)target.Y - dt, (int)target.Width + dl + dr, (int)target.Height + dt + db, 0x0004 | 0x0010 | 0x0200);
    }
    public static void Close(nint hwnd) => PostMessage(hwnd, 0x0010, 0, 0);
    public static void Maximize(nint hwnd, bool on) => ShowWindow(hwnd, on ? 3 : 9);
    public static bool Focus(nint hwnd) { if (IsIconic(hwnd)) ShowWindow(hwnd, 9); return SetForegroundWindow(hwnd); }
    public static nint Foreground => GetForegroundWindow();
    /// <summary>Where the visible frame (what screen capture sees) starts inside the window rectangle (what DWM thumbnails use).</summary>
    public static (int X, int Y) FrameOffset(nint hwnd) =>
        GetWindowRect(hwnd, out var outer) && DwmGetWindowAttribute(hwnd, 9, out var frame, Marshal.SizeOf<Rect>()) == 0 ? (frame.Left - outer.Left, frame.Top - outer.Top) : (0, 0);
    /// <summary>The id of the process that owns the window, or 0.</summary>
    public static int ProcessId(nint hwnd) { GetWindowThreadProcessId(hwnd, out var pid); return (int)pid; }
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    public static bool IsAlive(nint hwnd) => IsWindow(hwnd) && IsWindowVisible(hwnd);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorData { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsZoomed(nint hwnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hwnd, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(nint monitor, ref MonitorData data);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint hwnd, uint message, nint w, nint l);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out Rect value, int size);
}
