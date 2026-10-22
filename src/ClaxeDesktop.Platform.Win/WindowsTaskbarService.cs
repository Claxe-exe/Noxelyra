using System.Runtime.InteropServices;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.Platform.Win;
public sealed class WindowsTaskbarService:ITaskbarService
{
    private static AppBarData Data(){var hwnd=FindWindow("Shell_TrayWnd",null);if(hwnd==0)throw new InvalidOperationException("Windows görev çubuğu şu anda kullanılamıyor.");return new(){Size=(uint)Marshal.SizeOf<AppBarData>(),Window=hwnd};}
    public bool AutoHide { get {var data=Data();return (SHAppBarMessage(4,ref data)&1)!=0;} }
    public void SetAutoHide(bool value){var data=Data();var state=(uint)SHAppBarMessage(4,ref data);data.Parameter=(nint)(value?state|1:state&~1u);SHAppBarMessage(10,ref data);}
    [StructLayout(LayoutKind.Sequential)]private struct Rect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]private struct AppBarData{public uint Size;public nint Window;public uint Callback;public uint Edge;public Rect Rect;public nint Parameter;}
    [DllImport("shell32.dll")]private static extern nuint SHAppBarMessage(uint message,ref AppBarData data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern nint FindWindow(string className,string? title);
}
public static class WindowsPowerState
{
    public static bool? ConservePower(){if(!GetSystemPowerStatus(out var value))return null;if(value.SystemStatusFlag==1)return true;return value.AcLineStatus switch{0=>true,1=>false,_=>null};}
    [StructLayout(LayoutKind.Sequential)]private struct PowerStatus{public byte AcLineStatus,BatteryFlag,BatteryLifePercent,SystemStatusFlag;public uint BatteryLifeTime,BatteryFullLifeTime;}
    [DllImport("kernel32.dll")]private static extern bool GetSystemPowerStatus(out PowerStatus status);
}
