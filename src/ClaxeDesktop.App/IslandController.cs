using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;

/// <summary>The island card: opened from the bar, or summoned anywhere (games included) with the island shortcut.</summary>
public sealed partial class DesktopController
{
    private IslandCard? islandCard;
    private WindowHotkey? islandShortcut;
    private nint lastAppWindow;
    /// <summary>The last app window that had focus (Noxelyra's own windows excluded); the card's "close app" target from the bar.</summary>
    internal nint LastAppWindow => lastAppWindow;
    internal IslandCard? Card => islandCard;
    internal bool IslandShortcutRegistered => islandShortcut?.Enabled == true;
    private IslandCard EnsureCard()
    {
        if (islandCard is null) { islandCard = new(this, icons); islandCard.Closed += (_, _) => islandCard = null; }
        return islandCard;
    }
    /// <summary>Island click: the card under the island; a second click closes it.</summary>
    internal void ShowIslandCard(FrameworkElement anchor)
    {
        if (exiting) return;
        var card = EnsureCard();
        if (card.IsVisible && !card.Summoned) { card.Leave(); return; }
        // The click that moved focus away has just closed it; this click should not reopen it.
        if (card.RecentlyClosed) return;
        card.ShowUnder(anchor);
    }
    /// <summary>Island shortcut (Alt+0 by default): show the card on top of whatever is in front, or put it away again.</summary>
    internal void ToggleIslandSummon()
    {
        if (exiting || locked) return;
        var card = EnsureCard();
        if (card.IsVisible) { card.Leave(); return; }
        card.Summon();
        // Which app was in front (process name only), to tell "hotkey never arrived" from "card hidden by a fullscreen game".
        var front = ClaxeDesktop.Platform.Win.WindowOps.ProcessId(ClaxeDesktop.Platform.Win.WindowOps.Foreground);
        try { using var process = System.Diagnostics.Process.GetProcessById(front); Log.Write("island-summon " + process.ProcessName); } catch (ArgumentException) { Log.Write("island-summon"); }
    }
    internal void ShowTimerMenu(FrameworkElement target)
    {
        var menu = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.Bottom };
        void Add(string text, Action action) { var item = new MenuItem { Header = text }; item.Click += (_, _) => action(); menu.Items.Add(item); }
        if (target is DynamicIsland island) { Add("Ada kartını aç", () => ShowIslandCard(island)); menu.Items.Add(new Separator()); }
        foreach (var minutes in IslandTimer.Presets) { var length = minutes; Add(length == 25 ? "25 dakika · odak" : $"{length} dakika", () => StartTimer(length)); }
        Add("Kronometre", () => StartTimer(null));
        if (Timer is not null) Add("Zamanlayıcıyı durdur", StopTimer);
        menu.Items.Add(new Separator());
        Add("Bildirimler ve hızlı ayarlar", ShowQuickSettings);
        menu.IsOpen = true;
    }
    internal async Task<bool> SetIslandCityAsync(string city)
    {
        if (safe || ReadOnly) { ViewModel.Status = "Güvenli/salt okunur modda şehir kaydedilmez."; return false; }
        await persistenceGate.WaitAsync();
        try
        {
            var next = committed with { IslandCity = OpenMeteo.CleanCity(city) }; next.Validate();
            await store.SaveAsync(next); committed = next; ViewModel.SetAdvanced(next);
            ViewModel.Status = "Ada hava durumu: " + next.IslandCity; return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { Log.Write("island-city-failed", ex); ViewModel.Status = "Şehir kaydedilemedi: " + ex.Message; return false; }
        finally { persistenceGate.Release(); }
    }
    private void TrackAppWindow(IReadOnlyList<AppWindow> snapshot)
    {
        var foreground = windowService.Foreground;
        if (snapshot.Any(w => w.Handle == foreground)) lastAppWindow = foreground;
    }
}

/// <summary>A RegisterHotKey binding on a window's message loop under a fixed id.</summary>
internal sealed class WindowHotkey : IDisposable
{
    private readonly nint hwnd;
    private readonly HwndSource source;
    private readonly int id;
    private readonly Action action;
    private readonly string hint;
    internal bool Enabled { get; private set; }
    internal WindowHotkey(Window window, int id, Action action, string hint)
    {
        this.id = id; this.action = action; this.hint = hint;
        hwnd = new WindowInteropHelper(window).Handle; source = HwndSource.FromHwnd(hwnd)!; source.AddHook(Hook);
    }
    internal void Register(HotkeyGesture gesture)
    {
        if (Enabled) { UnregisterHotKey(hwnd, id); Enabled = false; }
        if (!RegisterHotKey(hwnd, id, HotkeyText.Modifiers(gesture), (uint)gesture.Key)) throw new InvalidOperationException(HotkeyText.Format(gesture) + " başka bir uygulama tarafından kullanılıyor. " + hint);
        Enabled = true;
    }
    private nint Hook(nint window, int message, nint w, nint l, ref bool handled)
    {
        if (message == 0x312 && w == id) { action(); handled = true; }
        return 0;
    }
    public void Dispose() { if (Enabled) UnregisterHotKey(hwnd, id); Enabled = false; source.RemoveHook(Hook); }
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint hwnd, int id);
}
