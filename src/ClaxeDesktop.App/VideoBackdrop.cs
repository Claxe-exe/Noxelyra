using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

/// <summary>
/// The track's video behind the island card: a DWM thumbnail of the player window cropped to its video area and filling the
/// card (cover). DWM draws it on the GPU, so it costs almost nothing. It sits directly under the card, never takes focus and
/// lets clicks through. It is a separate normal window because DWM thumbnails do not draw into layered windows like the card.
/// </summary>
internal sealed class VideoBackdrop : Window
{
    private nint hwnd, thumbnail, source;
    private (int Left, int Top, int Right, int Bottom) crop;
    private int width, height;
    internal nint Source => source;
    public VideoBackdrop()
    {
        Title = "Noxelyra · Ada videosu"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        Background = Brushes.Black; Width = 10; Height = 10;
        SourceInitialized += (_, _) =>
        {
            hwnd = new WindowInteropHelper(this).Handle;
            // No activation, not in Alt+Tab, clicks pass through to the card above.
            SetWindowLongPtr(hwnd, -20, GetWindowLongPtr(hwnd, -20) | 0x08000000 | 0x80 | 0x20);
            // DWM rounding clips the thumbnail too; the window region below may not.
            SystemBackdrop.Round(hwnd);
        };
        Closed += (_, _) => Release();
    }
    /// <summary>Shows the <paramref name="video"/> part of <paramref name="player"/> in the screen rectangle, right under <paramref name="card"/>.</summary>
    internal void Present(nint card, nint player, (int Left, int Top, int Right, int Bottom) video, int x, int y, int w, int h, int radius)
    {
        if (!IsVisible) Show();
        SetWindowPos(hwnd, card, x, y, w, h, 0x0010);
        // Re-applied on every call: the region can be reset when the window is resized or shown again.
        SetWindowRgn(hwnd, CreateRoundRectRgn(0, 0, w + 1, h + 1, radius * 2, radius * 2), w != width || h != height); width = w; height = h;
        if (player != source) { Release(); source = player; thumbnail = DwmThumbnail.Register(hwnd, player); }
        crop = video; Fit();
    }
    // Cover: trim the video's long side so it fills the card without bars.
    private void Fit()
    {
        if (thumbnail == 0 || width <= 0 || height <= 0) return;
        double l = crop.Left, t = crop.Top, cw = crop.Right - crop.Left, ch = crop.Bottom - crop.Top, aspect = (double)width / height;
        if (cw <= 0 || ch <= 0) return;
        if (cw / ch > aspect) { var nw = ch * aspect; l += (cw - nw) / 2; cw = nw; } else { var nh = cw / aspect; t += (ch - nh) / 2; ch = nh; }
        DwmThumbnail.ShowCropped(thumbnail, (0, 0, width, height), ((int)l, (int)t, (int)(l + cw), (int)(t + ch)));
    }
    internal void Release() { DwmThumbnail.Unregister(thumbnail); thumbnail = 0; source = 0; }
    internal void Leave() { Release(); if (IsVisible) Hide(); }
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(nint hwnd, nint region, bool redraw);
    [DllImport("gdi32.dll")] private static extern nint CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
}
