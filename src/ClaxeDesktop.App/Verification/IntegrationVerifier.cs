using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;
internal static class IntegrationVerifier
{
    public static async Task RunAsync(DesktopController controller,ISettingsStore store,string directory)
    {
        if(Environment.GetEnvironmentVariable("CLAXE_DATA_DIR") is null)throw new InvalidOperationException("Isolated directory required.");
        var checks=new List<object>();var output=Path.Combine(directory,"verification");Directory.CreateDirectory(output);
        void Check(string name,bool passed){checks.Add(new{name,passed});if(!passed)throw new InvalidOperationException(name);}
        try
        {
            var fake=new WallpaperFixture();var asset=Path.Combine(directory,"fixture.png");File.WriteAllText(asset,"fixture");var journal=Path.Combine(directory,"wallpaper-fixture.json");
            var owned=new WallpaperSession(fake,journal);owned.Apply("display",asset);new WallpaperSession(fake,journal).Restore();
            Check("Wallpaper recovery restores owned change after new session",fake.Current==fake.Original&&!File.Exists(journal));
            owned=new(fake,journal);owned.Apply("display",asset);fake.Current=Path.Combine(directory,"external.png");new WallpaperSession(fake,journal).Restore();
            Check("Wallpaper recovery preserves an external user change",fake.Current.EndsWith("external.png",StringComparison.Ordinal)&&!File.Exists(journal));
            fake.Current=fake.Original;owned=new(fake,journal);owned.Apply("display",asset);fake.Connected=false;try{new WallpaperSession(fake,journal).Restore();}catch(IOException){}var retained=File.Exists(journal);fake.Connected=true;new WallpaperSession(fake,journal).Restore();
            Check("Disconnected wallpaper lease waits and restores on reconnect",retained&&fake.Current==fake.Original&&!File.Exists(journal));
            AudioState? audio=null;bool unavailable=false;
            try{audio=new WindowsAudioService().Read();}catch(Exception ex)when(ex is COMException or InvalidOperationException){unavailable=true;}
            Check("Core Audio reads native output or reports unavailable",audio is{Level:>=0 and <=1}||unavailable);
            var displays=controller.WallpaperPlatform.Displays();
            var unreadable=displays.Count(d=>{try{return controller.WallpaperPlatform.Read(d.Id) is null;}catch(COMException){return true;}});
            Check($"Windows wallpaper API enumerates and reads real monitors ({displays.Count-unreadable}/{displays.Count} readable; an inactive display entry may refuse)",displays.Count>0&&unreadable<displays.Count);
            var before=new WindowsMonitorService().GetMonitors().First(m=>m.Primary);
            var theme=controller.ViewModel.Settings.PrimaryAccent;controller.ViewModel.Edit(controller.ViewModel.Settings with{PrimaryAccent="#123456"});
            var enabled=await controller.ConfigureDesktopAsync(true,false);await Task.Delay(200);
            var reserved=new WindowsMonitorService().GetMonitors().First(m=>m.Id==before.Id);
            Check("Native AppBar reserves Windows work area",enabled&&controller.HasReservedPanel&&reserved.Y>before.Y&&reserved.Height<before.Height);
            var barTop=WindowPlacement.Position(new System.Windows.Interop.WindowInteropHelper(controller.Windows.OfType<PanelWindow>().Single()).Handle).Y;
            Check("Reserved bar sits in its own strip at the top of the work area (no 48 px drop)",controller.ReservedPanelTop is int top&&barTop==top&&top==before.Y);
            var saved=(await store.LoadAsync()).Settings;
            Check("Desktop integration persists independently from theme preview",saved.IntegratedDesktop&&saved.PrimaryAccent==theme);
            await controller.ConfigureDesktopAsync(false,false);await Task.Delay(200);
            var restored=new WindowsMonitorService().GetMonitors().First(m=>m.Id==before.Id);
            Check("Disabling AppBar restores native work area",!controller.HasReservedPanel&&restored.Y==before.Y&&restored.Height==before.Height);
            controller.RevertPreview();var shortcuts=await controller.ConfigureDesktopAsync(false,true);var conflict=controller.ViewModel.Status;await controller.ConfigureDesktopAsync(false,false);
            Check("Global shortcuts register and release or show explicit conflict",(shortcuts||conflict.Contains("başka",StringComparison.Ordinal))&&!controller.ViewModel.Settings.GlobalShortcuts);
            controller.ShowLauncher();var launcher=Application.Current.Windows.OfType<LauncherWindow>().Single();
            for(int i=0;i<40&&launcher.ApplicationCount==0;i++)await Task.Delay(100);
            var first=launcher.InstalledApplications.First();launcher.Search.Text=first.Name;await Task.Delay(200);
            Check("Launcher searches actual installed Start menu entries",launcher.ApplicationCount>0&&launcher.Results.Items.Cast<ListBoxItem>().Any(i=>i.Tag is Shortcut a&&a.Name==first.Name));
            var probe=Environment.GetEnvironmentVariable("CLAXE_PROBE_EXE")??throw new InvalidOperationException("Launch probe required.");var marker=Path.Combine(directory,"launch.txt");
            Environment.SetEnvironmentVariable("CLAXE_LAUNCH_PROBE",marker);
            try{controller.ViewModel.Add(new("Noxelyra integration launch probe",probe));launcher.Search.Text="Noxelyra integration launch probe";launcher.Render();launcher.RunSelected();for(int i=0;i<30&&!File.Exists(marker);i++)await Task.Delay(100);Check("Launcher selection starts real executable",File.Exists(marker));}
            finally{Environment.SetEnvironmentVariable("CLAXE_LAUNCH_PROBE",null);}
            controller.ViewModel.Remove(controller.ViewModel.Shortcuts.Single(s=>s.Name=="Noxelyra integration launch probe"));
            launcher.Search.Text="";launcher.Show();launcher.Render();await Task.Delay(300);Export(launcher,Path.Combine(output,"Launcher.png"));
            controller.ShowFullControls();var controls=Application.Current.Windows.OfType<ControlCenterWindow>().Single();await controls.RefreshAsync();await Task.Delay(200);
            Check("Control center reflects real audio capability",audio is null?!controls.Volume.IsEnabled:controls.Volume.IsEnabled&&Math.Abs(controls.Volume.Value-new WindowsAudioService().Read().Level*100)<2);
            Export(controls,Path.Combine(output,"Control-Center.png"));
            await File.WriteAllTextAsync(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{success=true,checks,audioSupported=audio is not null,monitors=displays.Count},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception ex){await File.WriteAllTextAsync(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{success=false,checks,error=ex.Message,status=controller.ViewModel.Status}));}
        finally{await controller.ConfigureDesktopAsync(false,false);controller.Exit();}
    }
    private static void Export(Window window,string path){window.UpdateLayout();var image=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(path);encoder.Save(file);}
    private sealed class WallpaperFixture:IWallpaperService
    {
        public string Original{get;}=Path.Combine(Path.GetTempPath(),"original-wallpaper.png");public string Current;public bool Connected=true;
        public WallpaperFixture(){Current=Original;}
        public IReadOnlyList<WallpaperDisplay> Displays()=>Connected?[new("display","Fixture")]:[];public string Read(string monitor)=>Current;public void Set(string monitor,string path)=>Current=path;public bool HasSlideshow()=>false;
    }
}
