using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

/// <summary>Wallpaper colours, rice export/import, tray access while the taskbar is hidden, and a one-time memory trim after start.</summary>
public sealed partial class DesktopController
{
    private string RiceBackups => Path.Combine(desktopData, "rice-backups");
    internal bool HasRiceBackup => Directory.Exists(RiceBackups) && Directory.EnumerateFiles(RiceBackups, "*" + RiceFiles.Extension).Any();

    // ---- Wallpaper → theme colours (pywal-like) ----
    /// <summary>The image to read colours from: the Noxelyra desktop image, else the Windows wallpaper of the first display.</summary>
    internal string? WallpaperSource()
    {
        if (committed.DesktopSpace.ImagePath is { } image && File.Exists(image)) return image;
        IReadOnlyList<WallpaperDisplay> displays;
        try { displays = WallpaperPlatform.Displays(); }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException or UnauthorizedAccessException) { Log.Write("wallpaper-source-failed", ex); return null; }
        // An inactive display entry can refuse GetWallpaper; try the next one.
        foreach (var display in displays)
        {
            try { if (WallpaperPlatform.Read(display.Id) is { Length: > 0 } path && File.Exists(path) && RiceFiles.WallpaperTypes.Contains(Path.GetExtension(path).ToLowerInvariant())) return path; }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException or UnauthorizedAccessException) { Log.Write("wallpaper-source-skipped", ex); }
        }
        return null;
    }
    /// <summary>Generates a readable palette from the image and applies it as the "Duvar kâğıdından" theme.</summary>
    internal async Task<bool> UseWallpaperPaletteAsync(string? file = null)
    {
        if (safe || ReadOnly) { ViewModel.Status = "Güvenli/salt okunur modda tema kaydedilmez."; return false; }
        file ??= WallpaperSource();
        if (file is null) { ViewModel.Status = "Renk alınacak bir görsel bulunamadı. Kontrol merkezinden 'Görselden renk al' ile seçebilirsin."; return false; }
        ThemePalette palette;
        try
        {
            if (new FileInfo(file).Length > 64L * 1024 * 1024) throw new InvalidDataException("Görsel 64 MB'tan büyük.");
            var pixels = ReadPixels(file);
            palette = await Task.Run(() => WallpaperPalette.Generate(pixels));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException or FileFormatException or ArgumentException or System.Runtime.InteropServices.COMException)
        { Log.Write("wallpaper-palette-failed", ex); ViewModel.Status = "Görselden renk alınamadı: " + ex.Message; return false; }
        await persistenceGate.WaitAsync();
        try
        {
            Settings Change(Settings s) => s with { ThemeId = ThemePresets.WallpaperId, CustomPalette = palette, PrimaryAccent = null, SecondaryAccent = null };
            var next = Change(committed); next.Validate(); await store.SaveAsync(next); committed = next;
            ViewModel.Edit(Change(ViewModel.Settings));
            ViewModel.Status = "Renkler duvar kâğıdından alındı. Eski temaya dönmek için palet menüsünden bir tema seç."; return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { Log.Write("wallpaper-palette-save-failed", ex); ViewModel.Status = "Tema kaydedilemedi: " + ex.Message; return false; }
        finally { persistenceGate.Release(); }
    }
    // A 96 px thumbnail is plenty for colour statistics and decodes quickly even for 4K photos.
    private static List<(byte R, byte G, byte B)> ReadPixels(string file)
    {
        var image = new BitmapImage();
        image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile; image.DecodePixelWidth = 96; image.UriSource = new Uri(file); image.EndInit();
        var bitmap = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var stride = bitmap.PixelWidth * 4; var bytes = new byte[stride * bitmap.PixelHeight]; bitmap.CopyPixels(bytes, stride, 0);
        var pixels = new List<(byte, byte, byte)>(bitmap.PixelWidth * bitmap.PixelHeight);
        for (int i = 0; i < bytes.Length; i += 4) if (bytes[i + 3] > 32) pixels.Add((bytes[i + 2], bytes[i + 1], bytes[i]));
        return pixels;
    }

    // ---- Rice export / import ----
    internal string? RiceWallpaper => committed.DesktopSpace.ImagePath is { } image && File.Exists(image) && RiceFiles.WallpaperTypes.Contains(Path.GetExtension(image).ToLowerInvariant()) ? image : null;
    internal string? RiceIntro => IntroWindow.CustomClip(committed, desktopData) is { } clip && File.Exists(clip) && RiceFiles.IntroTypes.Contains(Path.GetExtension(clip).ToLowerInvariant()) ? clip : null;
    internal async Task<string> ExportRiceAsync(string path, bool wallpaper, bool intro)
    {
        try
        {
            var rice = RiceFiles.Capture(committed, Path.GetFileNameWithoutExtension(path), DateTimeOffset.Now, wallpaper, RiceWallpaper, intro, RiceIntro);
            await Task.Run(() => RiceFiles.Write(path, rice, RiceWallpaper, RiceIntro));
            Log.Write("rice-exported");
            return $"Rice kaydedildi: {Path.GetFileName(path)}. Not metinleri, hava durumu şehri, kısayollar ve Windows ayarları dosyada yok.";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { Log.Write("rice-export-failed", ex); return "Rice kaydedilemedi: " + ex.Message; }
    }
    /// <summary>Reads and checks a .noxrice; media goes to a private temporary folder that <see cref="ApplyRiceAsync"/> cleans up.</summary>
    internal (RiceManifest Rice, string? Wallpaper, string? Intro, string Folder) ReadRice(string path)
    {
        var folder = Path.Combine(desktopData, "rice-import", Guid.NewGuid().ToString("N"));
        try { var (rice, wallpaper, intro) = RiceFiles.Read(path, folder); return (rice, wallpaper, intro, folder); }
        catch { try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch (IOException) { } throw; }
    }
    internal async Task<string> ApplyRiceAsync(RiceManifest rice, string? wallpaper, string? intro, string folder, bool replaceWidgets, bool backup = true)
    {
        try
        {
            if (safe || ReadOnly) return "Güvenli/salt okunur modda rice uygulanmaz.";
            if (Widgets.Editing) return "Önce widget atölyesindeki taslağı kaydet veya kapat.";
            if (backup)
            {
                Directory.CreateDirectory(RiceBackups);
                var snapshot = RiceFiles.Capture(committed, "Önceki görünüm", DateTimeOffset.Now, false, null, false, null, personal: true);
                RiceFiles.Write(Path.Combine(RiceBackups, $"onceki-{DateTime.Now:yyyyMMdd-HHmmss}{RiceFiles.Extension}"), snapshot, null, null);
                foreach (var old in new DirectoryInfo(RiceBackups).GetFiles("*" + RiceFiles.Extension).OrderByDescending(f => f.LastWriteTimeUtc).Skip(5)) old.Delete();
            }
            await persistenceGate.WaitAsync();
            Settings next;
            try { next = RiceFiles.Apply(rice, committed, replaceWidgets); await store.SaveAsync(next); committed = next; }
            finally { persistenceGate.Release(); }
            ViewModel.Replace(next);
            if (replaceWidgets) Widgets.LoadSaved(next.Widgets);
            ConfigureKeyboard();
            if (wallpaper is not null) await SetDesktopImageAsync(wallpaper);
            if (intro is not null) await SetIntroMediaAsync(intro);
            Log.Write("rice-applied");
            return $"\"{rice.Name}\" uygulandı." + (backup ? " Önceki görünüm yedeklendi; 'Önceki görünüme dön' ile geri alabilirsin." : "");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException) { Log.Write("rice-apply-failed", ex); return "Rice uygulanamadı: " + ex.Message; }
        finally { try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch (IOException) { } }
    }
    internal async Task<string> RestoreRiceBackupAsync()
    {
        var latest = Directory.Exists(RiceBackups) ? new DirectoryInfo(RiceBackups).GetFiles("*" + RiceFiles.Extension).OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault() : null;
        if (latest is null) return "Geri dönülecek bir yedek yok.";
        try
        {
            var (rice, wallpaper, intro, folder) = ReadRice(latest.FullName);
            var message = await ApplyRiceAsync(rice, wallpaper, intro, folder, replaceWidgets: true, backup: false);
            if (message.Contains("uygulandı", StringComparison.Ordinal)) { latest.Delete(); return "Önceki görünüme dönüldü."; }
            return message;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { return "Yedek okunamadı: " + ex.Message; }
    }

    // ---- Tray access while the taskbar is hidden ----
    private DateTime trayPeekUntil, trayPeekStarted;
    private static readonly string[] TrayClasses = ["Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow", "TopLevelWindowForOverflowXamlIsland", "Xaml_WindowedPopupClass"];
    /// <summary>Bar button: shows hidden taskbars for a moment and focuses the notification area (Win+B, a documented Windows shortcut).</summary>
    internal void ShowTray()
    {
        if (exiting) return;
        try
        {
            if (taskbarHiddenByUs) { TaskbarWindows.SetVisible(true); trayPeekStarted = DateTime.UtcNow; trayPeekUntil = trayPeekStarted.AddSeconds(12); }
            KeyboardHook.Chord(0x5B, 0x42);
            ViewModel.Status = "Sistem tepsisi açıldı. Görev çubuğu kendiliğinden yeniden gizlenir.";
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { Log.Write("tray-peek-failed", ex); ViewModel.Status = "Sistem tepsisine ulaşılamadı."; }
    }
    // While the user is still in the tray, keep the taskbar up a little longer (at most a minute).
    private bool TrayPeeking()
    {
        var now = DateTime.UtcNow;
        if (now >= trayPeekUntil) return false;
        if (now > trayPeekUntil.AddSeconds(-2) && now - trayPeekStarted < TimeSpan.FromMinutes(1) && WindowOps.Read(WindowOps.Foreground) is { } focused && TrayClasses.Contains(focused.ClassName)) trayPeekUntil = now.AddSeconds(4);
        return true;
    }

    // ---- Memory ----
    // Once the start-up burst (intro, icons, first layout) is over, return its pages to Windows; steady use then grows only by what it needs.
    private void TrimMemoryLater()
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        timer.Tick += (_, _) =>
        {
            timer.Stop(); if (exiting) return;
            GCSettingsCompact();
            try { WorkingSet.Trim(); Log.Write("memory-trimmed"); } catch (System.ComponentModel.Win32Exception ex) { Log.Write("memory-trim-failed", ex); }
        };
        timer.Start();
    }
    private static void GCSettingsCompact()
    {
        System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    }
}
