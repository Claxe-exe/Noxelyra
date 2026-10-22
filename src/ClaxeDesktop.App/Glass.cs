using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

/// <summary>Turns a WPF window into a real Windows 11 glass window. Must run before the window is shown.</summary>
internal static class Glass
{
    internal static bool Available => SystemBackdrop.Supported && !SystemParameters.HighContrast;
    /// <summary>Chromeless surfaces (dock, widgets) use Acrylic; framed app windows use Mica behind their own title bar.</summary>
    internal static bool Prepare(Window window, BackdropKind kind, bool chromeless, Action<bool>? applied = null)
    {
        if (!Available) return false;
        window.AllowsTransparency = false;
        if (chromeless) WindowChrome.SetWindowChrome(window, new WindowChrome { GlassFrameThickness = new(-1), CaptionHeight = 0, ResizeBorderThickness = new(0), CornerRadius = new(0), UseAeroCaptionButtons = false });
        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (HwndSource.FromHwnd(handle) is { CompositionTarget: { } target }) target.BackgroundColor = Colors.Transparent;
            var ok = SystemBackdrop.Apply(handle, kind);
            if (ok && chromeless) SystemBackdrop.Round(handle);
            applied?.Invoke(ok);
        };
        return true;
    }
    /// <summary>Framed windows: keep the theme's canvas as a light tint so text stays readable over Mica.</summary>
    internal static void PrepareFramed(Window window)
    {
        Prepare(window, BackdropKind.Mica, false, ok =>
        {
            if (!ok) return;
            var canvas = (window.TryFindResource("Canvas") as SolidColorBrush)?.Color ?? Colors.Black; canvas.A = 0xD8;
            window.Background = new SolidColorBrush(canvas);
        });
    }
}
