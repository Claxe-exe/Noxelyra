using System.Runtime.InteropServices;
namespace ClaxeDesktop.Platform.Win;

/// <summary>GPU 3D load, disk activity and network throughput from Windows performance counters; null when a counter is missing.</summary>
public sealed record PerformanceSample(double? Gpu, double? Disk, double? NetworkBytesPerSecond);

/// <summary>
/// Reads documented PDH counters with English paths (works on any display language). One instance per visible graph widget;
/// the first sample after creation is a baseline because rate counters need two collections.
/// </summary>
public sealed class PerformanceSampler : IDisposable
{
    private const uint Double = 0x00000200, NoCap100 = 0x00008000;
    private const int MoreData = unchecked((int)0x800007D2);
    private nint query;
    private readonly nint gpu, disk, network;
    private readonly object gate = new();
    public PerformanceSampler()
    {
        if (PdhOpenQueryW(null, 0, out query) != 0) { query = 0; return; }
        gpu = Add(@"\GPU Engine(*engtype_3D)\Utilization Percentage");
        disk = Add(@"\PhysicalDisk(_Total)\% Idle Time");
        network = Add(@"\Network Interface(*)\Bytes Total/sec");
        PdhCollectQueryData(query);
    }
    private nint Add(string path) => PdhAddEnglishCounterW(query, path, 0, out var counter) == 0 ? counter : 0;
    public PerformanceSample Sample()
    {
        lock (gate)
        {
            if (query == 0 || PdhCollectQueryData(query) != 0) return new(null, null, null);
            var gpuLoad = Sum(gpu) is { } g ? Math.Clamp(g, 0, 100) : (double?)null;
            var idle = Single(disk); var diskLoad = idle is { } i ? Math.Clamp(100 - i, 0, 100) : (double?)null;
            return new(gpuLoad, diskLoad, Sum(network) is { } n && n >= 0 ? n : null);
        }
    }
    private double? Single(nint counter)
    {
        if (counter == 0 || PdhGetFormattedCounterValue(counter, Double | NoCap100, out _, out var value) != 0 || value.Status != 0) return null;
        return value.Value;
    }
    // Wildcard counters (one instance per GPU engine or network adapter) are summed.
    private double? Sum(nint counter)
    {
        if (counter == 0) return null;
        uint size = 0, count = 0;
        var status = PdhGetFormattedCounterArrayW(counter, Double | NoCap100, ref size, ref count, 0);
        if (status == 0 && count == 0) return 0;
        if (status != MoreData || size == 0) return null;
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (PdhGetFormattedCounterArrayW(counter, Double | NoCap100, ref size, ref count, buffer) != 0) return null;
            double total = 0; var stride = Marshal.SizeOf<Item>();
            for (int index = 0; index < count; index++)
            {
                var item = Marshal.PtrToStructure<Item>(buffer + index * stride);
                if (item.Value.Status == 0 && double.IsFinite(item.Value.Value)) total += item.Value.Value;
            }
            return total;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    public void Dispose() { lock (gate) { if (query != 0) PdhCloseQuery(query); query = 0; } }
    [StructLayout(LayoutKind.Sequential)] private struct Reading { public uint Status; public double Value; }
    [StructLayout(LayoutKind.Sequential)] private struct Item { public nint Name; public Reading Value; }
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern int PdhOpenQueryW(string? source, nint user, out nint query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern int PdhAddEnglishCounterW(nint query, string path, nint user, out nint counter);
    [DllImport("pdh.dll")] private static extern int PdhCollectQueryData(nint query);
    [DllImport("pdh.dll")] private static extern int PdhGetFormattedCounterValue(nint counter, uint format, out uint type, out Reading value);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern int PdhGetFormattedCounterArrayW(nint counter, uint format, ref uint size, ref uint count, nint buffer);
    [DllImport("pdh.dll")] private static extern int PdhCloseQuery(nint query);
}
