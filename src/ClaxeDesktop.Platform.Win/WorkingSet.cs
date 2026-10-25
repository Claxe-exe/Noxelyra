using System.ComponentModel;
using System.Runtime.InteropServices;
namespace ClaxeDesktop.Platform.Win;

/// <summary>Returns the process's idle pages to Windows (documented SetProcessWorkingSetSizeEx with -1/-1). Pages come back on demand.</summary>
public static class WorkingSet
{
    public static void Trim() { if (!SetProcessWorkingSetSizeEx(GetCurrentProcess(), -1, -1, 0)) throw new Win32Exception(); }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetProcessWorkingSetSizeEx(nint process, nint minimum, nint maximum, uint flags);
    [DllImport("kernel32.dll")] private static extern nint GetCurrentProcess();
}
