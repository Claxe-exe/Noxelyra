using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace ClaxeDesktop.WindowProbe;
internal static class Program
{
    [STAThread] private static void Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        for (var i = 0; i < 10; i++)
        {
            var window = new Window { Title = $"Claxe QA Window {i + 1:00}", Width = 240, Height = 140, Left = 60 + i * 24, Top = 120 + i * 20, ShowActivated = false, Content = new TextBlock { Text = "Claxe test penceresi · otomatik kapanır", TextWrapping = TextWrapping.Wrap, Margin = new(15) } };
            window.Show();
            if (i == 9) window.WindowState = WindowState.Minimized;
        }
        var tool = new Window { Title = "Claxe QA excluded tool", Width = 160, Height = 80, WindowStyle = WindowStyle.ToolWindow, ShowInTaskbar = false, ShowActivated = false }; tool.Show();
        _ = app.Dispatcher.BeginInvoke(() =>
        {
            if (Environment.GetEnvironmentVariable("CLAXE_WINDOW_PROBE_READY") is { } ready) System.IO.File.WriteAllText(ready, "ready");
        }, DispatcherPriority.ApplicationIdle);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Environment.GetCommandLineArgs().Contains("--interactive") ? 120 : 45) }; timer.Tick += (_, _) => app.Shutdown(); timer.Start(); app.Run();
    }
}
