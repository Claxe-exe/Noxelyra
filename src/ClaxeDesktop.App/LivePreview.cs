using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

/// <summary>
/// Live DWM view of the window that plays the track, shown under the island card. Windows does not hand out the video itself,
/// so this is the player window scaled to fit; a minimized window has nothing to show. It never takes focus.
/// </summary>
internal sealed class LivePreview : Window
{
    private nint hwnd, thumbnail, source;
    private readonly TextBlock note = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new(20) };
    internal event Action? Clicked;
    internal nint Source => source;
    public LivePreview()
    {
        Title = "Noxelyra · Canlı önizleme"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        SetResourceReference(BackgroundProperty, "Canvas"); note.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        Content = new Grid { Children = { note } }; ToolTip = "Canlı pencere görüntüsü · tıkla: kapat";
        SourceInitialized += (_, _) =>
        {
            hwnd = new WindowInteropHelper(this).Handle; SystemBackdrop.Round(hwnd);
            SetWindowLongPtr(hwnd, -20, GetWindowLongPtr(hwnd, -20) | 0x08000000 | 0x80);
        };
        SizeChanged += (_, _) => Fit();
        MouseLeftButtonUp += (_, _) => Clicked?.Invoke();
        Closed += (_, _) => Release();
    }
    /// <summary>Shows <paramref name="window"/> at the given screen pixel position and DIP size.</summary>
    internal void Present(nint window, int x, int y, double width, double height)
    {
        Width = width; Height = height;
        if (!IsVisible) Show();
        WindowPlacement.Move(hwnd, x, y); WindowPlacement.RaiseTopmost(hwnd);
        if (window != source) { Release(); source = window; thumbnail = DwmThumbnail.Register(hwnd, window); }
        Fit();
    }
    /// <summary>Re-fits after the player window resized or was minimized.</summary>
    internal void Fit()
    {
        if (!IsVisible || thumbnail == 0) { note.Text = "Pencere görüntüsü alınamadı."; return; }
        var (w, h) = DwmThumbnail.SourceSize(thumbnail);
        var minimized = WindowOps.Read(source)?.Minimized == true;
        note.Text = minimized ? "Oynatıcı penceresi simge durumunda; görüntüsü yok." : w <= 0 || h <= 0 ? "Pencere görüntüsü alınamadı." : "";
        if (minimized || w <= 0 || h <= 0) { DwmThumbnail.Show(thumbnail, 0, 0, 0, 0, 0); return; }
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        double cw = ActualWidth * dpi.DpiScaleX, ch = ActualHeight * dpi.DpiScaleY, scale = Math.Min(cw / w, ch / h);
        double fw = w * scale, fh = h * scale, left = (cw - fw) / 2, top = (ch - fh) / 2;
        DwmThumbnail.Show(thumbnail, (int)left, (int)top, (int)(left + fw), (int)(top + fh));
    }
    internal void Release() { DwmThumbnail.Unregister(thumbnail); thumbnail = 0; source = 0; }
    internal void Leave() { Release(); Hide(); }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
}
