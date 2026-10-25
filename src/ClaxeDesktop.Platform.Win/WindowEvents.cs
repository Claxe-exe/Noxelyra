using System.Runtime.InteropServices;
namespace ClaxeDesktop.Platform.Win;

/// <summary>
/// Out-of-context WinEvent hooks (no DLL in other processes) for top-level window show, hide, destroy, minimize,
/// restore, foreground and move/size end. Callbacks arrive on the creating thread's message loop.
/// </summary>
public sealed class WindowEvents : IDisposable
{
    private readonly WinEventProc callback;
    private readonly List<nint> hooks = [];
    private readonly Action<nint, uint> changed;
    public WindowEvents(Action<nint, uint> changed)
    {
        this.changed = changed; callback = Handle;
        foreach (var (min, max) in new (uint, uint)[] { (0x0003, 0x0003), (0x000B, 0x000B), (0x0016, 0x0017), (0x8001, 0x8003) })
        {
            var hook = SetWinEventHook(min, max, 0, callback, 0, 0, 0x0000 | 0x0002);
            if (hook != 0) hooks.Add(hook);
        }
        if (hooks.Count == 0) throw new InvalidOperationException("Pencere olayları izlenemiyor.");
    }
    private void Handle(nint hook, uint type, nint window, int objectId, int childId, uint thread, uint time)
    {
        if (objectId == 0 && childId == 0 && window != 0) changed(window, type);
    }
    public void Dispose() { foreach (var hook in hooks) UnhookWinEvent(hook); hooks.Clear(); }
    private delegate void WinEventProc(nint hook, uint type, nint window, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll")] private static extern nint SetWinEventHook(uint min, uint max, nint module, WinEventProc callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(nint hook);
}
