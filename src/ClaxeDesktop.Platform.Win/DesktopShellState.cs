using System.Diagnostics;
using System.Runtime.InteropServices;
using ClaxeDesktop.Core;
using Microsoft.Win32;
namespace ClaxeDesktop.Platform.Win;

/// <summary>Read-only looks at the shell for the see-through desktop. Windows' icon setting is the user's own: it is read here and never changed.</summary>
public static class DesktopShellState
{
    private const uint HideIconsMask = 0x4000;
    /// <summary>Whether "Show desktop icons" is off in Windows (SHGetSetSettings, reading only).</summary>
    public static bool WindowsIconsHidden()
    {
        var state = Marshal.AllocHGlobal(64);
        try
        {
            for (var i = 0; i < 64; i++) Marshal.WriteByte(state, i, 0);
            SHGetSetSettings(state, HideIconsMask, false);
            return (Marshal.ReadInt32(state) >> 12 & 1) == 1;
        }
        finally { Marshal.FreeHGlobal(state); }
    }
    public static bool WallpaperEngineRunning() => Process.GetProcessesByName("wallpaper64").Concat(Process.GetProcessesByName("wallpaper32")).Select(p => { p.Dispose(); return true; }).Any();
    /// <summary>Wallpaper Engine under any Steam library, or null. Steam's own files are only read.</summary>
    public static string? FindWallpaperEngine()
    {
        try
        {
            if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) is not string steam || steam.Length == 0) return null;
            var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            var libraries = new[] { steam }.Concat(File.Exists(vdf) ? SteamLibraries.Parse(File.ReadAllText(vdf)) : []);
            foreach (var library in libraries)
                foreach (var name in new[] { "wallpaper64.exe", "wallpaper32.exe" })
                {
                    var candidate = Path.GetFullPath(Path.Combine(library, "steamapps", "common", "wallpaper_engine", name));
                    if (File.Exists(candidate)) return candidate;
                }
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException) { return null; }
    }
    [DllImport("shell32.dll")] private static extern void SHGetSetSettings(nint state, uint mask, [MarshalAs(UnmanagedType.Bool)] bool set);
}
