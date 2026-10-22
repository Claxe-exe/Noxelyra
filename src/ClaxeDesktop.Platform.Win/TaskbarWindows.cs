using System.Runtime.InteropServices;
using System.Text;
namespace ClaxeDesktop.Platform.Win;

/// <summary>Shows or hides Explorer's taskbar windows on every monitor. Explorer keeps running; Start and Win shortcuts still work.</summary>
public static class TaskbarWindows
{
    public static bool AnyVisible() { var visible = false; Each(window => visible |= IsWindowVisible(window)); return visible; }
    public static void SetVisible(bool visible) => Each(window => ShowWindow(window, visible ? 8 : 0));
    private static void Each(Action<nint> action)
    {
        EnumWindows((window, _) =>
        {
            var name = new StringBuilder(64); GetClassName(window, name, name.Capacity);
            if (name.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") action(window);
            return true;
        }, 0);
    }
    private delegate bool EnumProc(nint window, nint data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc proc, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder name, int size);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int command);
}
