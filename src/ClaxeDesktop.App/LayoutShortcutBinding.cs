using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace ClaxeDesktop.App;
internal sealed class LayoutShortcutBinding:IDisposable
{
    private readonly nint hwnd;private readonly HwndSource source;private readonly Action action;public bool Enabled{get;private set;}
    public LayoutShortcutBinding(Window panel,Action action){this.action=action;hwnd=new WindowInteropHelper(panel).Handle;source=HwndSource.FromHwnd(hwnd)!;source.AddHook(Hook);}
    public void Configure(bool enabled){if(enabled&&!Enabled){if(!RegisterHotKey(hwnd,753,0x4003,0x4c))throw new InvalidOperationException("Ctrl+Alt+L başka bir uygulama tarafından kullanılıyor.");Enabled=true;}else if(!enabled&&Enabled){UnregisterHotKey(hwnd,753);Enabled=false;}}
    private nint Hook(nint window,int message,nint w,nint l,ref bool handled){if(message==0x312&&w==753){action();handled=true;}return 0;}
    public void Dispose(){Configure(false);source.RemoveHook(Hook);}
    [DllImport("user32.dll")]private static extern bool RegisterHotKey(nint window,int id,uint modifiers,uint key);
    [DllImport("user32.dll")]private static extern bool UnregisterHotKey(nint window,int id);
}
