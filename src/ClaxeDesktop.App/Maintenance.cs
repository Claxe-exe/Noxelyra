using System.Windows;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

internal static class Maintenance
{
    internal static async Task<int> RunAsync()
    {
        var data = Environment.GetEnvironmentVariable("CLAXE_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaxeDesktop");
        var log = new LocalLog(Path.Combine(data, "logs"));
        var fixture = Environment.GetEnvironmentVariable("CLAXE_MAINTENANCE_FIXTURE") == "1";
        using var gate = new Mutex(false, fixture ? "Local\\ClaxeDesktop.IntegrationVerification" : "Local\\ClaxeDesktop.Phase1");
        bool owned = false;
        try
        {
            if (!fixture) await DesktopCommands.SendAsync("exit");
            for (int i = 0; i < 100 && !owned; i++)
            {
                try { owned = gate.WaitOne(0); } catch (AbandonedMutexException) { owned = true; }
                if (!owned) await Task.Delay(100);
            }
            if (!owned) throw new IOException("Noxelyra açık kaldı. Bildirim alanından çıkış yapıp yeniden dene.");
            TaskbarWindows.SetVisible(true);
            await new TaskbarSession(new WindowsTaskbarService(), Path.Combine(data, "taskbar-recovery.json"), TaskbarGuardian.CurrentOwner()).RestoreAsync();
            new WallpaperSession(new WindowsWallpaperService(), Path.Combine(data, "wallpaper-recovery.json")).Restore();
            await new ExternalWallpaperSession(new WindowsExternalWallpaperService(), Path.Combine(data, "engine-recovery.json")).RestoreAsync();
            if (File.Exists(Path.Combine(data, "session.lock"))) new RecoveryService(data).Complete();
            log.Write("maintenance-shell-restored");
            return 0;
        }
        catch (Exception ex)
        {
            log.Write("maintenance-blocked", ex);
            if (!fixture) MessageBox.Show("Windows görünümü geri yüklenemedi veya Noxelyra hâlâ açık. Kaldırma durduruldu; uygulama ve kurtarma kayıtları korunuyor.\n\n" + ex.Message, "Noxelyra bakım", MessageBoxButton.OK, MessageBoxImage.Warning);
            return 2;
        }
        finally { if (owned) gate.ReleaseMutex(); }
    }
}
