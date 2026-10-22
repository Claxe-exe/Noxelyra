using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;
public sealed partial class DesktopController
{
    private DesktopBindings? desktopBindings;
    private LauncherWindow? launcherWindow;
    private ControlCenterWindow? controlWindow;
    private WallpaperSession wallpaperSession=null!;
    private ExternalWallpaperSession externalWallpaper = null!;
    private string desktopData="";
    private TaskbarSession taskbarSession=null!;
    private readonly TaskbarOwner taskbarOwner=TaskbarGuardian.CurrentOwner();
    private bool guardianStarted;
    private readonly Dictionary<string,DateTime> rotationDue=new();
    private readonly Dictionary<string,string> rotationCurrent=new();
    private readonly HashSet<string> rotationStopped=[];
    private bool rotating;
    public WindowsWallpaperService WallpaperPlatform { get; }=new();
    public ObservableCollection<string> Activity { get; }=[];
    internal bool HasReservedPanel=>desktopBindings?.Registered==true;
    private void InitializeDesktopServices()
    {
        desktopData=Environment.GetEnvironmentVariable("CLAXE_DATA_DIR")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ClaxeDesktop");
        externalWallpaper = new(new WindowsExternalWallpaperService(), Path.Combine(desktopData, "engine-recovery.json"), taskbarOwner, EnsureTaskbarGuardianAsync);
        wallpaperSession=new(WallpaperPlatform,Path.Combine(desktopData,"wallpaper-recovery.json"));
        taskbarSession=new(new WindowsTaskbarService(),Path.Combine(desktopData,"taskbar-recovery.json"),taskbarOwner);
        ViewModel.PropertyChanged+=(_,e)=>{if(e.PropertyName=="Status"&&!string.IsNullOrWhiteSpace(ViewModel.Status)){Activity.Insert(0,DateTime.Now.ToString("HH:mm")+"  "+ViewModel.Status);while(Activity.Count>8)Activity.RemoveAt(Activity.Count-1);}};
    }
    private void StartDesktopServices()
    {
        var panel=windows.OfType<PanelWindow>().Single();desktopBindings=new(panel,ShowLauncher,ShowControls);
        try{wallpaperSession.Restore();}catch(Exception ex){Log.Write("wallpaper-recovery-pending",ex);ViewModel.Status="Önceki arka planın kurtarılması bekliyor. Kontrol merkezinden yeniden dene.";}
        _ = StartTaskbarAsync();
        if(safe)return;
        try{desktopBindings.Configure(committed.IntegratedDesktop,committed.GlobalShortcuts,ViewModel.PanelHeight);RefreshMonitors();}
        catch(Exception ex){desktopBindings.Configure(false,false,ViewModel.PanelHeight);ViewModel.SetDesktopSettings(committed with{IntegratedDesktop=false,GlobalShortcuts=false});Log.Write("desktop-bindings-failed",ex);ViewModel.Status="Masaüstü entegrasyonu etkinleştirilemedi. Kontrol merkezinden yeniden deneyebilirsin.";}
        if(committed.WallpaperEnabled)
        {
            try{ApplySavedWallpapers(committed);}catch(Exception ex){Log.Write("wallpaper-start-failed",ex);ViewModel.Status="Kayıtlı arka plan uygulanamadı. Windows görünümü kullanılabilir.";}
        }
    }
    public void ShowLauncher()
    {
        if(exiting)return;
        if(launcherWindow is null){launcherWindow=new(this);launcherWindow.Closed+=(_,_)=>launcherWindow=null;}
        launcherWindow.Show();launcherWindow.WindowState=System.Windows.WindowState.Normal;launcherWindow.Render();launcherWindow.Activate();
    }
    public void ShowControls()
    {
        if(exiting)return;
        if(controlWindow is null){controlWindow=new(this);controlWindow.Closed+=(_,_)=>controlWindow=null;}
        controlWindow.Show();controlWindow.WindowState=System.Windows.WindowState.Normal;controlWindow.Activate();
    }
    public void ShowStartupSettings() { ShowSettings();if(settingsWindow is not null)settingsWindow.Editor.SelectedCategory="Ekranlar ve oturum"; }
    public void OpenSystem(string key)
    {
        var uri=key switch{"network"=>"ms-settings:network-wifi","bluetooth"=>"ms-settings:bluetooth","sound"=>"ms-settings:sound","display"=>"ms-settings:display","wallpaper"=>"ms-settings:personalization-background","notifications"=>"ms-settings:notifications","taskbar"=>"ms-settings:taskbar","apps"=>"shell:AppsFolder",_=>null};
        if(uri is null)return;
        try{Process.Start(new ProcessStartInfo(uri){UseShellExecute=true});}catch(Exception ex){Log.Write("system-settings-open-failed",ex);ViewModel.Status="Windows ayarları açılamadı; Başlat menüsünden erişebilirsin.";}
    }
    public async Task<bool> ConfigureDesktopAsync(bool integrated,bool shortcuts)
    {
        if(ReadOnly||safe||desktopBindings is null)return false;
        await persistenceGate.WaitAsync();
        try
        {
            desktopBindings.Configure(integrated,shortcuts,ViewModel.PanelHeight);
            var next=committed with{IntegratedDesktop=integrated,GlobalShortcuts=shortcuts};await store.SaveAsync(next);committed=next;ViewModel.SetDesktopSettings(next);
            RefreshMonitors();ViewModel.Status=integrated?"Panel Windows çalışma alanına yerleşti. Uygulamalar Noxelyra düğmesinde, kontroller sağda.":"Serbest masaüstü görünümüne dönüldü.";Log.Write("desktop-integration-saved");return true;
        }
        catch(Exception ex)
        {
            try{desktopBindings.Configure(committed.IntegratedDesktop,committed.GlobalShortcuts,ViewModel.PanelHeight);}catch{desktopBindings.Configure(false,false,ViewModel.PanelHeight);}
            RefreshMonitors();Log.Write("desktop-integration-failed",ex);ViewModel.Status=ex is InvalidOperationException?ex.Message:"Entegrasyon tercihi kaydedilemedi; önceki durum korundu.";return false;
        }
        finally{persistenceGate.Release();}
    }
    private void ApplySavedWallpapers(Settings settings)
    {
        var connected=WallpaperPlatform.Displays().Select(m=>m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var assignment in settings.Wallpapers.Where(w=>connected.Contains(w.MonitorId)))
        {
            var root=Path.GetFullPath(Path.Combine(desktopData,"wallpaper-assets"))+Path.DirectorySeparatorChar;
            var path=Path.GetFullPath(assignment.AssetPath);
            if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!path.EndsWith(".png",StringComparison.OrdinalIgnoreCase)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Kayıtlı arka plan varlığı geçersiz.");
            wallpaperSession.Apply(assignment.MonitorId,path);
            rotationCurrent[assignment.MonitorId]=path;
        }
    }
    public Task ApplyWallpaperAsync(string monitor,string file)=>ApplyWallpaperSetAsync(monitor,[file],120);
    public async Task ApplyWallpaperSetAsync(string monitor,IReadOnlyList<string> files,int intervalSeconds)
    {
        if(safe||ReadOnly)return;
        await persistenceGate.WaitAsync();
        var changed=false;
        try
        {
            if(files.Count is <1 or >20||intervalSeconds is <30 or >3600)throw new InvalidDataException("1–20 görsel ve en az 30 saniyelik aralık seç.");
            var assets=await Task.Run(()=>
            {
            var imported=new List<string>();
            foreach(var file in files)
            {
            lifetime.Token.ThrowIfCancellationRequested();
            if(!Path.IsPathFullyQualified(file)||file.StartsWith(@"\\")||new DriveInfo(Path.GetPathRoot(file)!).DriveType==DriveType.Network||new FileInfo(file).Length>32*1024*1024)throw new InvalidDataException("En fazla 32 MB yerel görsel seç.");
            var directory=Path.Combine(desktopData,"wallpaper-assets");Directory.CreateDirectory(directory);var asset=Path.Combine(directory,Guid.NewGuid().ToString("N")+".png");
            using(var source=File.OpenRead(file))
            {
                var frame=BitmapFrame.Create(source,BitmapCreateOptions.DelayCreation,BitmapCacheOption.OnDemand);
                if(frame.PixelWidth>16384||frame.PixelHeight>16384||(long)frame.PixelWidth*frame.PixelHeight>20000000)throw new InvalidDataException("Görsel en fazla 20 megapiksel olmalı.");
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(frame);using var output=File.Create(asset);encoder.Save(output);
            }
            imported.Add(asset);
            }
            return imported;
            },lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
            changed=true;wallpaperSession.Apply(monitor,assets[0]);
            var assignments=committed.Wallpapers.Where(w=>w.MonitorId!=monitor).Append(new WallpaperAssignment(monitor,assets[0])).ToList();
            var rotations=committed.WallpaperRotations.Where(r=>r.MonitorId!=monitor).ToList();if(assets.Count>1)rotations.Add(new(monitor,assets,intervalSeconds));
            var next=committed with{WallpaperEnabled=true,Wallpapers=assignments,WallpaperRotations=rotations};await store.SaveAsync(next);committed=next;ViewModel.SetDesktopSettings(next);
            rotationCurrent[monitor]=assets[0];rotationDue[monitor]=DateTime.UtcNow.AddSeconds(intervalSeconds);rotationStopped.Remove(monitor);
            ViewModel.Status=assets.Count>1?"Görsel listesi uygulandı; tam ekran, kilit ve pil durumunda geçişler durur.":"Masaüstü görseli uygulandı. Çıkışta önceki arka plan geri gelecek.";Log.Write("wallpaper-applied");
        }
        catch(Exception ex)
        {
            if(changed)try{wallpaperSession.Restore();if(committed.WallpaperEnabled)ApplySavedWallpapers(committed);}catch(Exception restore){Log.Write("wallpaper-rollback-pending",restore);}
            Log.Write("wallpaper-apply-failed",ex);ViewModel.Status=ex is InvalidDataException or InvalidOperationException?ex.Message:"Görsel uygulanamadı; dosyayı ve Windows arka plan ayarlarını kontrol et.";
        }
        finally{persistenceGate.Release();}
    }
    public async Task DisableWallpaperAsync()
    {
        if(ReadOnly)return;
        await persistenceGate.WaitAsync();
        try{wallpaperSession.Restore();var next=committed with{WallpaperEnabled=false};await store.SaveAsync(next);committed=next;ViewModel.SetDesktopSettings(next);ViewModel.Status="Noxelyra arka planı kapandı; önceki Windows görünümü geri geldi.";}
        catch(Exception ex){Log.Write("wallpaper-restore-failed",ex);ViewModel.Status="Arka plan geri yüklenemedi. Bağlı olmayan ekran veya eksik görsel için kurtarma kaydı korundu.";}
        finally{persistenceGate.Release();}
    }
    private async Task StopDesktopServicesAsync()
    {
        try{await taskbarSession.RestoreAsync(taskbarOwner);}catch(Exception ex){Log.Write("taskbar-exit-restore-pending",ex);}
        try { await externalWallpaper.RestoreAsync(); } catch (Exception ex) { Log.Write("engine-exit-recovery-pending", ex); }
        desktopBindings?.Dispose();launcherWindow?.Close();controlWindow?.Close();
        try{wallpaperSession.Restore();}catch(Exception ex){Log.Write("wallpaper-exit-restore-pending",ex);}
    }
    private async Task EnsureTaskbarGuardianAsync()
    {
        var ready=TaskbarGuardian.ReadyPath(desktopData,taskbarOwner);
        if(guardianStarted&&File.Exists(ready))return;
        var info=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
        info.Environment["CLAXE_DATA_DIR"]=desktopData;info.ArgumentList.Add("--taskbar-guardian");info.ArgumentList.Add(taskbarOwner.ProcessId.ToString());info.ArgumentList.Add(taskbarOwner.StartedUtcTicks.ToString());
        using var helper=Process.Start(info)??throw new InvalidOperationException("Görev çubuğu kurtarma yardımcısı başlatılamadı.");
        for(int i=0;i<40;i++){if(File.Exists(ready)){guardianStarted=true;return;}if(helper.HasExited)break;await Task.Delay(50);}
        throw new InvalidOperationException("Kurtarma yardımcısı hazır olmadığı için görev çubuğu değiştirilmedi.");
    }
    private async Task StartTaskbarAsync()
    {
        await persistenceGate.WaitAsync();
        try{await taskbarSession.RestoreAsync();if(!safe&&committed.HideWindowsTaskbar)await taskbarSession.EnableAsync(EnsureTaskbarGuardianAsync);RefreshMonitors();}
        catch(Exception ex){Log.Write("taskbar-start-failed",ex);ViewModel.Status="Windows görev çubuğu yönetimi başlatılamadı. Kontrol merkezinden yeniden deneyebilirsin.";}
        finally{persistenceGate.Release();}
    }
    private async Task AdvanceWallpaperRotationsAsync()
    {
        if(rotating||exiting||safe||suspended||!committed.WallpaperEnabled||committed.WallpaperRotations.Count==0)return;
        var now=DateTime.UtcNow;foreach(var item in committed.WallpaperRotations)if(!rotationDue.ContainsKey(item.MonitorId))rotationDue[item.MonitorId]=now.AddSeconds(item.IntervalSeconds);
        if(!committed.WallpaperRotations.Any(r=>!rotationStopped.Contains(r.MonitorId)&&rotationDue[r.MonitorId]<=now))return;
        if(WallpaperPausePolicy.Pause(safe,locked,WindowsPowerState.ConservePower(),false))return;
        rotating=true;await persistenceGate.WaitAsync();
        try
        {
            if(exiting)return;var displays=WallpaperPlatform.Displays();
            foreach(var item in committed.WallpaperRotations.Where(r=>!rotationStopped.Contains(r.MonitorId)&&rotationDue[r.MonitorId]<=now))
            {
                var display=displays.FirstOrDefault(d=>d.Id==item.MonitorId);if(display is null)continue;
                var monitor=ViewModel.Monitors.FirstOrDefault(m=>m.X>=display.X&&m.X<display.X+display.Width&&m.Y>=display.Y&&m.Y<display.Y+display.Height);
                if(monitor is null||windowService.IsFullscreenOn(monitor))continue;
                var current=rotationCurrent.GetValueOrDefault(item.MonitorId,committed.Wallpapers.FirstOrDefault(w=>w.MonitorId==item.MonitorId)?.AssetPath??"");
                if(!string.Equals(WallpaperPlatform.Read(item.MonitorId),current,StringComparison.OrdinalIgnoreCase)){rotationStopped.Add(item.MonitorId);ViewModel.Status="Arka plan dışarıdan değişti; bu ekranın Noxelyra geçişleri durduruldu.";continue;}
                var index=item.AssetPaths.FindIndex(p=>string.Equals(p,current,StringComparison.OrdinalIgnoreCase));var next=item.AssetPaths[(index+1)%item.AssetPaths.Count];
                var root=Path.GetFullPath(Path.Combine(desktopData,"wallpaper-assets"))+Path.DirectorySeparatorChar;
                if(!Path.GetFullPath(next).StartsWith(root,StringComparison.OrdinalIgnoreCase)||(File.GetAttributes(next)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Görsel listesi varlığı geçersiz.");
                wallpaperSession.Apply(item.MonitorId,next);rotationCurrent[item.MonitorId]=next;rotationDue[item.MonitorId]=now.AddSeconds(item.IntervalSeconds);
            }
        }
        catch(Exception ex){foreach(var r in committed.WallpaperRotations)rotationStopped.Add(r.MonitorId);Log.Write("wallpaper-rotation-stopped",ex);ViewModel.Status="Görsel geçişleri hata nedeniyle durdu; kontrol merkezinden listeyi yeniden seçebilirsin.";}
        finally{persistenceGate.Release();rotating=false;}
    }
    public async Task<bool> ConfigureWindowsTaskbarAsync(bool hide)
    {
        if(safe||ReadOnly)return false;await persistenceGate.WaitAsync();
        try
        {
            if(hide)await taskbarSession.EnableAsync(EnsureTaskbarGuardianAsync);else await taskbarSession.RestoreAsync(taskbarOwner);
            var next=committed with{HideWindowsTaskbar=hide};await store.SaveAsync(next);committed=next;ViewModel.SetDesktopSettings(next);RefreshMonitors();
            ViewModel.Status=hide?"Windows görev çubuğu otomatik gizleniyor. Çıkışta önceki tercih geri gelecek.":"Windows görev çubuğunun önceki tercihi geri getirildi.";return true;
        }
        catch(Exception ex)
        {
            try{if(committed.HideWindowsTaskbar)await taskbarSession.EnableAsync(EnsureTaskbarGuardianAsync);else await taskbarSession.RestoreAsync(taskbarOwner);}catch(Exception recovery){Log.Write("taskbar-rollback-pending",recovery);}
            Log.Write("taskbar-change-failed",ex);ViewModel.Status=ex is InvalidOperationException?ex.Message:"Görev çubuğu değiştirilemedi; kurtarma kaydı korunuyor.";return false;
        }
        finally{persistenceGate.Release();}
    }
}
