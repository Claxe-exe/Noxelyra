using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;

/// <summary>Launcher history and the optional clipboard history.</summary>
public sealed partial class DesktopController
{
    private LauncherHistory? launcherHistory;
    private ClipboardWatcher? clipboardWatcher;
    private string LauncherHistoryFile => Path.Combine(desktopData, "launcher-history.json");
    internal ClipboardBuffer ClipboardItems { get; } = new();
    internal LauncherHistory LauncherUsage
    {
        get
        {
            if (launcherHistory is not null) return launcherHistory;
            try { launcherHistory = File.Exists(LauncherHistoryFile) ? LauncherHistory.Parse(File.ReadAllText(LauncherHistoryFile)) : new(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log.Write("launcher-history-read-failed", ex); launcherHistory = new(); }
            return launcherHistory;
        }
    }
    internal void RecordLaunch(string target) { LauncherUsage.Record(target, DateTimeOffset.Now); SaveLauncherHistory(); }
    internal void ClearLauncherHistory() { LauncherUsage.Clear(); SaveLauncherHistory(); }
    private void SaveLauncherHistory()
    {
        if (safe || ReadOnly) return;
        var json = LauncherUsage.Serialize(); var file = LauncherHistoryFile;
        _ = Task.Run(() =>
        {
            try { var temporary = file + ".tmp"; File.WriteAllText(temporary, json); File.Move(temporary, file, true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log.Write("launcher-history-save-failed", ex); }
        });
    }
    /// <summary>Listens to the clipboard only while the option is on; turning it off forgets everything.</summary>
    private void ConfigureClipboard()
    {
        var wanted = !safe && !exiting && committed.Keyboard.ClipboardHistory;
        if (wanted == clipboardWatcher is not null) return;
        if (!wanted) { clipboardWatcher?.Dispose(); clipboardWatcher = null; ClipboardItems.Clear(); return; }
        try { clipboardWatcher = new(text => ClipboardItems.Add(text)); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { Log.Write("clipboard-history-failed", ex); ViewModel.Status = "Pano geçmişi başlatılamadı."; }
    }
    private CalendarFlyout? calendar;
    private long calendarClosedAt;
    /// <summary>Opens the month calendar under the clock. A click on the clock while it is open only closes it.</summary>
    internal void ShowCalendar(FrameworkElement anchor)
    {
        if (exiting || calendar is not null || Environment.TickCount64 - calendarClosedAt < 250) return;
        calendar = new(this, anchor); calendar.Closed += (_, _) => { calendar = null; calendarClosedAt = Environment.TickCount64; };
        calendar.Open();
    }
    /// <summary>Lock, sleep, restart or shut down Windows. Callers ask for confirmation before restart and shutdown.</summary>
    internal void Power(string action)
    {
        try
        {
            switch (action)
            {
                case "lock": if (!LockWorkStation()) ViewModel.Status = "Ekran kilitlenemedi."; break;
                case "sleep": System.Windows.Forms.Application.SetSuspendState(System.Windows.Forms.PowerState.Suspend, false, false); break;
                case "restart": Log.Write("user-restart"); Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0") { UseShellExecute = false, CreateNoWindow = true }); break;
                case "shutdown": Log.Write("user-shutdown"); Process.Start(new ProcessStartInfo("shutdown.exe", "/s /t 0") { UseShellExecute = false, CreateNoWindow = true }); break;
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { ViewModel.Status = "Windows bu işlemi yapmadı."; Log.Write("power-failed", ex); }
    }
    [DllImport("user32.dll")] private static extern bool LockWorkStation();
}

/// <summary>A hidden message window subscribed to clipboard changes with AddClipboardFormatListener. Text that its owner marks private (password managers do) is ignored.</summary>
internal sealed class ClipboardWatcher : IDisposable
{
    private const int ClipboardUpdate = 0x031D;
    private readonly HwndSource source;
    private readonly Action<string> found;
    private bool disposed;
    internal ClipboardWatcher(Action<string> found)
    {
        this.found = found;
        source = new(new HwndSourceParameters("Noxelyra clipboard") { ParentWindow = new nint(-3), WindowStyle = 0, Width = 0, Height = 0 });
        source.AddHook(Hook);
        if (!AddClipboardFormatListener(source.Handle)) { source.Dispose(); throw new System.ComponentModel.Win32Exception(); }
    }
    private nint Hook(nint hwnd, int message, nint w, nint l, ref bool handled)
    {
        if (message == ClipboardUpdate) source.Dispatcher.BeginInvoke(Read, System.Windows.Threading.DispatcherPriority.Background);
        return 0;
    }
    private void Read()
    {
        if (disposed) return;
        try
        {
            if (!Clipboard.ContainsText()) return;
            var data = Clipboard.GetDataObject();
            if (data is null || data.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing") || data.GetDataPresent("Clipboard Viewer Ignore")) return;
            if (data.GetData("CanIncludeInClipboardHistory") is MemoryStream allowed && allowed.Length >= 4) { var bytes = new byte[4]; allowed.Position = 0; allowed.ReadExactly(bytes); if (BitConverter.ToInt32(bytes) == 0) return; }
            found(Clipboard.GetText());
        }
        catch (COMException) { /* Another app holds the clipboard; this copy is simply not recorded. */ }
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        RemoveClipboardFormatListener(source.Handle); source.Dispose();
    }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool AddClipboardFormatListener(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RemoveClipboardFormatListener(nint hwnd);
}
