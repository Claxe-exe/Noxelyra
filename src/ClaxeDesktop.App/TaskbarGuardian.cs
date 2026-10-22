using System.Diagnostics;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;
internal static class TaskbarGuardian
{
    internal static TaskbarOwner CurrentOwner(){using var process=Process.GetCurrentProcess();return new(process.Id,process.StartTime.ToUniversalTime().Ticks);}
    internal static string ReadyPath(string directory,TaskbarOwner owner)=>Path.Combine(directory,$"taskbar-guardian-{owner.ProcessId}-{owner.StartedUtcTicks}.ready");
    internal static async Task RunAsync(string directory,TaskbarOwner owner,ITaskbarService? platform=null)
    {
        var ready=ReadyPath(directory,owner);var log=new LocalLog(Path.Combine(directory,"logs"));
        try
        {
            using var parent=Process.GetProcessById(owner.ProcessId);
            if(parent.Id==Environment.ProcessId)throw new InvalidOperationException();
            if(parent.StartTime.ToUniversalTime().Ticks==owner.StartedUtcTicks){File.WriteAllText(ready,"ready");await parent.WaitForExitAsync();}
        }
        catch(ArgumentException){} // Parent may have already exited.
        catch(Exception ex){log.Write("taskbar-guardian-wait-failed",ex);return;}
        try{await new TaskbarSession(platform??new WindowsTaskbarService(),Path.Combine(directory,"taskbar-recovery.json"),owner).RestoreAsync(owner);log.Write("taskbar-guardian-finished");}
        catch(Exception ex){log.Write("taskbar-guardian-restore-pending",ex);}
        finally
        {
            if (platform is null)
            {
                try { TaskbarWindows.SetVisible(true); } catch (Exception ex) { log.Write("taskbar-guardian-show-failed", ex); }
                try { await new ExternalWallpaperSession(new WindowsExternalWallpaperService(), Path.Combine(directory, "engine-recovery.json")).RestoreAsync(expectedOwner: owner); log.Write("engine-guardian-finished"); }
                catch (Exception ex) { log.Write("engine-guardian-restore-pending", ex); }
            }
            if(File.Exists(ready))File.Delete(ready);
        }
    }
}
