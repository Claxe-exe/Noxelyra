using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ClaxeDesktop.Core;

namespace ClaxeDesktop.App;
internal sealed class DesktopBindings : IDisposable
{
    private readonly Window panel;
    private readonly HwndSource source;
    private readonly nint hwnd;
    private readonly Action launcher,controls;
    private bool positioning;
    private int height;
    private Rect? reserved;
    private const int Callback=0x8000+517;
    public bool Registered { get; private set; }
    public bool Hotkeys { get; private set; }
    public DesktopBindings(Window panel,Action launcher,Action controls)
    {
        this.panel=panel;this.launcher=launcher;this.controls=controls;hwnd=new WindowInteropHelper(panel).Handle;source=HwndSource.FromHwnd(hwnd)!;source.AddHook(Hook);
    }
    public void Configure(bool integrated,bool shortcuts,int heightDip)
    {
        if(!integrated&&Registered) { Registered=false;reserved=null;var data=Data();SHAppBarMessage(1,ref data);panel.Topmost=false; }
        if(integrated&&!Registered)
        {
            var data=Data();data.Callback=Callback;
            if(SHAppBarMessage(0,ref data)==0)throw new InvalidOperationException("Windows panel için çalışma alanı ayıramadı.");Registered=true;
        }
        height=heightDip;if(Registered) { panel.Topmost=true;Position(); }
        if(shortcuts&&!Hotkeys)
        {
            if(!RegisterHotKey(hwnd,741,0x4003,0x20))throw new InvalidOperationException("Ctrl+Alt+Space başka bir uygulama tarafından kullanılıyor.");
            if(!RegisterHotKey(hwnd,742,0x4003,0x51)) { UnregisterHotKey(hwnd,741);throw new InvalidOperationException("Ctrl+Alt+Q başka bir uygulama tarafından kullanılıyor."); }
            Hotkeys=true;
        }
        else if(!shortcuts&&Hotkeys) { UnregisterHotKey(hwnd,741);UnregisterHotKey(hwnd,742);Hotkeys=false; }
    }
    public void Position()
    {
        if(!Registered||positioning)return;positioning=true;
        try
        {
            var monitor=MonitorFromWindow(hwnd,2);var info=new MonitorData { Size=Marshal.SizeOf<MonitorData>() };if(!GetMonitorInfo(monitor,ref info))return;
            var dpi=System.Windows.Media.VisualTreeHelper.GetDpi(panel).DpiScaleY;var pixels=(int)Math.Ceiling(height*dpi);
            var data=Data();data.Edge=1;data.Rect=info.Monitor;data.Rect.Bottom=data.Rect.Top+pixels;
            SHAppBarMessage(2,ref data);data.Rect.Bottom=data.Rect.Top+pixels;
            if(reserved is not {} previous || !previous.Equals(data.Rect)) { SHAppBarMessage(3,ref data);reserved=data.Rect; }
            panel.Width=(data.Rect.Right-data.Rect.Left)/dpi;panel.Height=(data.Rect.Bottom-data.Rect.Top)/dpi;
            var current=ClaxeDesktop.Platform.Win.WindowPlacement.Position(hwnd);
            if(current.X!=data.Rect.Left||current.Y!=data.Rect.Top)SetWindowPos(hwnd,0,data.Rect.Left,data.Rect.Top,0,0,0x15);
        }
        finally { positioning=false; }
    }
    public void Resize(int heightDip) { height=heightDip;Position(); }
    private nint Hook(nint window,int message,nint wParam,nint lParam,ref bool handled)
    {
        if(message==0x312&&(wParam==741||wParam==742)) { if(wParam==741)launcher();else controls();handled=true; }
        else if(message==Callback&&wParam==1&&!positioning)panel.Dispatcher.BeginInvoke(Position);
        else if(message==Callback&&wParam==2)panel.Topmost=lParam==0;
        else if(message==0x6&&Registered) { var data=Data();data.Parameter=(wParam.ToInt64()&0xffff)!=0?1:0;SHAppBarMessage(6,ref data); }
        else if(message==0x47&&Registered&&!positioning) { var data=Data();SHAppBarMessage(9,ref data); }
        return 0;
    }
    private AppBarData Data()=>new(){Size=(uint)Marshal.SizeOf<AppBarData>(),Window=hwnd};
    public void Dispose() { Configure(false,false,height);source.RemoveHook(Hook); }
    [StructLayout(LayoutKind.Sequential)]private struct Rect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)]private struct MonitorData { public int Size;public Rect Monitor,Work;public uint Flags; }
    [StructLayout(LayoutKind.Sequential)]private struct AppBarData { public uint Size;public nint Window;public uint Callback;public uint Edge;public Rect Rect;public nint Parameter; }
    [DllImport("shell32.dll")]private static extern nuint SHAppBarMessage(uint message,ref AppBarData data);
    [DllImport("user32.dll")]private static extern nint MonitorFromWindow(nint window,uint flags);
    [DllImport("user32.dll")]private static extern bool GetMonitorInfo(nint monitor,ref MonitorData data);
    [DllImport("user32.dll")]private static extern bool SetWindowPos(nint window,nint after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll",SetLastError=true)]private static extern bool RegisterHotKey(nint window,int id,uint modifiers,uint key);
    [DllImport("user32.dll")]private static extern bool UnregisterHotKey(nint window,int id);
}
