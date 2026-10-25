using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;

internal sealed class DockShortcutBinding : IDisposable
{
    private readonly nint hwnd;
    private readonly HwndSource source;
    private readonly Action action;
    internal bool Enabled { get; private set; }
    internal DockShortcutBinding(Window window, Action action)
    {
        this.action = action;
        hwnd = new WindowInteropHelper(window).Handle;
        source = HwndSource.FromHwnd(hwnd)!;
        source.AddHook(Hook);
    }
    /// <summary>Registers the dock shortcut (Alt+K unless the user chose another) under the fixed WM_HOTKEY id 754.</summary>
    internal void Register(HotkeyGesture? gesture = null)
    {
        var key = gesture ?? HotkeyGesture.Parse(KeyboardOptions.Defaults["dock"]);
        if (Enabled) { UnregisterHotKey(hwnd, 754); Enabled = false; }
        if (!RegisterHotKey(hwnd, 754, HotkeyText.Modifiers(key), (uint)key.Key))
            throw new InvalidOperationException(HotkeyText.Format(key) + " başka bir uygulama tarafından kullanılıyor. Bildirim alanındaki Noxelyra menüsünden ‘Dock'u öne getir’ seçeneğini kullanabilir ya da kısayolu değiştirebilirsin.");
        Enabled = true;
    }
    private nint Hook(nint window, int message, nint w, nint l, ref bool handled)
    {
        if (message == 0x312 && w == 754) { action(); handled = true; }
        return 0;
    }
    public void Dispose()
    {
        if (Enabled) UnregisterHotKey(hwnd, 754);
        Enabled = false;
        source.RemoveHook(Hook);
    }
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint hwnd, int id);
}
