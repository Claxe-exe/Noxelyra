using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

/// <summary>
/// Checks for the 0.28–0.33 features in an isolated data folder: dynamic island, notifications, timer, quick settings, emergency
/// shortcut, Alt+Tab switcher, live widgets, performance counters, wallpaper palette and rice. Tiling and the tray button are not
/// exercised here because they would move or focus the tester's real windows. No network request is made (the weather widget has no city).
/// </summary>
internal static class V033Verifier
{
    internal static async Task RunAsync(DesktopController controller, string directory)
    {
        var checks = new List<object>(); string? error = null;
        void Check(string name, bool passed, string detail = "") { checks.Add(new { name, passed, detail }); if (!passed) throw new InvalidOperationException(name + (detail.Length > 0 ? ": " + detail : "")); }
        var panel = controller.Windows.OfType<PanelWindow>().Single(); var island = panel.Island;
        try
        {
            await controller.WaitAdvancedAsync(); await Task.Delay(500);
            Check("Emergency exit shortcut is registered and named for the keyboard layout", controller.EmergencyRegistered && controller.GestureText("emergency").StartsWith("Ctrl+Shift+"), controller.GestureText("emergency"));

            // A track already playing on the test machine legitimately starts the island as "Music".
            var baseline = island.State;
            Check("Island starts as the clock, or the track when one is playing", baseline is "Clock" or "Music", baseline);
            controller.StartTimer(1); island.Tick(); var timer = island.State;
            controller.StopTimer(); var afterTimer = island.State;
            Check("Timer takes the island and stopping returns to the previous view", timer == "Timer" && afterTimer == baseline, timer + "/" + afterTimer);

            island.ShowVolume(40, false, "\uE994"); var volume = island.State; await Task.Delay(500); ExportOver(panel, Path.Combine(directory, "v033-island-volume.png"));
            await Task.Delay(1800);
            Check("A volume change shows briefly, then the previous view returns", volume == "Volume" && island.State == baseline, volume + "/" + island.State);

            var posted = 0; void Count(NotificationItem _) => posted++;
            controller.NotificationPosted += Count; var before = controller.Notifications.Items.Count;
            controller.Notify("Doğrulama", "Dinamik ada bildirimi"); var note = island.State; await Task.Delay(500); ExportOver(panel, Path.Combine(directory, "v033-island-note.png"));
            controller.SetDoNotDisturb(true); controller.Notify("Sessiz", "");
            controller.SetDoNotDisturb(false); controller.NotificationPosted -= Count;
            Check("Notifications are kept, shown in the island and only listed while do-not-disturb is on", note == "Note" && posted == 1 && controller.Notifications.Items.Count == before + 2 && controller.Notifications.Items[0].Title == "Sessiz", $"{note} posted={posted} items={controller.Notifications.Items.Count}");

            controller.ShowQuickSettings(); await Task.Delay(120);
            var quick = Application.Current.Windows.OfType<QuickSettingsWindow>().SingleOrDefault();
            var opened = quick is { IsVisible: true } && quick.TileCount == 8 && controller.Notifications.Unread == 0;
            if (quick is not null) { quick.UpdateLayout(); Export(quick, Path.Combine(directory, "v033-quick-settings.png")); }
            controller.ShowQuickSettings(); await Task.Delay(500);
            Check("Quick settings slide in with 8 real switches, mark notifications read and close on a second press", opened && quick is { IsVisible: false }, $"opened={opened}");

            controller.Switcher("switcher.open"); await Task.Delay(300); var switcherOpen = controller.SwitcherOpen;
            controller.Switcher("switcher.cancel"); await Task.Delay(200);
            Check("Alt+Tab switcher opens and cancels without switching", switcherOpen && !controller.SwitcherOpen);

            var live = new[] { "music", "graph", "bigclock", "weather" }.Select((kind, i) => new WidgetInstance { Kind = kind, Name = kind, Width = kind == "bigclock" ? 420 : 320, Height = kind switch { "music" => 420, "graph" => 430, "bigclock" => 230, _ => 340 }, Text = "", Placement = new(controller.ViewModel.Monitors.First(m => m.Primary).Id, 40 + i * 340, 120) }).ToList();
            await controller.SaveWidgetsAsync([.. controller.ViewModel.Settings.Widgets, .. live]); controller.Widgets.LoadSaved(controller.ViewModel.Settings.Widgets);
            await Task.Delay(3500);
            WidgetWindow Window(string kind) => controller.Windows.OfType<WidgetWindow>().Single(w => w.Model.Kind == kind);
            foreach (var kind in new[] { "music", "graph", "bigclock", "weather" }) { var w = Window(kind); w.UpdateLayout(); Export(w, Path.Combine(directory, $"v033-widget-{kind}.png")); }
            var graph = Window("graph"); var running = graph.LiveRunning && Window("music").LiveRunning && !Window("weather").LiveRunning;
            graph.Hide(); await Task.Delay(200);
            Check("Live widgets run while visible, stop when hidden, and weather waits for a city", running && !graph.LiveRunning, $"running={running}");

            using (var sampler = new PerformanceSampler()) { await Task.Delay(1100); var sample = sampler.Sample(); Check("Performance counters give disk, GPU or network readings", sample.Disk is not null || sample.Gpu is not null || sample.NetworkBytesPerSecond is not null, $"gpu={sample.Gpu} disk={sample.Disk} net={sample.NetworkBytesPerSecond}"); }

            var picture = Path.Combine(directory, "palette-fixture.png"); SaveFixture(picture);
            var applied = await controller.UseWallpaperPaletteAsync(picture);
            var custom = controller.ViewModel.Settings.ThemeId == ThemePresets.WallpaperId && controller.ViewModel.Settings.CustomPalette is { } palette && ThemePresets.Contrast(palette.Text, palette.Surface) >= 7;
            panel.UpdateLayout(); ExportOver(panel, Path.Combine(directory, "v033-panel-wallpaper-theme.png"));
            await controller.SetThemeAsync("midnight-claxe");
            Check("Wallpaper colours make a readable theme and a preset brings the old look back", applied && custom && controller.ViewModel.Settings.ThemeId == "midnight-claxe", controller.ViewModel.Status);

            await controller.SetThemeAsync("nord");
            var rice = Path.Combine(directory, "test.noxrice"); var exported = await controller.ExportRiceAsync(rice, false, false);
            await controller.SetThemeAsync("tokyo-night");
            var read = controller.ReadRice(rice); var message = await controller.ApplyRiceAsync(read.Rice, read.Wallpaper, read.Intro, read.Folder, replaceWidgets: false);
            var imported = controller.ViewModel.Settings.ThemeId == "nord" && controller.HasRiceBackup;
            var restored = await controller.RestoreRiceBackupAsync();
            Check("Rice exports, imports with a backup and returns to the previous look", File.Exists(rice) && imported && controller.ViewModel.Settings.ThemeId == "tokyo-night", exported + " | " + message + " | " + restored);
            await controller.SetThemeAsync("midnight-claxe");

            controller.ShowIslandCard(island); await Task.Delay(400);
            var card = controller.Card;
            var underBar = card is { IsVisible: true, Summoned: false } && card.Page == 0;
            if (card is not null) { await Task.Delay(900); card.UpdateLayout(); Export(card, Path.Combine(directory, "v034-card-page1.png")); card.Press(0x09); await Task.Delay(450); card.UpdateLayout(); Export(card, Path.Combine(directory, "v034-card-page2.png")); }
            var flipped = card?.Page == 1;
            controller.ShowIslandCard(island); await Task.Delay(400);
            Check("Island click opens the two-page card, Tab/arrow flips to PC controls and a second click closes it", underBar && flipped && card is { IsVisible: false }, $"under={underBar} flipped={flipped}");
            var focused = WindowOps.Foreground;
            controller.ToggleIslandSummon(); await Task.Delay(450);
            var summoned = card is { IsVisible: true, Summoned: true } && WindowOps.Foreground == focused; var keys = card?.RegisteredKeys ?? 0;
            controller.ToggleIslandSummon(); await Task.Delay(400);
            Check("Alt+0 summons the card on top without taking focus, claims the card keys and gives them back", controller.IslandShortcutRegistered && summoned && keys >= 5 && card is { IsVisible: false, RegisteredKeys: 0 }, $"shortcut={controller.IslandShortcutRegistered} summoned={summoned} keys={keys}");

            // Visual QA only: the island holding a track, on a wide bar.
            panel.Width = 1600; await Task.Delay(500);
            island.ShowMedia(new MediaState("Gece Yürüyüşü", "Noxelyra", true, true, true, true, true) { Position = TimeSpan.FromSeconds(42), Duration = TimeSpan.FromSeconds(195) });
            await Task.Delay(380); panel.UpdateLayout(); ExportOver(panel, Path.Combine(directory, "v033-island-music.png"));
            controller.StartTimer(25); await Task.Delay(700); ExportOver(panel, Path.Combine(directory, "v033-island-timer.png")); controller.StopTimer();
        }
        catch (Exception ex) { error = ex.ToString(); }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "v033-results.json"), JsonSerializer.Serialize(new { success = error is null, checks, error }, new JsonSerializerOptions { WriteIndented = true }));
            controller.Exit();
        }
    }
    private static void SaveFixture(string file)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(20, 70, 200), Color.FromRgb(40, 110, 230), 30), null, new Rect(0, 0, 320, 180));
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(230, 70, 140)), null, new Point(250, 60), 40, 40);
        }
        var bitmap = new RenderTargetBitmap(320, 180, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(file); png.Save(stream);
    }
    private static void ExportOver(Window window, string file)
    {
        int width = (int)Math.Ceiling(window.ActualWidth), height = (int)Math.Ceiling(window.ActualHeight) + 24;
        if (width <= 0 || height <= 24) return;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(OriginalArt.Wallpaper(), new Rect(-width * .1, -height * 4, width * 1.2, width * 1.2 * 900 / 1600));
            dc.DrawRectangle(new VisualBrush((Visual)window.Content) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, new Rect(0, 12, window.ActualWidth, window.ActualHeight));
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(file); png.Save(stream);
    }
    private static void Export(Window window, string file)
    {
        if (window.ActualWidth <= 0 || window.ActualHeight <= 0) return;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(file); png.Save(stream);
    }
}
