using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;
internal static class AdvancedVerifier
{
    internal static async Task CaptureAsync(DesktopController controller,string directory)
    {
        await controller.WaitAdvancedAsync();controller.ShowAdvanced();await Task.Delay(250);var window=Application.Current.Windows.OfType<AdvancedWindow>().Single();var root=Path.Combine(directory,"preview");Directory.CreateDirectory(root);for(int i=0;i<4;i++){window.SelectTab(i);window.UpdateLayout();Export(window,Path.Combine(root,"Advanced-"+i+".png"));}controller.Exit();
    }
    public static async Task RunAsync(DesktopController controller,ISettingsStore store,string directory,VerificationWindowService signals)
    {
        var checks=new List<object>();var root=Path.Combine(directory,"advanced-verification");Directory.CreateDirectory(root);Process? probe=null;
        void Check(string name,bool ok){checks.Add(new{name,passed=ok});if(!ok)throw new InvalidOperationException(name);}
        bool Reject(string text){try{ProfileFiles.Import(text);return false;}catch(InvalidDataException){return true;}}
        try
        {
            await controller.WaitAdvancedAsync();var screens=controller.ViewModel.Monitors;var screen=screens.First(m=>m.Primary);
            var web=new WidgetInstance{Kind="web",WebPackageId=Guid.NewGuid().ToString("N")};
            var source=new Settings{ThemeId="sakura",HideWindowsTaskbar=true,Shortcuts=[new("Private",@"C:\secret\app.exe")],Widgets=[WidgetInstance.Default with{Placement=new(screen.Id,120,96)},web]};
            var profile=DesktopProfile.Capture("Portable",source,screens);var json=ProfileFiles.Export(profile);var imported=ProfileFiles.Import(json);var merged=imported.Apply(new(){HideWindowsTaskbar=true,Widgets=[web]},screens);
            Check("Portable profile merge preserves risk preferences and web instances",!json.Contains("secret")&&!json.Contains("WebPackageId\": \"")&&merged.HideWindowsTaskbar&&merged.Widgets.Any(w=>w.Id==web.Id)&&merged.ThemeId=="sakura");
            Check("Profile import rejects unknown duplicate and future fields",Reject("{\"Danger\":true}")&&Reject("{\"Name\":\"A\",\"Name\":\"B\"}")&&Reject("{\"Version\":9}"));
            var second=profile with{Id=Guid.NewGuid().ToString("N"),Name="Second"};var a=new ProfileRule{ProfileId=profile.Id,Enabled=true,MonitorCount=2,Priority=10};var b=a with{Id=Guid.NewGuid().ToString("N"),ProfileId=second.Id,Priority=50};var book=new ProfileBook{AutomationEnabled=true,Profiles=[profile,second],Rules=[a,b]};var now=DateTimeOffset.UtcNow;var context=new AutomationContext(now,new DateTime(2026,10,22,12,0,0),null,2,null);
            var first=ProfileAutomation.Evaluate(book,context,new());var chosen=ProfileAutomation.Evaluate(book,context with{Now=now.AddSeconds(4)},first.State);var tied=ProfileAutomation.Evaluate(book with{Rules=[a with{Priority=50},b]},context,new());
            Check("Profile priority and stable tie ordering choose one winner",chosen.ProfileId==second.Id&&tied.State.Candidate==a.Id);
            var waiting=ProfileAutomation.Evaluate(book,context with{Now=now.AddSeconds(1)},first.State);var cooling=ProfileAutomation.Evaluate(book,context with{Now=now.AddSeconds(4)},first.State with{LastSwitch=now});var unchanged=ProfileAutomation.Evaluate(book,context with{Now=now.AddSeconds(40),ActiveProfileId=second.Id},chosen.State);
            Check("Rule dwell cooldown and active profile prevent thrashing",waiting.ProfileId is null&&cooling.ProfileId is null&&unchanged.ProfileId is null);
            var manualStore=new SettingsStore(Path.Combine(root,"manual"));await manualStore.SaveAsync(new(){ProfileManualUntil=now.AddMinutes(30)});var manual=(await manualStore.LoadAsync()).Settings.ProfileManualUntil;var held=ProfileAutomation.Evaluate(book,context,new(ManualUntil:manual));
            Check("Manual override survives settings reload",held.ProfileId is null&&manual>now.AddMinutes(29));
            var night=new ProfileRule{ProfileId=profile.Id,Enabled=true,Application="game.exe",StartMinute=23*60,EndMinute=6*60,MonitorCount=2};
            Check("Midnight application and topology rule conditions combine",night.Matches(context with{LocalTime=new(2026,10,22,23,30,0),ForegroundApplication="GAME.EXE"})&&!night.Matches(context with{LocalTime=new(2026,10,22,12,0,0),ForegroundApplication="game.exe"})&&!night.Matches(context with{LocalTime=new(2026,10,22,23,30,0),ForegroundApplication="game.exe",MonitorCount=1}));
            var books=new ProfileStore(Path.Combine(root,"books"));await books.SaveAsync(book);await books.SaveAsync(book with{Profiles=[profile],Rules=[a]});var bookPath=Path.Combine(root,"books","profiles.json");await File.WriteAllTextAsync(bookPath,"broken");var recovered=await books.LoadAsync();await books.SaveAsync(recovered);await File.WriteAllTextAsync(bookPath,"{\"Version\":99}");var future=new ProfileStore(Path.Combine(root,"books"));await future.LoadAsync();
            Check("Profile book atomic backup and future read-only protection",recovered.Profiles.Count==2&&future.ReadOnly);
            var selected=controller.Profiles.Profiles.Last();var applied=await controller.ApplyProfileAsync(selected.Id);var loaded=(await store.LoadAsync()).Settings;
            Check("Real profile apply persists and updates native widget workspace",applied&&loaded.ActiveProfileId==selected.Id&&controller.ViewModel.Settings.ThemeId==selected.Appearance.ThemeId&&controller.Windows.OfType<WidgetWindow>().Count()==loaded.Widgets.Count);
            var negative=new MonitorInfo("negative","Negative",-1920,40,1920,1000,1.5,false);var cells=WindowTiling.Resolve(negative,"grid",4);
            Check("DPI scaled tiling stays inside negative-origin work area",cells.All(r=>r.X>=negative.X&&r.Y>=negative.Y&&r.X+r.Width<=negative.X+negative.Width&&r.Y+r.Height<=negative.Y+negative.Height)&&cells[0].X+cells[0].Width<cells[1].X);
            var ready=Path.Combine(root,"probe-ready.txt");var info=new ProcessStartInfo(Environment.GetEnvironmentVariable("CLAXE_WINDOW_PROBE_EXE")??throw new InvalidOperationException("Window probe missing")){UseShellExecute=false};info.Environment["CLAXE_WINDOW_PROBE_READY"]=ready;probe=Process.Start(info)!;var service=new WindowsWindowService();AppWindow[] pair=[];
            for(int i=0;i<50;i++){pair=service.Snapshot().Where(w=>w.ProcessId==probe.Id&&!w.Minimized).Take(2).ToArray();if(pair.Length==2&&File.Exists(ready))break;await Task.Delay(100);}
            var layout=new WindowsLayoutService();var originals=pair.Select(layout.Bounds).ToArray();await controller.ArrangeWindowsAsync(pair,screen,"halves");var targets=WindowTiling.Resolve(screen,"halves",2);var actual=pair.Select(layout.Bounds).ToArray();controller.RestoreWindowLayout();await Task.Delay(250);var restored=pair.Select(layout.Bounds).ToArray();
            Check("Two real external HWNDs tile and restore original bounds",pair.Length==2&&actual.Select((r,i)=>Math.Abs(r.X-targets[i].X)<=8&&Math.Abs(r.Width-targets[i].Width)<=8).All(x=>x)&&restored.Select((r,i)=>Math.Abs(r.X-originals[i].X)<=8&&Math.Abs(r.Y-originals[i].Y)<=8&&Math.Abs(r.Width-originals[i].Width)<=8).All(x=>x));
            var image=Path.Combine(root,"fixture.png");var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawEllipse(Brushes.MediumPurple,null,new(32,32),26,26);}var bitmap=new RenderTargetBitmap(64,64,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(image))encoder.Save(file);
            await controller.ConfigureMascotAsync(true,imageFile:image);controller.MascotActivity();var mascot=controller.Windows.OfType<MascotWindow>().Single();var visible=mascot.IsVisible;signals.Fullscreen=true;controller.MascotActivity();var hidden=!mascot.IsVisible;signals.Fullscreen=false;controller.MascotActivity();var returned=mascot.IsVisible;await controller.ConfigureMascotAsync(false);
            Check("Local mascot renders hides on simulated fullscreen and disables",visible&&hidden&&returned&&!controller.Windows.OfType<MascotWindow>().Any()&&!(await store.LoadAsync()).Settings.Mascot.Enabled);
            var registered=false;var conflict=false;try{await controller.ConfigureLayoutShortcutAsync(true);registered=true;}catch(InvalidOperationException ex){conflict=ex.Message.Contains("başka");}await controller.ConfigureLayoutShortcutAsync(false);
            Check("Optional layout hotkey registers or reports conflict and releases",(registered||conflict)&&!controller.ViewModel.Settings.LayoutShortcut&&!new Settings().LayoutShortcut&&!new Settings().Mascot.Enabled);
            controller.ShowAdvanced();await Task.Delay(200);var window=Application.Current.Windows.OfType<AdvancedWindow>().Single();for(int i=0;i<4;i++){window.SelectTab(i);window.UpdateLayout();Export(window,Path.Combine(root,"Advanced-"+i+".png"));}
            await File.WriteAllTextAsync(Path.Combine(root,"results.json"),JsonSerializer.Serialize(new{success=true,checks},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception ex){await File.WriteAllTextAsync(Path.Combine(root,"results.json"),JsonSerializer.Serialize(new{success=false,checks,error=ex.Message,status=controller.ViewModel.Status}));}
        finally{if(probe is not null){if(!probe.HasExited)probe.Kill();probe.Dispose();}controller.Exit();}
    }
    private static void Export(Window window,string path){var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);png.Save(file);}
}
