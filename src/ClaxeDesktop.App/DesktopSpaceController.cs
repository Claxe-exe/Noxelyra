using System.Windows;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

public sealed partial class DesktopController
{
    private DesktopSpaceWindow? desktopSpace;
    private WindowsDesktopMonitor? desktopMonitor;
    private readonly System.Windows.Threading.DispatcherTimer desktopFollowDelay = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private bool desktopSuppressed;
    private bool desktopFollowTimerAttached;
    internal nint ForegroundWindow => windowService.Foreground;
    internal void RaiseDesktopParts() { foreach (var window in windows.Where(w => w.IsVisible)) RaiseDesktopPart(new System.Windows.Interop.WindowInteropHelper(window).Handle, 0, 0, 0, 0, 0, 0x0013); }
    internal DesktopSpaceWindow? SpaceWindow => desktopSpace;
    private void StartDesktopSpace() { _ = RestoreExternalWallpaperAsync(); ConfigureDesktopFollowing(); ConfigureShowDesktop(); if (!safe && committed.DesktopSpace.Enabled) OpenDesktopSpace(!committed.DesktopSpace.FollowDesktop); }
    public async void ShowDesktopSpace()
    {
        if (safe || ReadOnly || exiting) { ViewModel.Status = "Güvenli/salt okunur modda masaüstü alanı açılamaz."; return; }
        desktopSuppressed = false;
        if (await ConfigureDesktopSpaceAsync(true)) OpenDesktopSpace();
    }
    private void OpenDesktopSpace(bool activate = true)
    {
        if (desktopSpace is null)
        {
            desktopSpace = new(this);
            desktopSpace.Closed += (_, _) => desktopSpace = null;
        }
        desktopSpace.Reveal(activate);
        if (activate || WindowsDesktopMonitor.Presence() == DesktopPresence.Desktop)
            foreach (var window in windows.Where(w => w.IsVisible)) RaiseDesktopPart(new System.Windows.Interop.WindowInteropHelper(window).Handle, 0, 0, 0, 0, 0, 0x0013);
        else WindowsDesktopMonitor.PlaceBehindApplications(new System.Windows.Interop.WindowInteropHelper(desktopSpace).Handle);
    }
    internal async Task<bool> ConfigureDesktopSpaceAsync(bool enabled)
    {
        if (safe || ReadOnly || exiting) return false;
        var saved = await PersistDesktopSpaceAsync(options => options with { Enabled = enabled });
        if (saved) ConfigureDesktopFollowing(); return saved;
    }
    private async Task<bool> PersistDesktopSpaceAsync(Func<DesktopSpaceOptions, DesktopSpaceOptions> change)
    {
        await persistenceGate.WaitAsync();
        try
        {
            if (safe || ReadOnly || exiting) return false;
            var options = change(committed.DesktopSpace); options.Validate();
            var next = committed with { DesktopSpace = options }; await store.SaveAsync(next); committed = next; ViewModel.SetAdvanced(next);
            if (!options.Enabled) desktopSpace?.Hide();
            ConfigureShowDesktop();
            ViewModel.Status = "Masaüstü alanı tercihi kaydedildi.";
            return true;
        }
        catch (Exception ex) { Log.Write("desktop-space-save-failed", ex); ViewModel.Status = "Masaüstü alanı kaydedilemedi: " + ex.Message; return false; }
        finally { persistenceGate.Release(); }
    }
    internal Task<bool> SetDesktopMonitorAsync(string id) => PersistDesktopSpaceAsync(options => options with { MonitorId = id });
    internal Task<bool> SetDesktopIconsVisibleAsync(bool visible) => PersistDesktopSpaceAsync(options => options with { ShowIcons = visible });
    internal Task<bool> SaveDesktopIconsAsync(List<DesktopIconPosition> icons) => PersistDesktopSpaceAsync(options => options with { Icons = icons.Take(300).ToList() });
    internal async Task SetDesktopVideoAsync(string? file)
    {
        if (safe || ReadOnly || exiting) return;
        try
        {
            if (file is not null) new DesktopSpaceOptions { VideoPath = file }.Validate();
            var asset = file is null ? null : await ImportDesktopAssetAsync(file, "video-assets", 1073741824, "En fazla 1 GB yerel video seç.");
            await PersistDesktopSpaceAsync(options => options with { VideoPath = asset });
        }
        catch (Exception ex) { Log.Write("desktop-video-import-failed", ex); ViewModel.Status = "Video içe aktarılamadı: " + ex.Message; }
    }
    internal async Task SetDesktopImageAsync(string? file)
    {
        if (safe || ReadOnly || exiting) return;
        try
        {
            if (file is not null) new DesktopSpaceOptions { ImagePath = file }.Validate();
            var asset = file is null ? null : await ImportDesktopAssetAsync(file, "image-assets", 67108864, "En fazla 64 MB yerel görsel seç.");
            if (await PersistDesktopSpaceAsync(options => options with { ImagePath = asset })) ViewModel.Status = asset is null ? "Özgün gece arka planı kullanılıyor." : "Masaüstü arka planı kaydedildi. Windows arka planı değiştirilmedi.";
        }
        catch (Exception ex) { Log.Write("desktop-image-import-failed", ex); ViewModel.Status = "Görsel içe aktarılamadı: " + ex.Message; }
    }
    // Copies a user-chosen local file into Noxelyra's managed folder so later moves/deletes of the original cannot break the layer.
    private async Task<string> ImportDesktopAssetAsync(string file, string folder, long maxBytes, string sizeError)
    {
        if (!Path.IsPathFullyQualified(file) || file.StartsWith(@"\\") || new DriveInfo(Path.GetPathRoot(file)!).DriveType == DriveType.Network || (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0 || new FileInfo(file).Length <= 0 || new FileInfo(file).Length > maxBytes) throw new InvalidDataException(sizeError);
        var root = Path.Combine(desktopData, folder); Directory.CreateDirectory(root);
        var asset = Path.Combine(root, Guid.NewGuid().ToString("N") + Path.GetExtension(file).ToLowerInvariant());
        var temporary = asset + ".tmp";
        try
        {
            await using (var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true)) { await input.CopyToAsync(output, lifetime.Token); await output.FlushAsync(lifetime.Token); }
            File.Move(temporary, asset);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return asset;
    }
    internal void ValidateDesktopVideo(string file) => ValidateDesktopAsset(file, "video-assets", "Kayıtlı yerel video bulunamadı veya geçersiz.");
    internal void ValidateDesktopImage(string file) => ValidateDesktopAsset(file, "image-assets", "Kayıtlı arka plan görseli bulunamadı veya geçersiz.");
    private void ValidateDesktopAsset(string file, string folder, string error)
    {
        var root = Path.GetFullPath(Path.Combine(desktopData, folder)) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(file).StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(file) || (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException(error);
    }
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowPos")]
    private static extern bool RaiseDesktopPart(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    private void DesktopSpaceActivity()
    {
        if (desktopSpace is null) return;
        var monitor = ViewModel.Monitors.FirstOrDefault(m => m.Id == committed.DesktopSpace.MonitorId) ?? ViewModel.Monitors.First(m => m.Primary);
        desktopSpace.UpdateActivity(locked || suspended || safe || windowService.IsFullscreenOn(monitor));
    }
    internal async Task<bool> ConfigureDesktopFollowAsync(bool enabled)
    {
        if (safe || ReadOnly || exiting) return false;
        var saved = await PersistDesktopSpaceAsync(options => options with { FollowDesktop = enabled, Enabled = enabled || options.Enabled });
        if (saved)
        {
            desktopSuppressed = false; ConfigureDesktopFollowing(); desktopSpace?.LoadOptions();
            if (enabled) OpenDesktopSpace();
        }
        return saved;
    }
    private bool taskbarHiddenByUs;
    // Explorer re-shows its taskbars after restarts and display changes; the 1 s tick hides them again while the option is on.
    private void KeepTaskbarHidden()
    {
        var wanted = !safe && !exiting && committed.HideWindowsTaskbar && committed.DesktopSpace.HideTaskbarCompletely;
        // Never hide without a live guardian that shows the taskbars again if Noxelyra dies.
        if (wanted && !guardianStarted) { if (!guardianStarting && DateTime.UtcNow - guardianFailedAt > TimeSpan.FromMinutes(1)) _ = StartGuardianForHidingAsync(); return; }
        try
        {
            if (wanted && TaskbarWindows.AnyVisible()) { TaskbarWindows.SetVisible(false); taskbarHiddenByUs = true; }
            else if (!wanted && taskbarHiddenByUs) { TaskbarWindows.SetVisible(true); taskbarHiddenByUs = false; }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { Log.Write("taskbar-hide-failed", ex); }
    }
    private bool guardianStarting;
    private DateTime guardianFailedAt = DateTime.MinValue;
    private async Task StartGuardianForHidingAsync()
    {
        guardianStarting = true;
        try { await EnsureTaskbarGuardianAsync(); }
        catch (Exception ex) { guardianFailedAt = DateTime.UtcNow; Log.Write("taskbar-hide-guardian-failed", ex); ViewModel.Status = "Kurtarma yardımcısı hazır değil; görev çubukları gizlenmedi."; }
        finally { guardianStarting = false; }
    }
    internal async Task<bool> SetHideTaskbarCompletelyAsync(bool hidden)
    {
        if (hidden && !committed.HideWindowsTaskbar && !await ConfigureWindowsTaskbarAsync(true)) return false;
        var saved = await PersistDesktopSpaceAsync(options => options with { HideTaskbarCompletely = hidden });
        KeepTaskbarHidden();
        if (saved) ViewModel.Status = hidden ? "Windows görev çubukları Noxelyra açıkken tamamen gizli. Win tuşu Başlat'ı açmaya devam eder." : "Windows görev çubukları yeniden görünür.";
        return saved;
    }
    private ShowDesktopHook? showDesktopHook;
    private bool noxelyraDesktopShown;
    internal bool ShowDesktopCaptured => showDesktopHook is not null;
    internal Task<bool> SetCaptureShowDesktopAsync(bool enabled) => PersistDesktopSpaceAsync(options => options with { CaptureShowDesktop = enabled, Enabled = enabled || options.Enabled });
    private void ConfigureShowDesktop()
    {
        var wanted = !safe && !exiting && committed.DesktopSpace.Enabled && committed.DesktopSpace.CaptureShowDesktop;
        if (!wanted) { showDesktopHook?.Dispose(); showDesktopHook = null; return; }
        if (showDesktopHook is not null) return;
        try { showDesktopHook = new(() => Application.Current.Dispatcher.BeginInvoke(ToggleNoxelyraDesktop)); Log.Write("show-desktop-hook-installed"); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { Log.Write("show-desktop-hook-failed", ex); ViewModel.Status = "Win+D yakalanamadı; Windows'un kendi masaüstü davranışı sürüyor."; }
    }
    // Super+D like a Linux desktop: first press minimizes apps onto the Noxelyra desktop, the next press brings them back.
    internal void ToggleNoxelyraDesktop()
    {
        if (exiting || locked || suspended) return;
        dynamic? shell = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
            var onDesktop = WindowsDesktopMonitor.Presence() == DesktopPresence.Desktop || (desktopSpace is not null && ForegroundWindow == new System.Windows.Interop.WindowInteropHelper(desktopSpace).Handle);
            if (noxelyraDesktopShown && onDesktop) { shell!.UndoMinimizeALL(); noxelyraDesktopShown = false; return; }
            desktopSuppressed = false; shell!.MinimizeAll(); OpenDesktopSpace(true); noxelyraDesktopShown = true;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException) { Log.Write("show-desktop-failed", ex); ViewModel.Status = "Masaüstü gösterilemedi: " + ex.Message; }
        finally { if (shell is not null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
    }
    public void DismissDesktopSpace() { desktopSuppressed = true; desktopSpace?.Hide(); }
    private void ConfigureDesktopFollowing()
    {
        var enabled = !safe && committed.DesktopSpace.Enabled && committed.DesktopSpace.FollowDesktop;
        if (!enabled) { desktopMonitor?.Dispose(); desktopMonitor = null; desktopFollowDelay.Stop(); return; }
        if (desktopMonitor is not null) return;
        if (!desktopFollowTimerAttached)
        {
            desktopFollowDelay.Tick += (_, _) => { desktopFollowDelay.Stop(); ProcessDesktopPresence(WindowsDesktopMonitor.Presence()); };
            desktopFollowTimerAttached = true;
        }
        try
        {
            desktopMonitor = new(() => Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (exiting || suspended || desktopMonitor is null) return;
                desktopFollowDelay.Stop(); desktopFollowDelay.Start();
            }));
        }
        catch (Exception ex) { Log.Write("desktop-follow-unavailable", ex); ViewModel.Status = "Masaüstünü izleme açılamadı. Alanı bildirim menüsünden elle açabilirsin."; }
    }
    internal void ProcessDesktopPresence(DesktopPresence presence)
    {
        if (presence == DesktopPresence.Application) desktopSuppressed = false;
        var options = committed.DesktopSpace;
        switch (DesktopFollowPolicy.Decide(options.Enabled, options.FollowDesktop, safe || suspended, locked, desktopSuppressed, presence))
        {
            case DesktopFollowAction.Reveal:
                OpenDesktopSpace(false); break;
            case DesktopFollowAction.Lower:
                if (desktopSpace is not null && desktopSpace.IsVisible && !WindowsDesktopMonitor.PlaceBehindApplications(new System.Windows.Interop.WindowInteropHelper(desktopSpace).Handle))
                { Log.Write("desktop-follow-lower-failed"); ViewModel.Status = "Alan uygulamaların arkasına yerleşemedi. Esc ile Windows'a dönebilirsin."; }
                break;
        }
    }
    private void StopDesktopSpace() { showDesktopHook?.Dispose(); showDesktopHook = null; desktopFollowDelay.Stop(); desktopMonitor?.Dispose(); desktopMonitor = null; desktopSpace?.Close(); }
    internal async Task UseOriginalMascotAsync()
    {
        try
        {
            var root = Path.Combine(desktopData, "original-art"); var idle = Path.Combine(root, "Nox-idle.png"); var happy = Path.Combine(root, "Nox-happy.png");
            OriginalArt.Save(OriginalArt.Mascot(), idle, 256, 256); OriginalArt.Save(OriginalArt.Mascot(true), happy, 256, 256);
            await ConfigureMascotAsync(true, imageFile: idle, happyFile: happy);
        }
        catch (Exception ex) { Log.Write("original-mascot-failed", ex); ViewModel.Status = "Özgün maskot açılamadı: " + ex.Message; }
    }
    internal async Task UseOriginalWallpaperAsync()
    {
        if (safe || ReadOnly) return;
        try
        {
            var path = Path.Combine(desktopData, "original-art", "Noxelyra-night.png"); OriginalArt.Save(OriginalArt.Wallpaper(), path, 1600, 900);
            var display = WallpaperPlatform.Displays().FirstOrDefault() ?? throw new IOException("Windows arka plan ekranı bulunamadı.");
            await ApplyWallpaperSetAsync(display.Id, [path], 120);
        }
        catch (Exception ex) { Log.Write("original-wallpaper-failed", ex); ViewModel.Status = "Özgün arka plan uygulanamadı: " + ex.Message; }
    }
    internal async Task<string> RestoreExternalWallpaperAsync()
    {
        try { await externalWallpaper.RestoreAsync(); return "Önceki Wallpaper Engine arka planı geri yüklendi veya harici tercihin korundu."; }
        catch (Exception ex) { Log.Write("engine-recovery-pending", ex); return "WE kurtarması bekliyor: " + ex.Message; }
    }
    internal async Task<string> ApplyExternalVideoAsync()
    {
        if (safe || ReadOnly) return "Güvenli/salt okunur modda WE değiştirilmez.";
        try
        {
            var options = committed.DesktopSpace;
            var executable = options.EnginePath ?? throw new InvalidOperationException("Önce Wallpaper Engine uygulamasını seç.");
            var video = options.VideoPath ?? throw new InvalidOperationException("Önce masaüstü alanında yerel video seç.");
            ValidateDesktopVideo(video); await externalWallpaper.ApplyAsync(executable, video, lifetime.Token);
            return "WE varsayılan ekranındaki video doğrulandı. Çıkışta önceki dosya geri yüklenir; harici değişiklik korunur.";
        }
        catch (Exception ex) { Log.Write("engine-video-apply-failed", ex); return "WE video uygulanamadı: " + ex.Message; }
    }
    internal Task<bool> SetWallpaperEnginePathAsync(string path) => PersistDesktopSpaceAsync(options => options with { EnginePath = path });
    internal async Task<string> SendWallpaperEngineAsync(string command)
    {
        if (safe || ReadOnly) return "Güvenli/salt okunur modda harici komut gönderilmez.";
        try
        {
            var path = committed.DesktopSpace.EnginePath ?? throw new InvalidOperationException("Önce Wallpaper Engine uygulamasını seç.");
            return await WallpaperEngineService.SendAsync(path, command, lifetime.Token);
        }
        catch (Exception ex) { Log.Write("wallpaper-engine-command-failed", ex); return "Wallpaper Engine: " + ex.Message; }
    }
}
