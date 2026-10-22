using System.ComponentModel;
using System.Runtime.InteropServices;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.Platform.Win;

/// <summary>
/// Documented low-level keyboard hook (WH_KEYBOARD_LL) on its own thread, so a busy UI thread never delays typing.
/// No DLL is loaded into other processes. Only Win+D is consumed; the trigger runs on the caller's dispatcher.
/// </summary>
public sealed class ShowDesktopHook : IDisposable
{
    private readonly Action trigger;
    private readonly ShowDesktopKeys keys = new();
    private readonly Thread thread;
    private readonly ManualResetEventSlim ready = new();
    private HookProc? callback;
    private nint hook;
    private uint threadId;
    private Exception? failure;
    public ShowDesktopHook(Action trigger)
    {
        this.trigger = trigger;
        thread = new Thread(Run) { IsBackground = true, Name = "Noxelyra Win+D" }; thread.Start();
        ready.Wait(5000);
        if (failure is not null) throw failure;
        if (hook == 0) throw new InvalidOperationException("Klavye kancası zamanında kurulamadı.");
    }
    private void Run()
    {
        threadId = GetCurrentThreadId();
        callback = Proc;
        hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
        if (hook == 0) failure = new Win32Exception();
        ready.Set();
        if (hook == 0) return;
        while (GetMessage(out var message, 0, 0, 0) > 0) { TranslateMessage(ref message); DispatchMessage(ref message); }
        UnhookWindowsHookEx(hook);
    }
    private nint Proc(int code, nint message, nint data)
    {
        if (code >= 0)
        {
            var info = Marshal.PtrToStructure<KeyInfo>(data);
            var down = message is 0x0100 or 0x0104;
            var others = GetAsyncKeyState(0x10) < 0 || GetAsyncKeyState(0x11) < 0 || GetAsyncKeyState(0x12) < 0;
            switch (keys.Handle((int)info.Key, down, (info.Flags & 0x10) != 0, others))
            {
                case KeyDecision.Trigger: MaskStartMenu(); ThreadPool.QueueUserWorkItem(_ => trigger()); return 1;
                case KeyDecision.Swallow: return 1;
            }
        }
        return CallNextHookEx(0, code, message, data);
    }
    // Releasing Win after a swallowed key would open Start; an unassigned key in between tells Windows a combination happened.
    private static void MaskStartMenu()
    {
        var inputs = new Input[2];
        inputs[0].Type = 1; inputs[0].Key.Key = 0xE8;
        inputs[1].Type = 1; inputs[1].Key.Key = 0xE8; inputs[1].Key.Flags = 0x0002;
        SendInput(2, inputs, Marshal.SizeOf<Input>());
    }
    public void Dispose() { if (threadId != 0) PostThreadMessage(threadId, 0x0012, 0, 0); thread.Join(2000); ready.Dispose(); }

    private delegate nint HookProc(int code, nint message, nint data);
    [StructLayout(LayoutKind.Sequential)] private struct KeyInfo { public uint Key, Scan, Flags, Time; public nint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct Msg { public nint Window; public uint Message; public nint W, L; public uint Time; public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort Key, Scan; public uint Flags, Time; public nint Extra; }
    // INPUT is a union; the keyboard member is padded to the mouse member's size.
    [StructLayout(LayoutKind.Explicit)] private struct Input { [FieldOffset(0)] public uint Type; [FieldOffset(8)] public KeyboardInput Key; [FieldOffset(8)] private MousePadding pad; }
    [StructLayout(LayoutKind.Sequential)] private struct MousePadding { public int X, Y; public uint Data, Flags, Time; public nint Extra; }
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int id, HookProc proc, nint module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern int GetMessage(out Msg message, nint window, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Msg message);
    [DllImport("user32.dll")] private static extern nint DispatchMessage(ref Msg message);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread, uint message, nint w, nint l);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
}
