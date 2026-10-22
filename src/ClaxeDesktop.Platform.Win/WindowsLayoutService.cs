using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.Platform.Win;
public sealed class WindowsLayoutService
{
    [StructLayout(LayoutKind.Sequential)]public struct Point{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]public struct Rect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]public struct Placement{public int Length;public uint Flags,Show;public Point Min,Max;public Rect Normal;}
    public sealed record Snapshot(AppWindow Window,long Started,Placement Placement);
    private static bool Identity(Snapshot value)
    {
        if(!IsWindow(value.Window.Handle))return false;GetWindowThreadProcessId(value.Window.Handle,out var pid);if(pid!=value.Window.ProcessId||pid==Environment.ProcessId)return false;
        try{using var process=Process.GetProcessById((int)pid);return process.StartTime.ToUniversalTime().Ticks==value.Started;}catch(Exception ex)when(ex is Win32Exception or ArgumentException or InvalidOperationException){return false;}
    }
    public Snapshot Capture(AppWindow window)
    {
        if(!IsWindow(window.Handle)||window.ProcessId==Environment.ProcessId)throw new InvalidOperationException("Pencere kapandı veya Claxe'a ait.");
        GetWindowThreadProcessId(window.Handle,out var pid);if(pid!=window.ProcessId)throw new InvalidOperationException("Pencere kimliği değişti.");
        var style=IntPtr.Size==8?GetWindowLongPtr(window.Handle,-16).ToInt64():GetWindowLong(window.Handle,-16);
        if((style&0x40000)==0)throw new InvalidOperationException("Bu pencere yeniden boyutlandırmayı desteklemiyor.");
        using var process=Process.GetProcessById((int)pid);var placement=new Placement{Length=Marshal.SizeOf<Placement>()};if(!GetWindowPlacement(window.Handle,ref placement))throw new Win32Exception();return new(window,process.StartTime.ToUniversalTime().Ticks,placement);
    }
    public async Task MoveAsync(Snapshot window,PixelRect target)
    {
        if(!Identity(window))throw new InvalidOperationException("Pencere artık kullanılabilir değil.");
        var placement=window.Placement;if(placement.Show is 2 or 3 or 6 or 7){placement.Show=4;placement.Flags|=4;if(!SetWindowPlacement(window.Window.Handle,ref placement))throw new Win32Exception();await Task.Delay(100);}
        if(!Identity(window)||!SetWindowPos(window.Window.Handle,0,target.X,target.Y,target.Width,target.Height,0x4014))throw new Win32Exception();
        for(int i=0;i<10;i++){await Task.Delay(50);if(!Identity(window))throw new InvalidOperationException("Pencere kapandı.");if(GetWindowRect(window.Window.Handle,out var r)&&Math.Abs(r.Left-target.X)<=8&&Math.Abs(r.Top-target.Y)<=8&&Math.Abs(r.Right-r.Left-target.Width)<=8&&Math.Abs(r.Bottom-r.Top-target.Height)<=8)return;}
        throw new InvalidOperationException("Uygulama istenen boyutu kabul etmedi; en küçük boyut veya erişim sınırı olabilir.");
    }
    public bool Restore(Snapshot value){if(!Identity(value))return false;var p=value.Placement;p.Flags|=4;return SetWindowPlacement(value.Window.Handle,ref p);}
    public PixelRect Bounds(AppWindow window){var captured=Capture(window);if(!Identity(captured)||!GetWindowRect(window.Handle,out var r))throw new Win32Exception();return new(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top);}
    [DllImport("user32.dll")]private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(nint window,out uint pid);
    [DllImport("user32.dll",SetLastError=true)]private static extern bool GetWindowPlacement(nint window,ref Placement placement);
    [DllImport("user32.dll",SetLastError=true)]private static extern bool SetWindowPlacement(nint window,ref Placement placement);
    [DllImport("user32.dll",SetLastError=true)]private static extern bool SetWindowPos(nint window,nint after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")]private static extern bool GetWindowRect(nint window,out Rect rect);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]private static extern nint GetWindowLongPtr(nint window,int index);
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")]private static extern int GetWindowLong(nint window,int index);
}
