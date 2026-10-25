using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;

internal static class ReleaseVerifier
{
    internal static async Task RunAsync(DesktopController controller, string directory)
    {
        var checks = new List<object>();
        void Check(string name, bool ok) { checks.Add(new { name, passed = ok }); if (!ok) throw new InvalidOperationException(name); }
        string? error = null;
        try
        {
            await controller.WaitAdvancedAsync();
            var dock = controller.Windows.OfType<DockWindow>().Single();
            var panel = controller.Windows.OfType<PanelWindow>().Single();
            var contenderWindow = new Window(); new WindowInteropHelper(contenderWindow).EnsureHandle();
            using (var contender = new DockShortcutBinding(contenderWindow, () => { }))
            {
                bool conflict = false;
                try { contender.Register(); } catch (InvalidOperationException ex) { conflict = ex.Message.Contains("Alt+K") && ex.Message.Contains("menüsünden"); }
                Check("Alt+K registration owns shortcut and reports a second registration conflict", conflict);
            }
            contenderWindow.Close();
            var hwnd = new WindowInteropHelper(panel).Handle;
            PostMessage(hwnd, 0x312, 754, 0);
            await Task.Delay(100);
            Check("WM_HOTKEY dispatch brings dock above apps and expands it", dock.IsSummoned && dock.Topmost && dock.IsVisible && !dock.IsCollapsed);
            await Task.Delay(700);
            Check("Visibility timer keeps summoned dock above apps", dock.IsSummoned && dock.Topmost && dock.IsVisible);
            dock.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(dock), Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Check("Escape releases temporary topmost state", !dock.IsSummoned && !dock.Topmost);
            var placement = controller.ViewModel.Settings.Dock;
            controller.SummonDock(); controller.SummonDock();
            Check("Repeated Alt+K reasserts visibility without changing saved placement", dock.IsSummoned && dock.Topmost && controller.ViewModel.Settings.Dock == placement); dock.DismissSummon();
            var signals = (VerificationWindowService)controller.ReleaseWindowSignals!;
            signals.Fullscreen = true; await Task.Delay(700);
            var hidden = !dock.IsVisible;
            controller.SummonDock(); var shown = dock.IsVisible && dock.Topmost;
            dock.DismissSummon(); var restored = !dock.IsVisible && !dock.Topmost;
            signals.Fullscreen = false; await Task.Delay(900);
            Check("Explicit summon overrides fullscreen hide then restores fullscreen and normal policies", hidden && shown && restored && dock.IsVisible);
            var root = Path.Combine(directory, "recovery-check"); var recovery = new RecoveryService(root);
            var clean = !recovery.Begin(); var dirty = recovery.Begin(); recovery.Complete();
            var store = new SettingsStore(root); await store.SaveAsync(new()); await store.SaveAsync(new() { IconSize = 56 });
            await File.WriteAllTextAsync(Path.Combine(root, "settings.json"), "broken");
            var recovered = await store.LoadAsync();
            Check("Dirty session detection clean marker and settings backup recovery", clean && dirty && !File.Exists(Path.Combine(root, "session.lock")) && recovered.Settings.IconSize == new Settings().IconSize);
            controller.SummonDock(); dock.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(dock.ActualWidth), (int)Math.Ceiling(dock.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(dock);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(directory, "summoned-dock.png"))) png.Save(file);
            dock.DismissSummon();
        }
        catch (Exception ex) { error = ex.Message; }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "release-results.json"), JsonSerializer.Serialize(new { success = error is null, checks, error }, new JsonSerializerOptions { WriteIndented = true }));
            controller.Exit();
        }
    }
    [DllImport("user32.dll")] private static extern bool PostMessage(nint hwnd, uint message, nint w, nint l);
}
