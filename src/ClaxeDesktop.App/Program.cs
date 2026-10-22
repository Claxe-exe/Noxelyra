using Velopack;
namespace ClaxeDesktop.App;

/// <summary>Custom entry point so Setup.exe / Update.exe hooks run before any WPF window or mutex exists.</summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ =>
            {
                // Leave Windows as it was: close a running Noxelyra, show the taskbar, restore wallpaper and WE, drop our startup link.
                try { Task.Run(() => Maintenance.RunAsync(quiet: true)).Wait(TimeSpan.FromSeconds(20)); } catch (AggregateException) { }
                try { new StartupService().SetEnabled(false); } catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Runtime.InteropServices.COMException) { }
            })
            .Run();
        var app = new App(); app.InitializeComponent(); app.Run();
    }
}
