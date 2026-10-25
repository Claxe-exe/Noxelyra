using System.Runtime.InteropServices;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;

/// <summary>
/// The emergency exit shortcut lives on its own thread with its own message queue (RegisterHotKey with no window),
/// so it still answers when Noxelyra's UI thread is stuck. The caller decides what "exit" means.
/// </summary>
internal sealed class EmergencyHotkey : IDisposable
{
    private const int Id = 760;
    private readonly Thread thread;
    private readonly ManualResetEventSlim ready = new();
    private readonly Action pressed;
    private uint threadId;
    internal string? Error { get; private set; }
    internal EmergencyHotkey(HotkeyGesture gesture, Action pressed)
    {
        this.pressed = pressed;
        thread = new Thread(() => Run(gesture)) { IsBackground = true, Name = "Noxelyra emergency" }; thread.Start();
        ready.Wait(3000);
    }
    private void Run(HotkeyGesture gesture)
    {
        threadId = GetCurrentThreadId();
        if (!RegisterHotKey(0, Id, HotkeyText.Modifiers(gesture), (uint)gesture.Key)) Error = HotkeyText.Format(gesture) + " başka bir uygulama tarafından kullanılıyor; acil çıkış için başka bir kısayol seç.";
        ready.Set();
        if (Error is not null) return;
        while (GetMessage(out var message, 0, 0, 0) > 0)
            if (message.Message == 0x312 && message.W == Id) pressed();
        UnregisterHotKey(0, Id);
    }
    public void Dispose() { if (threadId != 0) PostThreadMessage(threadId, 0x0012, 0, 0); thread.Join(1000); ready.Dispose(); }
    [StructLayout(LayoutKind.Sequential)] private struct Msg { public nint Window; public uint Message; public nint W, L; public uint Time; public int X, Y; }
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll")] private static extern int GetMessage(out Msg message, nint window, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread, uint message, nint w, nint l);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
}
