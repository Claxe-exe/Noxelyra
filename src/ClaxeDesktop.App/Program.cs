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
        // The taskbar guardian lives as long as Noxelyra. It needs no UI, so it runs here without WPF and costs a few MB instead of a full app.
        if (args.Length == 3 && (args[0] == "--taskbar-guardian" || (args[0] == "--taskbar-guardian-fixture" && Environment.GetEnvironmentVariable("CLAXE_TEST_FIXTURE") == "1"))
            && int.TryParse(args[1], out var parentId) && long.TryParse(args[2], out var parentTicks))
        {
            var data = Environment.GetEnvironmentVariable("CLAXE_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaxeDesktop");
            TaskbarGuardian.RunAsync(data, new(parentId, parentTicks), args[0] == "--taskbar-guardian-fixture" ? new TaskbarFileFixture(data) : null).GetAwaiter().GetResult();
            return;
        }
        // Diagnostic: run the island's video search on one window and write the trace to %TEMP%. No window, no tray, no network.
        if (args.Length == 2 && args[0] == "--probe-video" && long.TryParse(args[1], out var handle))
        {
            var lines = new List<string>();
            var found = VideoRegion.FindAsync((nint)handle, CancellationToken.None, lines.Add).GetAwaiter().GetResult();
            lines.Add(found is { } f ? $"found {f.Left},{f.Top}-{f.Right},{f.Bottom}" : "none");
            File.WriteAllLines(Path.Combine(Path.GetTempPath(), "noxelyra-video-probe.txt"), lines);
            return;
        }
        var app = new App(); app.InitializeComponent(); app.Run();
    }
}
