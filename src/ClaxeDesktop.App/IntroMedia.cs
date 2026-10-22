using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace ClaxeDesktop.App;

/// <summary>Reads user intro clips: GIF frames with their delays (WPF only shows a GIF's first frame) and MP4 duration.</summary>
internal static class IntroMedia
{
    internal sealed record GifFrame(BitmapSource Image, Rect Area, TimeSpan Delay, bool ClearAfter);
    internal sealed record Gif(int Width, int Height, IReadOnlyList<GifFrame> Frames)
    {
        public TimeSpan Duration => TimeSpan.FromTicks(Frames.Sum(f => f.Delay.Ticks));
    }
    internal static bool IsGif(string path) => Path.GetExtension(path).Equals(".gif", StringComparison.OrdinalIgnoreCase);
    internal static Gif LoadGif(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count is 0 or > 600) throw new InvalidDataException("GIF en fazla 600 kare olabilir.");
        T Read<T>(BitmapMetadata? metadata, string query, T fallback) { try { return metadata?.GetQuery(query) is T value ? value : fallback; } catch (Exception ex) when (ex is NotSupportedException or ArgumentException) { return fallback; } }
        var meta = decoder.Metadata as BitmapMetadata;
        int width = Read<ushort>(meta, "/logscrdesc/Width", 0), height = Read<ushort>(meta, "/logscrdesc/Height", 0);
        if (width == 0 || height == 0) { width = decoder.Frames[0].PixelWidth; height = decoder.Frames[0].PixelHeight; }
        var frames = decoder.Frames.Select(frame =>
        {
            var data = frame.Metadata as BitmapMetadata;
            // GIF delays are in hundredths of a second; browsers treat 0-1 as 100 ms.
            var delay = Read<ushort>(data, "/grctlext/Delay", 10); if (delay < 2) delay = 10;
            var area = new Rect(Read<ushort>(data, "/imgdesc/Left", 0), Read<ushort>(data, "/imgdesc/Top", 0), frame.PixelWidth, frame.PixelHeight);
            frame.Freeze();
            return new GifFrame(frame, area, TimeSpan.FromMilliseconds(delay * 10), Read<byte>(data, "/grctlext/Disposal", 0) == 2);
        }).ToList();
        return new(width, height, frames);
    }
    /// <summary>Opens a video just long enough to read its length; null when Windows cannot decode it.</summary>
    internal static async Task<TimeSpan?> VideoDurationAsync(string path)
    {
        var player = new MediaPlayer { IsMuted = true }; var result = new TaskCompletionSource<TimeSpan?>();
        player.MediaOpened += (_, _) => result.TrySetResult(player.NaturalDuration.HasTimeSpan ? player.NaturalDuration.TimeSpan : null);
        player.MediaFailed += (_, _) => result.TrySetResult(null);
        player.Open(new Uri(path));
        var finished = await Task.WhenAny(result.Task, Task.Delay(8000));
        player.Close();
        return finished == result.Task ? result.Task.Result : null;
    }
    /// <summary>Composites GIF frames onto one surface, honouring frame offsets and "restore to background".</summary>
    internal sealed class GifPlayer
    {
        private readonly Gif gif; private readonly RenderTargetBitmap surface; private int index;
        private readonly System.Windows.Threading.DispatcherTimer timer = new();
        internal ImageSource Surface => surface;
        internal event Action? Ended;
        internal GifPlayer(Gif gif) { this.gif = gif; surface = new(gif.Width, gif.Height, 96, 96, PixelFormats.Pbgra32); timer.Tick += (_, _) => Next(); }
        internal void Play() { index = 0; Draw(0); timer.Interval = gif.Frames[0].Delay; timer.Start(); }
        internal void Stop() => timer.Stop();
        private void Next()
        {
            if (gif.Frames[index].ClearAfter) surface.Clear();
            if (++index >= gif.Frames.Count) { timer.Stop(); Ended?.Invoke(); return; }
            Draw(index); timer.Interval = gif.Frames[index].Delay;
        }
        private void Draw(int i)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) dc.DrawImage(gif.Frames[i].Image, gif.Frames[i].Area);
            surface.Render(visual);
        }
    }
}
