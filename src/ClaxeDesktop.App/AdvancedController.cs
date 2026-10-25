using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;
public sealed partial class DesktopController
{
    private ProfileStore profileStore=null!;
    private ProfileBook profileBook=new();
    private readonly SemaphoreSlim profileEdits=new(1,1);
    private Task advancedLoad=Task.CompletedTask;
    private bool advancedReady,automationBusy;
    private AutomationState automation=new();
    private AdvancedWindow? advancedWindow;
    private LayoutShortcutBinding? layoutShortcut;
    private DockShortcutBinding? dockShortcut;
    public void SummonDock() { if (!exiting) windows.OfType<DockWindow>().Single().ToggleSummon(); }
    private MascotWindow? mascot;
    private readonly WindowsLayoutService layoutService=new();
    private List<WindowsLayoutService.Snapshot> lastLayout=[];
    private IReadOnlyList<AppWindow> lastWindowSnapshot=[];
    internal IReadOnlyList<AppWindow> LastSnapshot => lastWindowSnapshot;
    public event Action? AdvancedChanged;
    internal ProfileBook Profiles=>profileBook;
    internal Task WaitAdvancedAsync()=>advancedLoad;
    internal bool AdvancedReadOnly=>ReadOnly||safe||profileStore.ReadOnly;
    private void StartAdvancedServices()
    {
        profileStore=new(desktopData);automation=new(ManualUntil:committed.ProfileManualUntil);
        layoutShortcut=new(windows.OfType<PanelWindow>().Single(),()=>ShowAdvanced(2));
        dockShortcut = new(windows.OfType<PanelWindow>().Single(), SummonDock);
        islandShortcut = new(windows.OfType<PanelWindow>().Single(), 755, ToggleIslandSummon, "Klavye ve kısayollar penceresinden başka bir kısayol seçebilirsin.");
        ApplyHotkeys();
        advancedLoad=LoadAdvancedAsync();UpdateMascot();
    }
    private async Task LoadAdvancedAsync()
    {
        try
        {
            profileBook=await profileStore.LoadAsync();
            if(profileBook.Profiles.Count==0&&!AdvancedReadOnly)
            {
                var main=ViewModel.Monitors.First(m=>m.Primary);
                var focus=new Settings{ThemeId="tempest",Material="flat",IconSize=40,DockEdge="bottom",PanelWidth=1280,Widgets=[new(){Kind="clock",Name="Saat",Height=190,Placement=new(main.Id,30,100)},new(){Kind="note",Name="Odak notu",Height=220,Text="Bugün tek bir işe odaklan.",Placement=new(main.Id,30,320)}]};
                var free=new Settings{ThemeId="sakura",IconSize=56,PanelWidth=1100,Widgets=[new(){Kind="calendar",Name="Takvim",Placement=new(main.Id,Math.Max(24,main.Width/main.Scale-340),96)},new(){Kind="clock",Name="Saat",Height=190,Placement=new(main.Id,40,96)}]};
                profileBook=profileBook with{Profiles=[DesktopProfile.Capture("Midnight · Çalışma",new(),ViewModel.Monitors),DesktopProfile.Capture("Tempest · Odak",focus,ViewModel.Monitors),DesktopProfile.Capture("Sakura · Serbest",free,ViewModel.Monitors)]};await profileStore.SaveAsync(profileBook);
            }
            if(profileStore.ReadOnly)ViewModel.Status="Profil kitabı yeni sürüme ait; profil kayıtları salt okunur.";else if(profileStore.Notice is{} notice)ViewModel.Status=notice;
            advancedReady=true;AdvancedChanged?.Invoke();
        }
        catch(Exception ex){Log.Write("profile-load-failed",ex);ViewModel.Status="Profil kitabı yüklenemedi. Mevcut masaüstü korunuyor.";}
    }
    public async void ShowAdvanced(int tab=0)
    {
        await advancedLoad;if(exiting)return;
        if(advancedWindow is null){advancedWindow=new(this);advancedWindow.Closed+=(_,_)=>advancedWindow=null;}
        advancedWindow.Show();advancedWindow.SelectTab(tab);advancedWindow.Activate();
    }
    internal async Task SaveProfileAsync(string name)
    {
        if(AdvancedReadOnly)throw new IOException("Profil kayıtları bu modda kapalı.");if(Widgets.Editing)throw new InvalidOperationException("Önce widget taslağını kaydet veya iptal et.");
        var profile=DesktopProfile.Capture(name,ViewModel.Settings,ViewModel.Monitors);profile.Validate();await SaveBookAsync(profileBook with{Profiles=[..profileBook.Profiles,profile]});ViewModel.Status="Görünüm ve yerel widget düzeni profil olarak kaydedildi.";
    }
    internal async Task SaveBookAsync(ProfileBook next)
    {
        if(AdvancedReadOnly)throw new IOException("Profil kitabı salt okunur.");await profileEdits.WaitAsync();try{await profileStore.SaveAsync(next);profileBook=next;AdvancedChanged?.Invoke();}finally{profileEdits.Release();}
    }
    internal async Task<bool> ApplyProfileAsync(string id,bool manual=true)
    {
        if(AdvancedReadOnly||Widgets.Editing||settingsWindow is not null){ViewModel.Status="Önce ayar/widget düzenleme penceresini kapat. Güvenli modda profil uygulanmaz.";return false;}
        var profile=profileBook.Profiles.SingleOrDefault(p=>p.Id==id);if(profile is null)return false;
        await persistenceGate.WaitAsync();var old=committed;
        try
        {
            var next=profile.Apply(committed,ViewModel.Monitors) with{ActiveProfileId=id,ProfileManualUntil=manual?DateTimeOffset.UtcNow.AddMinutes(30):committed.ProfileManualUntil};
            await store.SaveAsync(next);ViewModel.Replace(next);Widgets.LoadSaved(next.Widgets);ApplyAppearance();committed=next;
            if(manual)automation=automation with{ManualUntil=next.ProfileManualUntil,Candidate=null};
            ViewModel.Status=profile.Name+(manual?" uygulandı · 30 dakika manuel öncelik.":" otomatik uygulandı.");AdvancedChanged?.Invoke();return true;
        }
        catch(Exception ex)
        {
            try{await store.SaveAsync(old);ViewModel.Replace(old);Widgets.LoadSaved(old.Widgets);ApplyAppearance();}catch(Exception recovery){Log.Write("profile-rollback-failed",recovery);}
            Log.Write("profile-apply-failed",ex);ViewModel.Status="Profil uygulanamadı; önceki düzen geri yüklenmeye çalışıldı.";return false;
        }
        finally{persistenceGate.Release();}
    }
    internal async Task ResumeAutomationAsync()
    {
        if(AdvancedReadOnly)return;await persistenceGate.WaitAsync();try{var next=committed with{ProfileManualUntil=default};await store.SaveAsync(next);committed=next;ViewModel.SetAdvanced(next);automation=automation with{ManualUntil=default,Candidate=null};ViewModel.Status="Manuel bekletme kaldırıldı. Açık kurallar yeniden değerlendirilecek.";}finally{persistenceGate.Release();}
    }
    private async Task AdvanceProfilesAsync()
    {
        if(!advancedReady||automationBusy||exiting||safe||locked||suspended||!profileBook.AutomationEnabled||advancedWindow is not null||controlWindow is not null||settingsWindow is not null||Widgets.Editing)return;
        var foreground=lastWindowSnapshot.FirstOrDefault(w=>w.Handle==windowService.Foreground);
        var context=new AutomationContext(DateTimeOffset.UtcNow,DateTime.Now,foreground?.Executable is{} path?Path.GetFileName(path):null,ViewModel.Monitors.Count,committed.ActiveProfileId);
        var decision=ProfileAutomation.Evaluate(profileBook,context,automation);automation=decision.State;if(decision.ProfileId is null)return;
        automationBusy=true;try{await ApplyProfileAsync(decision.ProfileId,false);}finally{automationBusy=false;}
    }
    internal Task<IReadOnlyList<AppWindow>> LayoutWindowsAsync()=>Task.Run(windowService.Snapshot,lifetime.Token);
    internal async Task ArrangeWindowsAsync(IReadOnlyList<AppWindow> chosen,MonitorInfo monitor,string template)
    {
        if(safe||ReadOnly)throw new InvalidOperationException("Güvenli veya salt okunur modda pencere yerleşimi kapalı.");
        var rectangles=WindowTiling.Resolve(monitor,template,chosen.Count);var snapshots=chosen.Select(layoutService.Capture).ToList();
        try{for(int i=0;i<snapshots.Count;i++)await layoutService.MoveAsync(snapshots[i],rectangles[i]);lastLayout=snapshots;ViewModel.Status="Seçilen pencereler yerleştirildi. Önceki konumlara geri dönebilirsin.";}
        catch(Exception ex){var failed=snapshots.Count(item=>!layoutService.Restore(item));Log.Write("window-layout-failed",ex);throw new InvalidOperationException(failed==0?"Yerleşim uygulanamadı; önceki konumlar geri yüklendi. "+ex.Message:"Yerleşim uygulanamadı; bazı pencereler kapalı veya erişilemediği için geri alınamadı.",ex);}
    }
    internal void RestoreWindowLayout(){var failed=lastLayout.Count(w=>!layoutService.Restore(w));lastLayout=[];ViewModel.Status=failed==0?"Pencerelerin önceki konumlarına dönüldü.":"Bazı pencereler kapalı veya erişilemiyor; kalanlar geri alındı.";}
    internal async Task ConfigureLayoutShortcutAsync(bool enabled)
    {
        if(safe||ReadOnly)return;await persistenceGate.WaitAsync();
        try{layoutShortcut!.Configure(enabled,Gesture("layout"));var next=committed with{LayoutShortcut=enabled};await store.SaveAsync(next);committed=next;ViewModel.SetAdvanced(next);}
        catch{layoutShortcut!.Configure(committed.LayoutShortcut,Gesture("layout"));throw;}finally{persistenceGate.Release();}
    }
    private string ImportMascot(string file)
    {
        if(!Path.IsPathFullyQualified(file)||file.StartsWith(@"\\")||new DriveInfo(Path.GetPathRoot(file)!).DriveType==DriveType.Network||new FileInfo(file).Length>8*1024*1024)throw new InvalidDataException("En fazla 8 MB yerel görsel seç.");
        var root=Path.Combine(desktopData,"mascot-assets");Directory.CreateDirectory(root);var target=Path.Combine(root,Guid.NewGuid().ToString("N")+".png");
        using var input=File.OpenRead(file);var bitmap=BitmapFrame.Create(input,BitmapCreateOptions.DelayCreation,BitmapCacheOption.OnDemand);if((long)bitmap.PixelWidth*bitmap.PixelHeight>4000000)throw new InvalidDataException("Maskot en fazla 4 megapiksel olmalı.");var encoder=new PngBitmapEncoder();encoder.Frames.Add(bitmap);using var output=File.Create(target);encoder.Save(output);return target;
    }
    internal async Task ConfigureMascotAsync(bool enabled,string? imageFile=null,string? happyFile=null,int? size=null)
    {
        if(safe||ReadOnly)return;await persistenceGate.WaitAsync();var old=committed;
        try
        {
            var options=committed.Mascot with{Enabled=enabled,Size=size??committed.Mascot.Size};
            if(imageFile is not null)options=options with{ImagePath=await Task.Run(()=>ImportMascot(imageFile),lifetime.Token)};
            if(happyFile is not null)options=options with{HappyImagePath=await Task.Run(()=>ImportMascot(happyFile),lifetime.Token)};
            options.Validate();var next=committed with{Mascot=options};if(enabled)ValidateMascotPaths(options);await store.SaveAsync(next);committed=next;ViewModel.SetAdvanced(next);UpdateMascot();if(enabled&&mascot is null)throw new IOException("Maskot gösterilemedi.");ViewModel.Status=enabled?"Yerel maskot açıldı; tam ekranda gizlenir.":"Maskot kapandı.";AdvancedChanged?.Invoke();
        }
        catch{try{await store.SaveAsync(old);}catch(Exception ex){Log.Write("mascot-rollback-failed",ex);}committed=old;ViewModel.SetAdvanced(old);UpdateMascot();throw;}finally{persistenceGate.Release();}
    }
    private void ValidateMascotPaths(MascotOptions value)
    {
        var root=Path.GetFullPath(Path.Combine(desktopData,"mascot-assets"))+Path.DirectorySeparatorChar;
        foreach(var file in new[]{value.ImagePath,value.HappyImagePath})if(file is not null&&(!Path.GetFullPath(file).StartsWith(root,StringComparison.OrdinalIgnoreCase)||!File.Exists(file)||(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0))throw new InvalidDataException("Maskot varlığı bulunamadı veya geçersiz.");
    }
    private void UpdateMascot()
    {
        if(safe||!ViewModel.Settings.Mascot.Enabled){if(mascot is not null){RemoveWidgetWindow(mascot);mascot=null;}return;}
        try
        {
            ValidateMascotPaths(ViewModel.Settings.Mascot);
            if(mascot is null){mascot=new(()=>_=HideMascotAsync());AddWidgetWindow(mascot);}
            mascot.Load(ViewModel.Settings.Mascot);mascot.Show();mascot.Place(ViewModel.Settings.Mascot.Placement,ViewModel.Monitors,ViewModel.SnapToGrid);
        }
        catch(Exception ex){Log.Write("mascot-load-failed",ex);ViewModel.Status="Maskot görseli açılamadı. Masaüstü kullanılabilir; görseli yeniden seç.";if(mascot is not null){RemoveWidgetWindow(mascot);mascot=null;}}
    }
    internal void MascotActivity()
    {
        if(mascot is null)return;var monitor=ViewModel.Monitors.FirstOrDefault(m=>m.Id==ViewModel.Settings.Mascot.Placement?.MonitorId)??ViewModel.Monitors.First(m=>m.Primary);
        var visible=!locked&&!suspended&&!windowService.IsFullscreenOn(monitor);mascot.Activity(visible,ViewModel.Settings.AnimationsEnabled&&SystemParameters.ClientAreaAnimation&&!SystemParameters.HighContrast&&WindowsPowerState.ConservePower()==false);
    }
    private async Task HideMascotAsync(){try{await ConfigureMascotAsync(false);}catch(Exception ex){Log.Write("mascot-disable-failed",ex);ViewModel.Status="Maskot tercihi kaydedilemedi; yeniden deneyebilirsin.";}}
    private async Task SaveMascotPlacementAsync(MascotWindow window)
    {
        if(safe||ReadOnly)return;var placement=window.Capture(ViewModel.Monitors);await persistenceGate.WaitAsync();try{var next=committed with{Mascot=committed.Mascot with{Placement=placement}};await store.SaveAsync(next);committed=next;ViewModel.SetAdvanced(next);}catch(Exception ex){Log.Write("mascot-placement-failed",ex);ViewModel.Status="Maskot konumu kaydedilemedi.";}finally{persistenceGate.Release();}
    }
    private void StopAdvancedServices(){StopKeyboard();islandShortcut?.Dispose();islandCard?.Close();dockShortcut?.Dispose();layoutShortcut?.Dispose();advancedWindow?.Close();}
}

