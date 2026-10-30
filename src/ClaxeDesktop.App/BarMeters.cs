using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
namespace ClaxeDesktop.App;

/// <summary>
/// Download/upload speed from the adapters that carry the default route (so virtual switches are not counted twice), with a
/// one-minute history for the bar's graph. Sampled off the UI thread.
/// </summary>
internal sealed class NetworkMeter
{
    internal const int HistoryLength = 60;
    /// <summary>Sampled once a second by the bar; read by the island card.</summary>
    internal static readonly NetworkMeter Shared = new();
    private readonly object gate = new();
    private readonly Queue<(double Down, double Up)> history = new();
    private long lastDown, lastUp; private DateTime lastAt; private bool primed;
    internal (double Down, double Up) Current { get; private set; }
    internal long SessionDown { get; private set; }
    internal long SessionUp { get; private set; }
    internal (double Down, double Up)[] History { get { lock (gate) return history.ToArray(); } }
    internal void Sample()
    {
        long down = 0, up = 0;
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            try
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                if (nic.GetIPProperties().GatewayAddresses.Count == 0) continue;
                var stats = nic.GetIPStatistics(); down += stats.BytesReceived; up += stats.BytesSent;
            }
            catch (Exception ex) when (ex is NetworkInformationException or PlatformNotSupportedException) { }
        }
        var now = DateTime.UtcNow;
        lock (gate)
        {
            // An adapter appearing or resetting its counters makes the total jump backwards; start again from there.
            if (primed && down >= lastDown && up >= lastUp)
            {
                var seconds = Math.Max(.25, (now - lastAt).TotalSeconds);
                Current = ((down - lastDown) / seconds, (up - lastUp) / seconds);
                SessionDown += down - lastDown; SessionUp += up - lastUp;
                history.Enqueue(Current); while (history.Count > HistoryLength) history.Dequeue();
            }
            lastDown = down; lastUp = up; lastAt = now; primed = true;
        }
    }
    internal static string Speed(double bytesPerSecond) => bytesPerSecond switch
    {
        < 1024 => $"{bytesPerSecond:0} B/s",
        < 1024 * 1024 => $"{bytesPerSecond / 1024:0} KB/s",
        _ => (bytesPerSecond / 1024 / 1024).ToString(bytesPerSecond < 10 * 1024 * 1024 ? "0.0" : "0", CultureInfo.GetCultureInfo("tr-TR")) + " MB/s",
    };
    internal static string Size(long bytes) => bytes switch
    {
        < 1024 * 1024 => $"{bytes / 1024.0:0} KB",
        < 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024:0} MB",
        _ => (bytes / 1024.0 / 1024 / 1024).ToString("0.00", CultureInfo.GetCultureInfo("tr-TR")) + " GB",
    };
}

/// <summary>The last minute of CPU and RAM load (percent), filled by the bar's tick.</summary>
internal static class SystemHistory
{
    private static readonly object gate = new();
    private static readonly Queue<(double Cpu, double Memory)> samples = new();
    internal static void Push(double cpu, double memory) { lock (gate) { samples.Enqueue((cpu, memory)); while (samples.Count > NetworkMeter.HistoryLength) samples.Dequeue(); } }
    internal static double[] Cpu { get { lock (gate) return samples.Select(s => s.Cpu).ToArray(); } }
    internal static double[] Memory { get { lock (gate) return samples.Select(s => s.Memory).ToArray(); } }
}

/// <summary>Per-app CPU and memory, grouped by process name, for the system panel. CPU needs two samples; protected processes are skipped.</summary>
internal sealed class ProcessMeter
{
    internal sealed record Usage(string Name, double Cpu, long Memory, int Count);
    private Dictionary<int, TimeSpan> last = new(); private DateTime lastAt;
    internal IReadOnlyList<Usage> Sample()
    {
        var now = DateTime.UtcNow; var elapsed = (now - lastAt).TotalMilliseconds; var next = new Dictionary<int, TimeSpan>();
        var groups = new Dictionary<string, (double Cpu, long Memory, int Count)>(StringComparer.OrdinalIgnoreCase);
        var processes = Process.GetProcesses();
        foreach (var process in processes)
        {
            using (process)
            {
                try
                {
                    if (process.Id is 0 or 4) continue;
                    var name = process.ProcessName; double cpu = 0;
                    try
                    {
                        var used = process.TotalProcessorTime; next[process.Id] = used;
                        if (last.TryGetValue(process.Id, out var before) && elapsed > 0) cpu = Math.Max(0, (used - before).TotalMilliseconds / elapsed / Environment.ProcessorCount * 100);
                    }
                    catch (Exception ex) when (ex is Win32Exception or NotSupportedException) { }
                    var group = groups.GetValueOrDefault(name);
                    groups[name] = (group.Cpu + cpu, group.Memory + process.WorkingSet64, group.Count + 1);
                }
                catch (InvalidOperationException) { /* Exited while being read. */ }
            }
        }
        last = next; lastAt = now;
        return groups.Select(g => new Usage(g.Key, g.Value.Cpu, g.Value.Memory, g.Value.Count)).ToList();
    }
}
