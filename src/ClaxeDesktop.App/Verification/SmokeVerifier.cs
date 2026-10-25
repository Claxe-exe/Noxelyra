using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;

namespace ClaxeDesktop.App;
// Opt-in integration harness. Only uses the app's own windows and isolated CLAXE_DATA_DIR.
internal static class SmokeVerifier
{
    public static async Task RunAsync(DesktopController controller, ISettingsStore store, string directory, VerificationMonitorService? monitorSource = null)
    {
        if (Environment.GetEnvironmentVariable("CLAXE_DATA_DIR") is null) throw new InvalidOperationException("Verification requires isolated data directory.");
        var checks = new List<object>();
        void Check(string name, bool passed) { checks.Add(new { name, passed }); if (!passed) throw new InvalidOperationException(name); }
        var output = Path.Combine(directory, "verification"); Directory.CreateDirectory(output);
        System.Diagnostics.Process? windowProbe = null;
        try
        {
            await Task.Delay(1600);
            Check("Three floating WPF windows visible", controller.Windows.Count == 3 && controller.Windows.All(w => w.IsVisible));
            Check("Real clock binding", controller.ViewModel.Time.StartsWith(DateTime.Now.ToString("HH:mm")));
            Check("Real CPU sample", !controller.ViewModel.CpuText.Contains("erişilemiyor"));
            var panel = controller.Windows.First(w => w.Kind == "Panel");
            var original = controller.ViewModel.Settings;
            Check("Restart restores appearance", panel.Height == original.PanelHeight && controller.Windows.OfType<DockWindow>().Single().Height >= original.IconSize);
            controller.ViewModel.PanelHeight = 60;
            Check("Live panel height preview", panel.Height == 60);
            controller.RevertPreview(); Check("Cancel restores appearance", panel.Height == original.PanelHeight);
            controller.ViewModel.IconSize = 56; controller.ViewModel.ShowSeconds = true;
            await controller.SaveAsync();
            var loaded = await store.LoadAsync(); Check("Settings saved and reloaded", loaded.Settings.IconSize == 56 && loaded.Settings.ShowSeconds);
            var target = controller.ViewModel.Monitors.Last();
            panel.Place(new(target.Id, 120, 84), controller.ViewModel.Monitors, true);
            var actual = panel.Capture(controller.ViewModel.Monitors);
            Check("Panel moves to target monitor", actual.MonitorId == target.Id && Math.Abs(actual.X - 120) < 2 && Math.Abs(actual.Y - 84) < 2);
            controller.OnMoved(panel); await Task.Delay(900);
            loaded = await store.LoadAsync();
            Check(controller.IsSafeMode ? "Safe mode preserves stored placement" : "Moved panel persisted", controller.IsSafeMode ? loaded.Settings.Panel == original.Panel : loaded.Settings.Panel?.MonitorId == target.Id);
            controller.ShowSettings(); await Task.Delay(400);
            var settingsWindow = Application.Current.Windows.OfType<SettingsWindow>().Single();
            controller.ViewModel.IconSize = 60;
            settingsWindow.SaveButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            await Task.Delay(500);
            Check("Settings Save button persists view model", (await store.LoadAsync()).Settings.IconSize == 60);
            var editor = settingsWindow.Editor;
            var themeBaseline = controller.ViewModel.Settings;
            foreach (var category in editor.Categories)
            {
                editor.SelectedCategory = category; settingsWindow.UpdateLayout();
                Check("Navigation preserves settings: " + category, controller.ViewModel.Settings == themeBaseline);
                if (category == "Dock") Export(settingsWindow, Path.Combine(output, "Settings-Dock.png"));
            }
            editor.SelectedCategory = "Görünüm";
            settingsWindow.SearchBox.Text = "appearance.cornerRadius";
            Check("Search textbox filters setting keys", editor.VisibleRows.Cast<object>().Count() == 1);
            settingsWindow.SearchBox.Text = "bulunamayan-ayar";
            Check("Empty search results disable reset", !editor.CanReset);
            settingsWindow.SearchBox.Text = "";
            var colorRow = editor.Rows.Single(r => r.Definition.Key == "appearance.accentPrimary");
            colorRow.Value = "#BAD";
            Check("Invalid color blocks Save and preserves live theme", colorRow.HasError && !settingsWindow.SaveButton.IsEnabled && controller.ViewModel.Settings.PrimaryAccent == themeBaseline.PrimaryAccent);
            colorRow.Value = "#7FDAFB";
            Check("Valid color previews and enables Save", settingsWindow.SaveButton.IsEnabled && ((SolidColorBrush)Application.Current.Resources["Accent"]).Color == (Color)ColorConverter.ConvertFromString("#7FDAFB"));
            editor.Undo(); Check("Undo restores appearance", controller.ViewModel.Settings.PrimaryAccent == themeBaseline.PrimaryAccent);
            editor.Redo(); Check("Redo reapplies appearance", controller.ViewModel.Settings.PrimaryAccent == "#7FDAFB");
            foreach (var theme in ThemePresets.All)
            {
                settingsWindow.UpdateLayout();
                var card = FindVisual<System.Windows.Controls.Button>(settingsWindow, b => b.Tag as string == theme.Id) ?? throw new InvalidOperationException("Theme card not rendered");
                card.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                Check(theme.Name + " card updates real resource palette", ((SolidColorBrush)Application.Current.Resources["Canvas"]).Color == (Color)ColorConverter.ConvertFromString(theme.Canvas));
                Export(settingsWindow, Path.Combine(output, "Theme-" + theme.Id + ".png"));
            }
            var materialRow = editor.Rows.Single(r => r.Definition.Key == "themes.panelStyle");
            var opacityRow = editor.Rows.Single(r => r.Definition.Key == "appearance.surfaceOpacity");
            var panelFrame = (System.Windows.Controls.Border)panel.Content;
            materialRow.Value = "flat";
            Check("Flat is opaque and disables transparency editor", ((SolidColorBrush)panelFrame.Background).Color.A == 255 && !opacityRow.IsEnabled);
            materialRow.Value = "glass"; opacityRow.Value = .5;
            Check("Glass opacity affects real panel surface", ((SolidColorBrush)panelFrame.Background).Color.A == 127);
            materialRow.Value = "neon";
            Check("Neon applies accent border and glow", panelFrame.Effect is System.Windows.Media.Effects.DropShadowEffect && ((SolidColorBrush)panelFrame.BorderBrush).Color == ((SolidColorBrush)Application.Current.Resources["Accent"]).Color);
            editor.Rows.Single(r => r.Definition.Key == "appearance.cornerRadius").Value = 24.0;
            Check("Corner radius changes real panel", panelFrame.CornerRadius.TopLeft == 24);
            var serializedTheme = ThemeFiles.Serialize(controller.ViewModel.Settings);
            controller.RevertPreview();
            var beforeBadTheme = controller.ViewModel.Settings;
            try { settingsWindow.PreviewThemeText("{broken"); } catch (InvalidDataException) { }
            Check("Invalid import preserves current settings", controller.ViewModel.Settings == beforeBadTheme);
            settingsWindow.PreviewThemeText(serializedTheme);
            Check("Theme import previews without changing disk", controller.ViewModel.Settings.ThemeId == "sakura" && (await store.LoadAsync()).Settings.ThemeId == themeBaseline.ThemeId);
            await controller.SaveAsync();
            Check("Theme commit persists selection and material", (await store.LoadAsync()).Settings is { ThemeId: "sakura", Material: "neon", CornerRadius: 24 });
            editor.SearchText = "dock.iconSizeDip";
            editor.Rows.Single(r => r.Definition.Key == "dock.iconSizeDip").Value = 80.0;
            editor.ResetVisible();
            Check("Filtered reset affects only visible settings", controller.ViewModel.Settings.IconSize == 48 && controller.ViewModel.Settings.ThemeId == "sakura");
            controller.ViewModel.Edit(themeBaseline); await controller.SaveAsync();
            editor.ClearErrors(); editor.SearchText = "";
            if (monitorSource is not null)
            {
                var topology = controller.ViewModel.Monitors.ToArray();
                var beforeRemap = controller.ViewModel.Settings.Panel;
                var primary = topology.First(m => m.Primary);
                try
                {
                    monitorSource.Override = [primary with { Scale = 1.5, Width = 1280, Height = 960 }];
                    controller.RefreshMonitors();
                    var remapped = panel.Capture(controller.ViewModel.Monitors);
                    Check("Simulated unplug remaps panel to remaining monitor", remapped.MonitorId == primary.Id);
                    Check("Simulated mixed DPI clamps panel width", panel.Width <= 1280 / 1.5);
                    Check("Topology fallback preserves saved identity", controller.ViewModel.Settings.Panel == beforeRemap);
                }
                finally { monitorSource.Override = null; controller.RefreshMonitors(); }
            }
            foreach (Window window in Application.Current.Windows)
            {
                if (window is not FloatingWindow && window is not SettingsWindow) continue;
                window.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(output, window is FloatingWindow f ? f.Kind + ".png" : "Settings.png")); encoder.Save(file);
            }
            Check("Window render exports", File.Exists(Path.Combine(output, "Settings.png")));
            if (Environment.GetEnvironmentVariable("CLAXE_WINDOW_PROBE_EXE") is { } windowProbeExe)
            {
                var ready = Path.Combine(directory, "windows-ready-" + Guid.NewGuid().ToString("N") + ".txt");
                var probeStart = new System.Diagnostics.ProcessStartInfo(windowProbeExe) { UseShellExecute = false };
                probeStart.Environment["CLAXE_WINDOW_PROBE_READY"] = ready;
                windowProbe = System.Diagnostics.Process.Start(probeStart);
                var service = new WindowsWindowService();
                AppWindow[] fixtureWindows = [];
                for (var attempt = 0; attempt < 50; attempt++)
                {
                    fixtureWindows = service.Snapshot().Where(w => w.ProcessId == windowProbe!.Id).ToArray();
                    if (File.Exists(ready) && fixtureWindows.Length == 10 && fixtureWindows.Any(w => w.Minimized)) break;
                    await Task.Delay(100);
                }
                Check("Ten real windows found; tool window excluded", fixtureWindows.Length == 10 && fixtureWindows.All(w => w.Title.StartsWith("Noxelyra QA Window")));
                Check("Native minimized window retained", fixtureWindows.Any(w => w.Minimized));
                Check("Ten native windows grouped as one application", WindowGrouping.Build(fixtureWindows, true).Count == 1);
                var minimized = fixtureWindows.Single(w => w.Minimized);
                var activation = service.Activate(minimized); await Task.Delay(350);
                Check("Minimized native window restored", !service.Snapshot().Single(w => w.Handle == minimized.Handle).Minimized);
                checks.Add(new { name = "Foreground activation outcome (Windows can restrict focus)", passed = activation, informational = true });
                var dock = controller.Windows.OfType<DockWindow>().Single();
                controller.ViewModel.GroupWindows = true; dock.UpdateWindows(fixtureWindows); await Task.Delay(300);
                Check("Dock displays native running group", dock.AppButtons.Any(b => System.Windows.Automation.AutomationProperties.GetName(b).Contains("10 açık pencere")));
                var groupButton = dock.AppButtons.First(b => System.Windows.Automation.AutomationProperties.GetName(b).Contains("10 açık pencere"));
                var pin = groupButton.ContextMenu.Items.OfType<System.Windows.Controls.MenuItem>().Single(i => (string)i.Header == "Dock'a sabitle");
                pin.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent)); await Task.Delay(450);
                Check("Running app context pin persists", (await store.LoadAsync()).Settings.Shortcuts.Any(s => s.Target == windowProbeExe));
                var pinnedButton = dock.AppButtons.Single(b => System.Windows.Automation.AutomationProperties.GetName(b).StartsWith("ClaxeDesktop.WindowProbe"));
                var unpin = pinnedButton.ContextMenu.Items.OfType<System.Windows.Controls.MenuItem>().Single(i => (string)i.Header == "Dock'tan kaldır");
                unpin.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent)); await Task.Delay(450);
                Check("Dock context unpin persists", !(await store.LoadAsync()).Settings.Shortcuts.Any(s => s.Target == windowProbeExe));
                Export(dock, Path.Combine(output, "Dock-Running.png"));
                controller.ViewModel.GroupWindows = false; dock.UpdateWindows(fixtureWindows);
                Check("Ungrouped dock exposes ten native windows", dock.AppButtons.Count >= 10);
                using var iconService = new ShellIconService(); var icon = await iconService.GetAsync(windowProbeExe);
                Check("Real shell icon loaded", icon.Icon is not null);
                controller.ViewModel.DockEdge = "bottom"; controller.ViewModel.DockAutoHide = true;
                settingsWindow.Activate(); await Task.Delay(1100);
                Check("Edge auto hide respects safe mode", controller.IsSafeMode ? !dock.IsCollapsed : dock.IsCollapsed || dock.IsMouseOver || dock.IsKeyboardFocusWithin);
                controller.ViewModel.DockAutoHide = false; await Task.Delay(250);
                Check("Disabling auto hide restores dock", !dock.IsCollapsed && dock.Height > 6);
                controller.RevertPreview();
                var scripted = new VerificationWindowService();
                var testVm = new DesktopViewModel(new Settings());
                foreach (var m in controller.ViewModel.Monitors) testVm.Monitors.Add(m);
                var testDock = new DockWindow(testVm, _ => { }, () => { }, scripted, iconService, () => { });
                try
                {
                    testDock.Show(); testDock.Place(null, testVm.Monitors, false);
                    scripted.Fullscreen = true; await Task.Delay(800);
                    Check("Simulated fullscreen signal hides real dock", !testDock.IsVisible);
                    scripted.Fullscreen = false; await Task.Delay(1000);
                    Check("Dock reappears after fullscreen signal clears", testDock.IsVisible);
                }
                finally { testDock.Close(); }
            }
            var probe = Environment.GetEnvironmentVariable("CLAXE_PROBE_EXE");
            if (probe is not null)
            {
                var marker = Path.Combine(directory, "launch-probe-" + Guid.NewGuid().ToString("N") + ".txt"); Environment.SetEnvironmentVariable("CLAXE_LAUNCH_PROBE", marker);
                try { new WindowsLauncher().Launch(new("Launch probe", probe)); await Task.Delay(1500); Check("Real executable launched through dock service", File.Exists(marker)); }
                finally { Environment.SetEnvironmentVariable("CLAXE_LAUNCH_PROBE", null); }
            }
            await WidgetVerifier.RunAsync(controller, store, output, Check);
            await File.WriteAllTextAsync(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { safeMode = controller.IsSafeMode, success = true, checks }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            await File.WriteAllTextAsync(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { success = false, checks, error = ex.Message, status = controller.ViewModel.Status, stack = ex.StackTrace }));
        }
        finally { if (windowProbe is not null) { if (!windowProbe.HasExited) windowProbe.Kill(); windowProbe.Dispose(); } controller.Exit(); }
    }
    private static void Export(Window window, string path)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
    private static T? FindVisual<T>(DependencyObject parent, Func<T, bool> predicate) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found && predicate(found)) return found;
            var nested = FindVisual(child, predicate); if (nested is not null) return nested;
        }
        return null;
    }
}

internal sealed class VerificationMonitorService : IMonitorService
{
    public IReadOnlyList<MonitorInfo>? Override { get; set; }
    public IReadOnlyList<MonitorInfo> GetMonitors() => Override ?? new WindowsMonitorService().GetMonitors();
}
internal sealed class VerificationWindowService : IWindowService
{
    public bool Fullscreen { get; set; }
    public IReadOnlyList<AppWindow> Snapshot() => [];
    public bool Activate(AppWindow window) => false;
    public nint Foreground => 0;
    public bool IsFullscreenOn(MonitorInfo monitor) => Fullscreen;
}

