using System.Runtime.InteropServices;
namespace ClaxeDesktop.Platform.Win;

public enum BackdropKind { Mica = 2, Acrylic = 3 }

/// <summary>Windows 11 system backdrops (Mica / Acrylic) through the documented DwmSetWindowAttribute API. Older builds report unsupported and keep painted surfaces.</summary>
public static class SystemBackdrop
{
    // DWMWA_SYSTEMBACKDROP_TYPE arrived in Windows 11 22H2 (build 22621).
    public static bool Supported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);
    public static bool Apply(nint window, BackdropKind kind)
    {
        if (!Supported || window == 0) return false;
        Set(window, 20, 1); // DWMWA_USE_IMMERSIVE_DARK_MODE: dark tint and dark caption
        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        if (DwmExtendFrameIntoClientArea(window, ref margins) != 0) return false;
        return Set(window, 38, (int)kind);
    }
    /// <summary>Native anti-aliased rounded corners (DWMWA_WINDOW_CORNER_PREFERENCE).</summary>
    public static void Round(nint window, bool small = false) => Set(window, 33, small ? 3 : 2);
    /// <summary>The 1px outline Windows draws around the rounded window (DWMWA_BORDER_COLOR), as #RRGGBB.</summary>
    public static void Border(nint window, string hex)
    {
        if (hex.Length != 7 || hex[0] != '#') return;
        var r = Convert.ToInt32(hex[1..3], 16); var g = Convert.ToInt32(hex[3..5], 16); var b = Convert.ToInt32(hex[5..7], 16);
        Set(window, 34, r | g << 8 | b << 16);
    }
    private static bool Set(nint window, int attribute, int value) => DwmSetWindowAttribute(window, attribute, ref value, sizeof(int)) == 0;
    [StructLayout(LayoutKind.Sequential)] private struct Margins { public int Left, Right, Top, Bottom; }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(nint window, ref Margins margins);
}
