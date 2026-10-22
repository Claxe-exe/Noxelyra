using System.Windows;
using System.Windows.Media;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;

namespace ClaxeDesktop.App;
public partial class App : Application
{
    private Mutex? instance;
    private DesktopController? controller;
    private DesktopCommands? commands;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(Dispatcher));
        if (e.Args.Length == 3 && e.Args[0] == "--we-query") { Shutdown(await WallpaperEngineConsole.RunHelperAsync(e.Args[1], e.Args[2])); return; }
        if (e.Args.Contains("--shutdown-for-maintenance")) { Shutdown(await Maintenance.RunAsync()); return; }
        if(e.Args.Length==3&&(e.Args[0]=="--taskbar-guardian"||(e.Args[0]=="--taskbar-guardian-fixture"&&Environment.GetEnvironmentVariable("CLAXE_TEST_FIXTURE")=="1"))&&int.TryParse(e.Args[1],out var parentId)&&long.TryParse(e.Args[2],out var parentTicks))
        {
            var data=Environment.GetEnvironmentVariable("CLAXE_DATA_DIR")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ClaxeDesktop");
            await TaskbarGuardian.RunAsync(data,new(parentId,parentTicks),e.Args[0]=="--taskbar-guardian-fixture"?new TaskbarFileFixture(data):null);Shutdown();return;
        }
        if(e.Args.Length == 3 && e.Args[0] == "--verify-engine-crash" && Environment.GetEnvironmentVariable("CLAXE_TEST_FIXTURE") == "1" && Environment.GetEnvironmentVariable("CLAXE_DATA_DIR") is {} engineCrashData) { Shutdown(await DesktopAcceptanceVerifier.RunCrashFixtureAsync(engineCrashData, e.Args[1], e.Args[2])); return; }
        if(e.Args.Contains("--verify-taskbar-crash")&&Environment.GetEnvironmentVariable("CLAXE_TEST_FIXTURE")=="1"&&Environment.GetEnvironmentVariable("CLAXE_DATA_DIR") is {} crashData){await TaskbarVerifier.CrashFixtureAsync(crashData);Shutdown();return;}
        var verification=(e.Args.Contains("--verify-desktop-acceptance")||e.Args.Contains("--verify-noxelyra")||e.Args.Contains("--verify-release")||e.Args.Contains("--verify-integration")||e.Args.Contains("--verify-taskbar")||e.Args.Contains("--verify-advanced")||e.Args.Contains("--capture-advanced"))&&Environment.GetEnvironmentVariable("CLAXE_DATA_DIR") is not null;
        instance = new Mutex(true, verification?"Local\\ClaxeDesktop.IntegrationVerification":"Local\\ClaxeDesktop.Phase1", out var first);
        if (!first) { if(!await DesktopCommands.SendAsync(e.Args.Contains("--launcher")?"launcher":e.Args.Contains("--dock")?"dock":"controls"))MessageBox.Show("Noxelyra zaten açık. Bildirim alanındaki simgeden kontrol merkezini açabilirsin."); Shutdown(); return; }
        var directory = Environment.GetEnvironmentVariable("CLAXE_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaxeDesktop");
        var log = new LocalLog(Path.Combine(directory, "logs"));
        DispatcherUnhandledException += (_, args) => { log.Write("unhandled-ui-error", args.Exception); /* Fail closed; dirty marker remains for next start. */ };
        try
        {
            if (SystemParameters.HighContrast)
            {
                Resources["Canvas"] = SystemColors.WindowBrush; Resources["Surface"] = SystemColors.WindowBrush; Resources["Card"] = SystemColors.ControlBrush;
                Resources["Text"] = SystemColors.WindowTextBrush; Resources["Muted"] = SystemColors.WindowTextBrush; Resources["Accent"] = SystemColors.HighlightBrush; Resources["Line"] = SystemColors.WindowTextBrush;
            }
            var store = new SettingsStore(directory); var loaded = await store.LoadAsync();
            if (loaded.Settings.Shortcuts.Count == 0 && !loaded.Settings.FirstRunComplete)
            {
                var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
                loaded = loaded with { Settings = loaded.Settings with { Shortcuts = new[] { new Shortcut("Not Defteri", Path.Combine(system, "notepad.exe")), new Shortcut("Hesap Makinesi", Path.Combine(system, "calc.exe")) }.Where(s => File.Exists(s.Target)).ToList() } };
            }
            var recovery = new RecoveryService(directory); var dirty = recovery.Begin();
            if (dirty) loaded = loaded with { Notice = "Önceki oturum beklenmedik kapandı. Güvenli yerleşim kullanılıyor; kayıtlı düzen korundu." };
            var verificationMonitors = e.Args.Contains("--verify") ? new VerificationMonitorService() : null;
            IMonitorService monitorService = verificationMonitors ?? (IMonitorService)new WindowsMonitorService();
            var advancedSignals=verification&&(e.Args.Contains("--verify-noxelyra")||e.Args.Contains("--verify-release")||e.Args.Contains("--verify-advanced")||e.Args.Contains("--capture-advanced"))?new VerificationWindowService():null;
            controller = new(loaded, store, monitorService, new WindowsMetrics(), new WindowsLauncher(), recovery, log, dirty || e.Args.Contains("--safe-mode"),advancedSignals); controller.Start();
            if(!verification&&!e.Args.Contains("--verify"))commands=new(command=>{if(command=="launcher")controller.ShowLauncher();else if(command=="dock")controller.SummonDock();else if(command=="exit")controller.Exit();else controller.ShowControls();});
            if(verification&&e.Args.Contains("--verify-desktop-acceptance"))await DesktopAcceptanceVerifier.RunAsync(controller,directory);
            else if(verification&&e.Args.Contains("--verify-noxelyra"))await NoxelyraVerifier.RunAsync(controller,directory,advancedSignals!);
            else if(verification&&e.Args.Contains("--verify-release"))await ReleaseVerifier.RunAsync(controller,directory);
            else if(verification&&e.Args.Contains("--capture-advanced"))await AdvancedVerifier.CaptureAsync(controller,directory);
            else if (advancedSignals is not null) await AdvancedVerifier.RunAsync(controller,store,directory,advancedSignals);
            else if (verification&&e.Args.Contains("--verify-taskbar")) await TaskbarVerifier.RunAsync(controller,directory);
            else if (verification) await IntegrationVerifier.RunAsync(controller, store, directory);
            else if (e.Args.Contains("--verify")) await SmokeVerifier.RunAsync(controller, store, directory, verificationMonitors);
            else if (e.Args.Contains("--inspect") && Environment.GetEnvironmentVariable("CLAXE_DATA_DIR") is not null)
            {
                foreach (var floating in controller.Windows) floating.ShowInTaskbar = true;
                controller.ShowSettings();
            }
            else if (e.Args.Contains("--desktop-follow")) await controller.ConfigureDesktopFollowAsync(true);
            else if (e.Args.Contains("--dock")) controller.SummonDock();
            else if (e.Args.Contains("--launcher")) controller.ShowLauncher();
            else if (!controller.IsSafeMode && !controller.ViewModel.Settings.IntegratedDesktop) controller.ShowControls();
        }
        catch (Exception ex) { log.Write("startup-error", ex); MessageBox.Show("Noxelyra başlatılamadı. Windows arayüzü değişmedi. Yerel tanılama günlüğünü kontrol edin.", "Noxelyra Desktop", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1); }
    }
    protected override void OnExit(ExitEventArgs e) { commands?.Dispose();instance?.Dispose(); base.OnExit(e); }
}
