using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

internal static class NoxelyraVerifier
{
    internal static async Task RunAsync(DesktopController controller, string directory, VerificationWindowService signals)
    {
        var checks = new List<object>(); string? error = null;
        void Check(string name, bool passed) { checks.Add(new { name, passed }); if (!passed) throw new InvalidOperationException(name); }
        var dock = controller.Windows.OfType<DockWindow>().Single();
        var panel = controller.Windows.OfType<PanelWindow>().Single();
        Window? other = null;
        try
        {
            await controller.WaitAdvancedAsync();
            other = new Window { Title = "Noxelyra verification focus probe", Width = 300, Height = 150 };
            new WindowInteropHelper(other).EnsureHandle();
            using (var competing = new DockShortcutBinding(other, () => { }))
            {
                bool rejected = false; try { competing.Register(); } catch (InvalidOperationException) { rejected = true; }
                Check("Alt+K owns native registration and rejects a conflicting registration", rejected);
            }
            var handle = new WindowInteropHelper(panel).Handle;
            PostMessage(handle, 0x312, 754, 0); await Task.Delay(100); PostMessage(handle, 0x312, 754, 0); await Task.Delay(200);
            Check("Repeated Alt+K always raises dock instead of hiding every second press", dock.IsSummoned && dock.IsVisible && dock.Topmost && !dock.IsCollapsed);
            other.Show(); other.Activate(); await Task.Delay(50); controller.SummonDock(); await Task.Delay(350);
            Check("Rapid deactivate and re-summon cannot dismiss the newly summoned dock", dock.IsSummoned && dock.IsVisible && dock.Topmost);
            var saved = controller.ViewModel.Settings.Dock;
            dock.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(dock), Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Check("Escape restores normal topmost policy and preserves saved dock placement", !dock.IsSummoned && !dock.Topmost && saved == controller.ViewModel.Settings.Dock);
            other.Hide(); signals.Fullscreen = true; await Task.Delay(700); var hidden = !dock.IsVisible;
            controller.SummonDock(); controller.SummonDock(); var shown = dock.IsSummoned && dock.IsVisible && dock.Topmost; dock.DismissSummon(); var restored = !dock.IsVisible;
            signals.Fullscreen = false; await Task.Delay(900);
            Check("Fullscreen hide summon and dismissal return to expected visibility", hidden && shown && restored && dock.IsVisible);
            Check("Executable and UI use Noxelyra branding", typeof(App).Assembly.GetName().Name == "Noxelyra" && panel.Title.StartsWith("Noxelyra") && dock.Title.StartsWith("Noxelyra"));
            var store = new SettingsStore(directory); var legacy = await store.LoadAsync(); bool invalid = false;
            try { new DesktopSpaceOptions { VideoPath = @"\\remote\movie.mp4" }.Validate(); } catch (InvalidDataException) { invalid = true; }
            Check("Legacy settings remain readable and new desktop space defaults off with local-only validation", legacy.Settings.SchemaVersion == 3 && !legacy.Settings.DesktopSpace.Enabled && invalid);
            controller.ShowDesktopSpace();
            for (int i = 0; i < 30 && controller.SpaceWindow is null; i++) await Task.Delay(100);
            var space = controller.SpaceWindow ?? throw new IOException("Desktop space missing"); await space.RefreshFilesAsync(); space.UpdateLayout();
            var expected = DesktopFiles.Read();
            Check("Desktop space reads real desktop entries and fits detected work area", space.IsVisible && space.ItemCount == expected.Count && space.ActualWidth > 500 && space.ActualHeight > 300);
            var icon = new DesktopIconPosition(expected.FirstOrDefault()?.Path ?? Path.Combine(DesktopFiles.Roots[0], "layout-test.txt"), 120, 180);
            await controller.SaveDesktopIconsAsync([icon]); controller.ViewModel.Undo();
            var settings = (await store.LoadAsync()).Settings;
            Check("Icon layout persists atomically and appearance undo preserves independent space options", settings.DesktopSpace.Icons.Single() == icon && controller.ViewModel.Settings.DesktopSpace.Icons.Single() == icon);
            var folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(directory);
            var file = await folder.CreateFileAsync("fixture.mp4", Windows.Storage.CreationCollisionOption.ReplaceExisting);
            var composition = new Windows.Media.Editing.MediaComposition(); composition.Clips.Add(Windows.Media.Editing.MediaClip.CreateFromColor(Windows.UI.Color.FromArgb(255, 74, 42, 105), TimeSpan.FromSeconds(2)));
            var encoding = Windows.Media.MediaProperties.MediaEncodingProfile.CreateMp4(Windows.Media.MediaProperties.VideoEncodingQuality.Wvga);
            var rendered = await composition.RenderToFileAsync(file, Windows.Media.Editing.MediaTrimmingPreference.Precise, encoding);
            if (rendered != Windows.Media.Transcoding.TranscodeFailureReason.None) throw new IOException("Fixture video encode: " + rendered);
            await controller.SetDesktopVideoAsync(file.Path); space.LoadOptions();
            for (int i = 0; i < 80 && !space.MediaReady; i++) await Task.Delay(100);
            var ready = space.MediaReady; space.Hide();
            Check("Real local MP4 imports decodes and pauses when space is hidden", ready && !space.Playing && (await store.LoadAsync()).Settings.DesktopSpace.VideoPath is not null);
            await controller.SetDesktopVideoAsync(null); space.LoadOptions(); space.Show(); space.Place();
            // Intro clip QA (not one of the 12 checks): real MP4 length probe, animated GIF decode and both imports.
            var qa = new List<string>();
            qa.Add("mp4 length: " + await IntroMedia.VideoDurationAsync(file.Path));
            await controller.SetIntroMediaAsync(file.Path); qa.Add("mp4 import: " + ((await store.LoadAsync()).Settings.DesktopSpace.IntroMediaPath is { } introMp4 && IntroWindow.CustomClip((await store.LoadAsync()).Settings, directory) == introMp4));
            var hashName = Path.Combine(directory, "klip #1 %20.mp4"); File.Copy(file.Path, hashName, true);
            await controller.SetIntroMediaAsync(hashName); qa.Add("name with # and %: " + ((await store.LoadAsync()).Settings.DesktopSpace.IntroMediaPath is not null) + " / " + controller.ViewModel.Status);
            var convertedClip = await IntroMedia.ConvertAsync(file.Path, directory); qa.Add("convert to H.264: " + (await IntroMedia.ProbeAsync(convertedClip))); File.Delete(convertedClip);
            var gifPath = Path.Combine(directory, "fixture.gif");
            using (var gifStream = File.Create(gifPath))
            {
                var encoder = new GifBitmapEncoder();
                foreach (var shade in new[] { Colors.MediumPurple, Colors.Plum, Colors.SlateBlue })
                { var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) dc.DrawRectangle(new SolidColorBrush(shade), null, new Rect(0, 0, 64, 64)); var frame = new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32); frame.Render(visual); encoder.Frames.Add(BitmapFrame.Create(frame)); }
                encoder.Save(gifStream);
            }
            var decoded = IntroMedia.LoadGif(gifPath); qa.Add($"gif frames: {decoded.Frames.Count}, length: {decoded.Duration}");
            await controller.SetIntroMediaAsync(gifPath); qa.Add("gif import: " + ((await store.LoadAsync()).Settings.DesktopSpace.IntroMediaPath?.EndsWith(".gif") == true));
            await controller.SetIntroMediaAsync(null); qa.Add("reset: " + ((await store.LoadAsync()).Settings.DesktopSpace.IntroMediaPath is null));
            await File.WriteAllLinesAsync(Path.Combine(directory, "intro-qa.txt"), qa);
            await controller.UseOriginalMascotAsync();
            var mascot = controller.ViewModel.Settings.Mascot;
            Check("Original moon mascot generates both expressions and enables existing mascot host", mascot.Enabled && File.Exists(mascot.ImagePath) && File.Exists(mascot.HappyImagePath) && controller.Windows.OfType<MascotWindow>().Any());
            bool blocked = false; try { WallpaperEngineService.Arguments("system.exec"); } catch (ArgumentException) { blocked = true; }
            var absent = await controller.SendWallpaperEngineAsync("getWallpaper");
            var prior = Path.Combine(directory, "prior-project.json"); await File.WriteAllTextAsync(prior, "{}");
            var fake = new EngineFixture { Current = prior }; var journal = Path.Combine(directory, "engine-fixture.json");
            var engineSession = new ExternalWallpaperSession(fake, journal); var executable = Path.Combine(directory, "wallpaper64.exe");
            await engineSession.ApplyAsync(executable, file.Path); await engineSession.RestoreAsync(); var restoredPrior = fake.Current == prior && !File.Exists(journal);
            await engineSession.ApplyAsync(executable, file.Path); fake.Current = Path.Combine(directory, "user-choice.json"); await engineSession.RestoreAsync();
            Check("WE command allowlist missing-install fallback and owned-file restore preserve external changes", blocked && WallpaperEngineService.Arguments("pause").SequenceEqual(new[] { "-control", "pause" }) && absent.Contains("seç") && restoredPrior && fake.Current.EndsWith("user-choice.json") && !File.Exists(journal));
            space.UpdateLayout(); Export(space, Path.Combine(directory, "Noxelyra-desktop.png"));
            OriginalArt.Save(OriginalArt.Mascot(), Path.Combine(directory, "Nox.png"), 256, 256);
            // Visual QA only (not a check): the bar over the night art in every palette.
            foreach (var palette in ThemePresets.All) { await controller.SetThemeAsync(palette.Id); panel.UpdateLayout(); ExportOver(panel, Path.Combine(directory, $"Noxelyra-panel-{palette.Id}.png")); }
            var lighten = new LightenWindow(controller) { Height = 1400 }; lighten.Show(); lighten.UpdateLayout(); Export(lighten, Path.Combine(directory, "Noxelyra-lighten.png")); lighten.Close();
            dock.UpdateLayout(); ExportOver(dock, Path.Combine(directory, "Noxelyra-dock.png"));
            await controller.SetThemeAsync("midnight-claxe"); var floatingWidth = panel.Width; panel.Width = 1920; panel.UpdateLayout(); ExportOver(panel, Path.Combine(directory, "Noxelyra-panel-wide.png")); panel.Width = floatingWidth;
        }
        catch (Exception ex) { error = ex.ToString(); }
        finally
        {
            other?.Close();
            await File.WriteAllTextAsync(Path.Combine(directory, "noxelyra-results.json"), JsonSerializer.Serialize(new { success = error is null, checks, error }, new JsonSerializerOptions { WriteIndented = true }));
            controller.Exit();
        }
    }
    private static void ExportOver(Window window, string file)
    {
        int width = (int)Math.Ceiling(window.ActualWidth), height = (int)Math.Ceiling(window.ActualHeight) + 24;
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
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(file); png.Save(stream);
    }
    private sealed class EngineFixture : IExternalWallpaperService
    {
        internal string Current = "";
        public Task<string> ReadAsync(string executable, CancellationToken token = default) => Task.FromResult(Current);
        public Task ApplyAsync(string executable, string file, CancellationToken token = default) { Current = file; return Task.CompletedTask; }
    }
    [DllImport("user32.dll")] private static extern bool PostMessage(nint hwnd, uint message, nint w, nint l);
}
