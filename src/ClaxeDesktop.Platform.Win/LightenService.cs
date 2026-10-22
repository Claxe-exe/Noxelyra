using System.Diagnostics;
using ClaxeDesktop.Core;
using Microsoft.Win32;
namespace ClaxeDesktop.Platform.Win;

/// <summary>Read-only views of what runs at sign-in and what uses memory. Changes go through <see cref="WindowsLookSession"/>.</summary>
public static class LightenService
{
    private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static IReadOnlyList<StartupEntry> Startup()
    {
        var result = new List<StartupEntry>();
        void FromRun(RegistryKey hive, string key, string approvalKey, StartupSource source)
        {
            using var run = hive.OpenSubKey(key); using var approved = hive.OpenSubKey(approvalKey);
            if (run is null) return;
            foreach (var name in run.GetValueNames().Where(n => n.Length > 0))
                result.Add(new(name, run.GetValue(name)?.ToString() ?? "", source, Lighten.IsEnabled(approved?.GetValue(name) as byte[])));
        }
        void FromFolder(string folder, RegistryKey hive, StartupSource source)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
            using var approved = hive.OpenSubKey(Lighten.ApprovedRoot + @"\StartupFolder");
            foreach (var file in Directory.EnumerateFiles(folder).Where(f => !Path.GetFileName(f).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)))
                result.Add(new(Path.GetFileName(file), file, source, Lighten.IsEnabled(approved?.GetValue(Path.GetFileName(file)) as byte[])));
        }
        FromRun(Registry.CurrentUser, Run, Lighten.ApprovedRoot + @"\Run", StartupSource.UserRun);
        FromFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup), Registry.CurrentUser, StartupSource.UserFolder);
        FromRun(Registry.LocalMachine, Run, Lighten.ApprovedRoot + @"\Run", StartupSource.MachineRun);
        FromRun(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", Lighten.ApprovedRoot + @"\Run32", StartupSource.MachineRun);
        FromFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), Registry.LocalMachine, StartupSource.MachineFolder);
        return result.OrderBy(e => e.UserScope ? 0 : 1).ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
    public static IReadOnlyList<MemoryUse> Memory(int top)
    {
        var processes = Process.GetProcesses();
        try { return Lighten.Group(processes.Select(p => { try { return (p.ProcessName, p.WorkingSet64); } catch (InvalidOperationException) { return (p.ProcessName, 0L); } }), top); }
        finally { foreach (var p in processes) p.Dispose(); }
    }
    public static void OpenTaskManagerStartup() => Process.Start(new ProcessStartInfo("taskmgr.exe", "/0 /startup") { UseShellExecute = true })?.Dispose();
    public static void OpenWindowsUpdate() => Process.Start(new ProcessStartInfo("ms-settings:windowsupdate") { UseShellExecute = true })?.Dispose();
}
