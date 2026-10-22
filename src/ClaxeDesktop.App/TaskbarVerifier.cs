using System.Diagnostics;
using System.Text.Json;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;
internal sealed class TaskbarFileFixture(string directory):ITaskbarService
{
    private string Pathname=>Path.Combine(directory,"fixture-state.txt");
    public bool AutoHide=>bool.Parse(File.ReadAllText(Pathname));
    public void SetAutoHide(bool value)=>File.WriteAllText(Pathname,value.ToString());
}
internal static class TaskbarVerifier
{
    public static async Task CrashFixtureAsync(string directory)
    {
        Directory.CreateDirectory(directory);var platform=new TaskbarFileFixture(directory);platform.SetAutoHide(false);var owner=TaskbarGuardian.CurrentOwner();
        var session=new TaskbarSession(platform,Path.Combine(directory,"taskbar-recovery.json"),owner);
        await session.EnableAsync(async()=>
        {
            var info=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};info.Environment["CLAXE_DATA_DIR"]=directory;
            info.ArgumentList.Add("--taskbar-guardian-fixture");info.ArgumentList.Add(owner.ProcessId.ToString());info.ArgumentList.Add(owner.StartedUtcTicks.ToString());using var process=Process.Start(info)!;
            for(int i=0;i<60;i++){if(File.Exists(TaskbarGuardian.ReadyPath(directory,owner)))return;if(process.HasExited)break;await Task.Delay(50);}throw new InvalidOperationException("Fixture helper failed");
        });
        File.WriteAllText(Path.Combine(directory,"applied.txt"),"applied");Environment.Exit(0); // Only this isolated fixture process exits without app cleanup.
    }
    public static async Task RunAsync(DesktopController controller,string directory)
    {
        var checks=new List<object>();void Check(string name,bool value){checks.Add(new{name,passed=value});if(!value)throw new InvalidOperationException(name);}
        var root=Path.Combine(directory,"taskbar-verification");Directory.CreateDirectory(root);var native=new WindowsTaskbarService();bool? original=null;
        try
        {
            original=native.AutoHide;var owner=new TaskbarOwner(7,9);var other=new TaskbarOwner(8,10);string Journal(string name)=>Path.Combine(root,name+".json");
            Check("Risky taskbar management defaults off",!new Settings().HideWindowsTaskbar);
            var fake=new Fixture();var path=Journal("durable");var session=new TaskbarSession(fake,path,owner);var guarded=false;
            await session.EnableAsync(()=>{guarded=File.Exists(path)&&!fake.AutoHide;return Task.CompletedTask;});
            Check("Durable journal and guardian precede mutation",guarded&&fake.AutoHide);
            await new TaskbarSession(fake,path,owner).RestoreAsync();Check("New session restores original taskbar preference",!fake.AutoHide&&!File.Exists(path));
            fake=new(){Value=true};path=Journal("existing");guarded=false;await new TaskbarSession(fake,path,owner).EnableAsync(()=>{guarded=true;return Task.CompletedTask;});
            Check("Existing user autohide preference is not owned",fake.AutoHide&&!guarded&&!File.Exists(path));
            fake=new();path=Journal("external");session=new(fake,path,owner);await session.EnableAsync(()=>Task.CompletedTask);fake.Value=false;await session.RestoreAsync();
            Check("External user change is preserved",!fake.AutoHide&&!File.Exists(path));
            fake=new();path=Journal("owner");session=new(fake,path,owner);await session.EnableAsync(()=>Task.CompletedTask);await session.RestoreAsync(other);
            Check("Old guardian cannot restore a new owner lease",fake.AutoHide&&File.Exists(path));await session.RestoreAsync(owner);
            fake=new(){Ignore=true};path=Journal("ignored");session=new(fake,path,owner);var rejected=false;try{await session.EnableAsync(()=>Task.CompletedTask);}catch(InvalidOperationException){rejected=true;}await session.RestoreAsync();
            Check("Ignored Windows changes fail verification safely",rejected&&!File.Exists(path)&&!fake.AutoHide);
            Check("Wallpaper pause follows per-monitor fullscreen",WallpaperPausePolicy.Pause(false,false,false,true)&&!WallpaperPausePolicy.Pause(false,false,false,false));
            Check("Wallpaper pauses on lock power risk and unknown power",WallpaperPausePolicy.Pause(false,true,false,false)&&WallpaperPausePolicy.Pause(false,false,true,false)&&WallpaperPausePolicy.Pause(false,false,null,false));
            var childData=Path.Combine(root,"guardian-child");Directory.CreateDirectory(childData);var childInfo=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};childInfo.Environment["CLAXE_DATA_DIR"]=childData;childInfo.Environment["CLAXE_TEST_FIXTURE"]="1";childInfo.ArgumentList.Add("--verify-taskbar-crash");
            using(var child=Process.Start(childInfo)!){await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));}
            for(int i=0;i<60&&File.Exists(Path.Combine(childData,"taskbar-recovery.json"));i++)await Task.Delay(100);
            Check("Guardian recovers fixture after parent exits without cleanup",File.Exists(Path.Combine(childData,"applied.txt"))&&!File.Exists(Path.Combine(childData,"taskbar-recovery.json"))&&!new TaskbarFileFixture(childData).AutoHide);
            var enabled=await controller.ConfigureWindowsTaskbarAsync(true);
            Check("Real Windows taskbar accepts and reports autohide",enabled&&native.AutoHide);
            await controller.ConfigureWindowsTaskbarAsync(false);
            Check("Real Windows taskbar original preference is restored",native.AutoHide==original.Value);
            await File.WriteAllTextAsync(Path.Combine(root,"results.json"),JsonSerializer.Serialize(new{success=true,checks,originalAutoHide=original},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception ex){await File.WriteAllTextAsync(Path.Combine(root,"results.json"),JsonSerializer.Serialize(new{success=false,checks,error=ex.Message,status=controller.ViewModel.Status}));}
        finally{await controller.ConfigureWindowsTaskbarAsync(false);if(original is{} baseline&&native.AutoHide!=baseline)native.SetAutoHide(baseline);controller.Exit();}
    }
    private sealed class Fixture:ITaskbarService{public bool Value,Ignore;public bool AutoHide=>Value;public void SetAutoHide(bool value){if(!Ignore)Value=value;}}
}
