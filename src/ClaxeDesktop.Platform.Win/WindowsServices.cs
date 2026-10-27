using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ClaxeDesktop.Core;

namespace ClaxeDesktop.Platform.Win;

public sealed class WindowsMonitorService : IMonitorService
{
    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var result = new List<MonitorInfo>();
        Native.MonitorEnum callback = (nint monitor, nint dc, ref Native.Rect rect, nint data) =>
        {
            var info = new Native.MonitorData { Size = Marshal.SizeOf<Native.MonitorData>() };
            if (!Native.GetMonitorInfo(monitor, ref info)) return true;
            var device = new Native.DisplayDevice { Size = Marshal.SizeOf<Native.DisplayDevice>() };
            var found = Native.EnumDisplayDevices(info.Device, 0, ref device, 1);
            var id = found && !string.IsNullOrEmpty(device.Id) ? device.Id : info.Device;
            var scale = Native.GetDpiForMonitor(monitor, 0, out var dpi, out _) == 0 ? dpi / 96.0 : 1;
            result.Add(new(id, found ? device.Name : info.Device, info.Work.Left, info.Work.Top, info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top, scale, (info.Flags & 1) != 0));
            return true;
        };
        if (!Native.EnumDisplayMonitors(0, 0, callback, 0)) throw new Win32Exception();
        return result.OrderByDescending(m => m.Primary).ThenBy(m => m.X).ToList();
    }
}
public sealed class WindowsMetrics : ISystemMetrics
{
    private (ulong Idle, ulong Total)? previous;
    public SystemSample Sample()
    {
        double? cpu = null;
        if (Native.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            var total = kernel + user;
            if (previous is { } p && total > p.Total && idle >= p.Idle) cpu = Math.Clamp(100.0 * (1 - (double)(idle - p.Idle) / (total - p.Total)), 0, 100);
            previous = (idle, total);
        }
        var memory = new Native.MemoryStatus { Length = (uint)Marshal.SizeOf<Native.MemoryStatus>() };
        if (!Native.GlobalMemoryStatusEx(ref memory)) return new(cpu, null, null, null);
        return new(cpu, memory.Load, (memory.TotalPhysical - memory.AvailablePhysical) / 1073741824.0, memory.TotalPhysical / 1073741824.0);
    }
}
public sealed class WindowsLauncher : IAppLauncher
{
    public void Launch(Shortcut shortcut)
    {
        if (LaunchPolicy.IsPackagedApp(shortcut.Target))
        {
            // Store apps: Explorer activates the app by its AppUserModelID (the same thing the Start menu does).
            Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), shortcut.Target) { UseShellExecute = false, WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows) });
            return;
        }
        if (!LaunchPolicy.IsAllowed(shortcut.Target) || !File.Exists(shortcut.Target)) throw new FileNotFoundException("Uygulama bulunamadı. Kısayolu yeniden ekleyin.");
        Process.Start(new ProcessStartInfo(shortcut.Target) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(shortcut.Target) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) });
    }
}
public static class WindowPlacement
{
    public static void RaiseWithoutActivation(nint hwnd) => Native.SetWindowPos(hwnd, 0, 0, 0, 0, 0, 0x13);
    /// <summary>Puts the window in the topmost band above other topmost windows, without activating it.</summary>
    public static void RaiseTopmost(nint hwnd) => Native.SetWindowPos(hwnd, -1, 0, 0, 0, 0, 0x13);
    public static void Move(nint hwnd, int x, int y) => Native.SetWindowPos(hwnd, 0, x, y, 0, 0, 0x15);
    public static (int X, int Y) Position(nint hwnd) { if (!Native.GetWindowRect(hwnd, out var r)) throw new Win32Exception(); return (r.Left, r.Top); }
}
internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct MonitorData { public int Size; public Rect Monitor, Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct DisplayDevice { public int Size; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Name; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key; }
    [StructLayout(LayoutKind.Sequential)] internal struct MemoryStatus { public uint Length, Load; public ulong TotalPhysical, AvailablePhysical, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, AvailableExtended; }
    internal delegate bool MonitorEnum(nint monitor, nint dc, ref Rect rect, nint data);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool EnumDisplayMonitors(nint dc, nint clip, MonitorEnum callback, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetMonitorInfo(nint monitor, ref MonitorData info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool EnumDisplayDevices(string device, uint index, ref DisplayDevice info, uint flags);
    [DllImport("shcore.dll")] internal static extern int GetDpiForMonitor(nint monitor, int type, out uint x, out uint y);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetWindowRect(nint window, out Rect rect);
}
