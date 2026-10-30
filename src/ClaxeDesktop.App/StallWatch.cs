using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;

/// <summary>
/// Notes in the local log when the main UI thread stops answering for seconds, with the stage it was in. Windows reports such
/// a stall as a hang (and offers to end the app), so the "start" entry is written while it is still happening.
/// </summary>
internal static class StallWatch
{
    private static volatile string stage = "";
    private static int started;
    internal static string Stage { get => stage; set => stage = value; }
    internal static void Start(LocalLog log)
    {
        if (Interlocked.Exchange(ref started, 1) == 1) return;
        var dispatcher = Application.Current.Dispatcher;
        new Thread(() =>
        {
            while (!dispatcher.HasShutdownStarted)
            {
                var answered = new ManualResetEventSlim(); var waited = Stopwatch.StartNew();
                dispatcher.BeginInvoke(answered.Set, DispatcherPriority.Send);
                if (!answered.Wait(3000))
                {
                    var during = stage; log.Write("ui-stall-start " + during);
                    answered.Wait(TimeSpan.FromMinutes(2));
                    log.Write($"ui-stall-end {during} {waited.Elapsed.TotalSeconds:0.0}s");
                }
                Thread.Sleep(1000);
            }
        }) { IsBackground = true, Name = "Noxelyra stall watch", Priority = ThreadPriority.BelowNormal }.Start();
    }
}
