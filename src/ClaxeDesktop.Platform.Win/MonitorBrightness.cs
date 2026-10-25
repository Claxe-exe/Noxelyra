using System.Runtime.InteropServices;
namespace ClaxeDesktop.Platform.Win;

/// <summary>
/// Monitor brightness over DDC/CI with the documented Monitor Configuration API (dxva2). Many external monitors support it;
/// some (and most laptop panels) do not, which is reported as null. Calls are slow (tens of ms), so use them off the UI thread.
/// </summary>
public static class MonitorBrightness
{
    public sealed record Reading(int Minimum, int Current, int Maximum)
    {
        public int Percent => Maximum > Minimum ? (int)Math.Round((Current - Minimum) * 100.0 / (Maximum - Minimum)) : 0;
    }
    /// <summary>The monitor that shows the given screen point (pixels).</summary>
    public static nint MonitorAt(int x, int y) => MonitorFromPoint(new Point { X = x, Y = y }, 2);
    public static Reading? Read(nint monitor) => With(monitor, handle => GetMonitorBrightness(handle, out var min, out var current, out var max) && max > min ? new Reading((int)min, (int)current, (int)max) : null);
    public static bool Set(nint monitor, int percent) => With(monitor, handle =>
    {
        if (!GetMonitorBrightness(handle, out var min, out _, out var max) || max <= min) return false;
        return SetMonitorBrightness(handle, (uint)Math.Round(min + (max - min) * Math.Clamp(percent, 0, 100) / 100.0));
    });
    private static T? With<T>(nint monitor, Func<nint, T> action)
    {
        if (monitor == 0 || !GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out var count) || count == 0) return default;
        var monitors = new PhysicalMonitor[count];
        if (!GetPhysicalMonitorsFromHMONITOR(monitor, count, monitors)) return default;
        try { return action(monitors[0].Handle); }
        finally { DestroyPhysicalMonitors(count, monitors); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct PhysicalMonitor { public nint Handle; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description; }
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("dxva2.dll", SetLastError = true)] private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(nint monitor, out uint count);
    [DllImport("dxva2.dll", SetLastError = true)] private static extern bool GetPhysicalMonitorsFromHMONITOR(nint monitor, uint count, [Out] PhysicalMonitor[] monitors);
    [DllImport("dxva2.dll", SetLastError = true)] private static extern bool DestroyPhysicalMonitors(uint count, PhysicalMonitor[] monitors);
    [DllImport("dxva2.dll", SetLastError = true)] private static extern bool GetMonitorBrightness(nint monitor, out uint minimum, out uint current, out uint maximum);
    [DllImport("dxva2.dll", SetLastError = true)] private static extern bool SetMonitorBrightness(nint monitor, uint value);
}
