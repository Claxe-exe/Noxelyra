using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ClaxeDesktop.Core;
using Microsoft.Win32;
namespace ClaxeDesktop.Platform.Win;

/// <summary>Reads need no rights; changes need an elevated process and go through the documented Service Control Manager and sc.exe.</summary>
public sealed class WindowsServiceControl : IServiceControl
{
    private const uint Running = 4, StartPending = 2, Stopped = 1;
    public ServiceState Read(string name)
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name) ?? throw new InvalidOperationException(name + " servisi bu Windows'ta yok.");
        var start = key.GetValue("Start") is int value ? value : 3;
        var delayed = key.GetValue("DelayedAutostart") is int d && d == 1;
        var state = Status(name, 0x0004);
        return new(name, start switch { 2 => delayed ? ServiceStart.AutomaticDelayed : ServiceStart.Automatic, 4 => ServiceStart.Disabled, _ => ServiceStart.Manual }, state is Running or StartPending);
    }
    public void SetStart(string name, ServiceStart start)
    {
        Guard(name);
        var mode = start switch { ServiceStart.Automatic => "auto", ServiceStart.AutomaticDelayed => "delayed-auto", ServiceStart.Disabled => "disabled", _ => "demand" };
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "sc.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        foreach (var argument in new[] { "config", name, "start=", mode }) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("sc.exe başlatılamadı.");
        process.StandardOutput.ReadToEnd();
        if (!process.WaitForExit(15000)) { process.Kill(); throw new TimeoutException("sc.exe yanıt vermedi."); }
        if (process.ExitCode != 0) throw new InvalidOperationException($"{name} başlangıç türü değiştirilemedi (sc.exe {process.ExitCode}).");
    }
    public void Stop(string name) => Change(name, Stopped, service => ControlService(service, 1, out _));
    public void Start(string name) => Change(name, Running, service => StartService(service, 0, 0));
    private static void Guard(string name) { if (!ServiceTweaks.Allowed(name)) throw new InvalidOperationException("İzin verilmeyen servis."); }
    private static void Change(string name, uint target, Func<nint, bool> action)
    {
        Guard(name);
        // SERVICE_QUERY_STATUS | SERVICE_START | SERVICE_STOP
        WithService(name, 0x0004 | 0x0010 | 0x0020, service =>
        {
            if (Query(service) == target) return 0u;
            if (!action(service) && Marshal.GetLastWin32Error() is not (1056 or 1062)) throw new Win32Exception();
            for (int i = 0; i < 80; i++) { if (Query(service) == target) return 0u; Thread.Sleep(250); }
            throw new TimeoutException(name + " servisi 20 saniyede durum değiştirmedi.");
        });
    }
    private static uint Status(string name, uint access) => WithService(name, access, Query);
    private static uint Query(nint service) { if (!QueryServiceStatus(service, out var status)) throw new Win32Exception(); return status.State; }
    private static T WithService<T>(string name, uint access, Func<nint, T> action)
    {
        var manager = OpenSCManager(null, null, 0x0001); if (manager == 0) throw new Win32Exception();
        try
        {
            var service = OpenService(manager, name, access); if (service == 0) throw new Win32Exception();
            try { return action(service); } finally { CloseServiceHandle(service); }
        }
        finally { CloseServiceHandle(manager); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct ServiceStatus { public uint Type, State, Accepted, ExitCode, SpecificExitCode, CheckPoint, WaitHint; }
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint OpenSCManager(string? machine, string? database, uint access);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint OpenService(nint manager, string name, uint access);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool QueryServiceStatus(nint service, out ServiceStatus status);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool ControlService(nint service, uint control, out ServiceStatus status);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool StartService(nint service, uint count, nint arguments);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool CloseServiceHandle(nint handle);
}
