using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
using Microsoft.Win32;
namespace ClaxeDesktop.App;

internal static class DesktopAcceptanceVerifier
{
    internal static async Task RunAsync(DesktopController controller, string directory)
    {
        var checks = new List<object>(); string? error = null; Process? crash = null;
        ExternalWallpaperSession? realSession = null; ExternalWallpaperSession? crashedSession = null;
        Window? application = null; dynamic? shell = null; bool minimized = false;
        void Check(string name, bool passed, string? detail = null) { checks.Add(new { name, passed, detail }); if (!passed) throw new InvalidOperationException(name); }
        try
        {
            await controller.WaitAdvancedAsync();
            Check("Desktop follow defaults off and reveals only in explicitly enabled desktop mode", !new DesktopSpaceOptions().FollowDesktop && DesktopFollowPolicy.Decide(true, true, false, false, false, DesktopPresence.Desktop) == DesktopFollowAction.Reveal && DesktopFollowPolicy.Decide(true, false, false, false, false, DesktopPresence.Desktop) == DesktopFollowAction.None);
            Check("Locked safe and unknown desktop states do not reveal the layer", DesktopFollowPolicy.Decide(true, true, false, true, false, DesktopPresence.Desktop) == DesktopFollowAction.None && DesktopFollowPolicy.Decide(true, true, true, false, false, DesktopPresence.Desktop) == DesktopFollowAction.None && DesktopFollowPolicy.Decide(true, true, false, false, false, DesktopPresence.Unknown) == DesktopFollowAction.None);
            using (var native = new WindowsDesktopMonitor(() => { })) { native.Dispose(); }
            Check("Native out-of-context foreground observer registers and releases idempotently", true);
            await controller.ConfigureDesktopFollowAsync(true); var space = controller.SpaceWindow ?? throw new IOException("Layer missing");
            Check("Enabled desktop layer is persisted and excluded from taskbar without topmost", space.IsVisible && !space.ShowInTaskbar && !space.Topmost && (await new SettingsStore(directory).LoadAsync()).Settings.DesktopSpace.FollowDesktop);
            application = new Window { Title = "Noxelyra acceptance application", Width = 450, Height = 240 }; application.Show(); application.Activate();
            controller.ProcessDesktopPresence(DesktopPresence.Application);
            var layerHandle = new WindowInteropHelper(space).Handle; var appHandle = new WindowInteropHelper(application).Handle;
            Check("Desktop layer moves below a real application HWND", Above(appHandle, layerHandle) && !space.Topmost);
            controller.DismissDesktopSpace(); controller.ProcessDesktopPresence(DesktopPresence.Desktop); var suppressed = !space.IsVisible;
            controller.ProcessDesktopPresence(DesktopPresence.Application); var foregroundBeforeReveal = controller.ForegroundWindow; controller.ProcessDesktopPresence(DesktopPresence.Desktop);
            Check("Escape suppresses immediate reopen and the next desktop visit restores the layer", suppressed && space.IsVisible && !space.Topmost && controller.ForegroundWindow == foregroundBeforeReveal);
            // Real shell desktop transition, then restore the user's minimized applications in finally.
            application.Activate(); shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
            shell!.MinimizeAll(); minimized = true; await Task.Delay(900);
            var presence = WindowsDesktopMonitor.DescribeForeground();
            var shown = space.IsVisible && space.WindowState != WindowState.Minimized;
            shell!.UndoMinimizeALL(); minimized = false; await Task.Delay(350);
            Check("Shell MinimizeAll leaves the non-taskbar desktop layer visible", shown, "Shell MinimizeAll/UndoMinimizeALL, not a simulated key press; presence=" + presence);
            controller.ApplyPowerMode(PowerModes.Suspend); controller.DismissDesktopSpace(); controller.ProcessDesktopPresence(DesktopPresence.Desktop);
            var suspended = controller.IsSuspended && !space.IsVisible; controller.ApplyPowerMode(PowerModes.Resume);
            Check("Suspend event handler blocks desktop reveal and resume restores processing", suspended && !controller.IsSuspended, "Power event handlers invoked; the physical PC was not put to sleep.");
            var dock = controller.Windows.OfType<DockWindow>().Single(); var panel = controller.Windows.OfType<PanelWindow>().Single();
            PostMessage(new WindowInteropHelper(panel).Handle, 0x312, 754, 0); await Task.Delay(100); PostMessage(new WindowInteropHelper(panel).Handle, 0x312, 754, 0); await Task.Delay(100);
            var summoned = dock.IsSummoned && dock.IsVisible && dock.Topmost; dock.DismissSummon(); await controller.ConfigureDesktopFollowAsync(false);
            Check("Alt+K remains repeatable over the layer and follow mode can be disabled", summoned && !controller.ViewModel.Settings.DesktopSpace.FollowDesktop && space.ShowInTaskbar);
            var executable = Environment.GetEnvironmentVariable("NOXELYRA_ACCEPTANCE_WE") ?? throw new IOException("Real WE path missing");
            var video = Environment.GetEnvironmentVariable("NOXELYRA_ACCEPTANCE_VIDEO") ?? throw new IOException("Acceptance video missing");
            var service = new WindowsExternalWallpaperService(); var before = await service.ReadAsync(executable);
            Check("Real Wallpaper Engine 2.8 query yields a normalized existing local source", Path.IsPathFullyQualified(before) && File.Exists(before), FileVersionInfo.GetVersionInfo(executable).ProductVersion);
            var journal = Path.Combine(directory, "real-engine-test.json"); realSession = new(service, journal);
            await realSession.ApplyAsync(executable, video); var applied = string.Equals(await service.ReadAsync(executable), Path.GetFullPath(video), StringComparison.OrdinalIgnoreCase) && File.Exists(journal);
            await realSession.RestoreAsync(); var restored = string.Equals(await service.ReadAsync(executable), before, StringComparison.OrdinalIgnoreCase) && !File.Exists(journal);
            Check("Real WE switches to the fixture video and restores its original scene", applied && restored);
            var childData = Path.Combine(directory, "crash-fixture"); Directory.CreateDirectory(childData); crashedSession = new(service, Path.Combine(childData, "engine-recovery.json"));
            var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            info.Environment["CLAXE_DATA_DIR"] = childData; info.Environment["CLAXE_TEST_FIXTURE"] = "1";
            foreach (var value in new[] { "--verify-engine-crash", executable, video }) info.ArgumentList.Add(value);
            crash = Process.Start(info) ?? throw new IOException("Crash fixture missing");
            var ready = Path.Combine(childData, "engine-crash-ready");
            for (int i = 0; i < 150 && !File.Exists(ready) && !crash.HasExited; i++) await Task.Delay(100);
            if (!File.Exists(ready)) throw new IOException(File.Exists(Path.Combine(childData, "error.txt")) ? await File.ReadAllTextAsync(Path.Combine(childData, "error.txt")) : "Crash fixture did not become ready");
            crash.Kill(); await crash.WaitForExitAsync(); // Only this test's own Process instance, never Explorer/WE.
            for (int i = 0; i < 150 && File.Exists(Path.Combine(childData, "engine-recovery.json")); i++) await Task.Delay(100);
            var afterCrash = await service.ReadAsync(executable);
            Check("Guardian restores real WE scene after only the isolated fixture process crashes", string.Equals(afterCrash, before, StringComparison.OrdinalIgnoreCase) && !File.Exists(Path.Combine(childData, "engine-recovery.json")));
        }
        catch (Exception ex) { error = ex.ToString(); }
        finally
        {
            if (minimized && shell is not null) try { shell!.UndoMinimizeALL(); } catch { }
            if (shell is not null) Marshal.FinalReleaseComObject(shell);
            if (crash is not null) { if (!crash.HasExited) { crash.Kill(); await crash.WaitForExitAsync(); } crash.Dispose(); }
            try { if (crashedSession is not null) await crashedSession.RestoreAsync(); if (realSession is not null) await realSession.RestoreAsync(); } catch (Exception ex) { error = (error ?? "") + "\nRecovery: " + ex; }
            application?.Close();
            await File.WriteAllTextAsync(Path.Combine(directory, "desktop-acceptance.json"), JsonSerializer.Serialize(new { success = error is null, checks, error, monitorCount = controller.ViewModel.Monitors.Count }, new JsonSerializerOptions { WriteIndented = true }));
            controller.Exit();
        }
    }
    internal static async Task<int> RunCrashFixtureAsync(string directory, string executable, string video)
    {
        Directory.CreateDirectory(directory); var owner = TaskbarGuardian.CurrentOwner();
        async Task Guard()
        {
            var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            info.Environment["CLAXE_DATA_DIR"] = directory;
            foreach (var value in new[] { "--taskbar-guardian", owner.ProcessId.ToString(), owner.StartedUtcTicks.ToString() }) info.ArgumentList.Add(value);
            using var process = Process.Start(info) ?? throw new IOException("Guardian missing");
            for (int i = 0; i < 50; i++) { if (File.Exists(TaskbarGuardian.ReadyPath(directory, owner))) return; if (process.HasExited) break; await Task.Delay(50); }
            throw new IOException("Guardian not ready");
        }
        try
        {
            var session = new ExternalWallpaperSession(new WindowsExternalWallpaperService(), Path.Combine(directory, "engine-recovery.json"), owner, Guard);
            await session.ApplyAsync(executable, video); await File.WriteAllTextAsync(Path.Combine(directory, "engine-crash-ready"), "ready"); await Task.Delay(Timeout.Infinite); return 0;
        }
        catch (Exception ex) { await File.WriteAllTextAsync(Path.Combine(directory, "error.txt"), ex.ToString()); return 2; }
    }
    private static bool Above(nint first, nint second)
    {
        var windows = new List<nint>(); EnumWindows((w, _) => { windows.Add(w); return true; }, 0);
        return windows.IndexOf(first) >= 0 && windows.IndexOf(second) > windows.IndexOf(first);
    }
    private delegate bool EnumProc(nint window, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint hwnd, uint message, nint w, nint l);
}
