namespace ClaxeDesktop.App;
public sealed partial class PanelWindow
{
    private bool sampling;
    /// <summary>
    /// The bar's once-a-second tick also feeds the shared network and CPU/RAM history, so the island card's graphs are already
    /// full when it opens. Network counters are read off the UI thread.
    /// </summary>
    private async Task SampleAsync()
    {
        if (sampling) return; sampling = true;
        try { await Task.Run(NetworkMeter.Shared.Sample); SystemHistory.Push(vm.CpuValue, vm.MemoryValue); }
        catch (Exception ex) when (ex is System.Net.NetworkInformation.NetworkInformationException or InvalidOperationException) { }
        finally { sampling = false; }
    }
}
