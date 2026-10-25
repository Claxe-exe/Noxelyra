using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

/// <summary>
/// Finds where a player window shows its video: it watches the window for about 1.5 s with Windows Graphics Capture
/// (documented, works while the window is covered) and returns the largest area that keeps changing. Menus, lists and
/// buttons stay still, so they drop out. A still picture (audio-only track, paused video) has no such area and gives null.
/// Coordinates are window-rectangle pixels, the space DWM thumbnails crop in. Nothing is stored or sent anywhere.
/// </summary>
internal static class VideoRegion
{
    private const int Frames = 14, Columns = 96;
    internal static async Task<(int Left, int Top, int Right, int Bottom)?> FindAsync(nint window, CancellationToken token)
    {
        if (!GraphicsCaptureSession.IsSupported() || WindowOps.Read(window) is not { Minimized: false }) return null;
        IDirect3DDevice? device = null; Direct3D11CaptureFramePool? pool = null; GraphicsCaptureSession? session = null;
        try
        {
            var item = CreateItem(window); device = CreateDevice();
            pool = Direct3D11CaptureFramePool.CreateFreeThreaded(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, item.Size);
            session = pool.CreateCaptureSession(item); HideBorder(session); session.StartCapture();
            var grids = new List<float[]>(); int width = 0, height = 0, cell = 8;
            for (int i = 0; i < Frames && !token.IsCancellationRequested; i++)
            {
                await Task.Delay(110, token);
                using var frame = pool.TryGetNextFrame(); if (frame is null) continue;
                using var bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface, BitmapAlphaMode.Premultiplied);
                if (width == 0) { width = bitmap.PixelWidth; height = bitmap.PixelHeight; cell = Math.Max(8, width / Columns); }
                if (bitmap.PixelWidth != width || bitmap.PixelHeight != height) continue;
                grids.Add(Luminance(bitmap, cell));
            }
            if (grids.Count < 4 || width == 0) return null;
            var box = MovingArea(grids, width / cell, height / cell);
            if (box is not { } b) return null;
            var (dx, dy) = WindowOps.FrameOffset(window);
            return (b.X0 * cell + dx, b.Y0 * cell + dy, Math.Min(width, (b.X1 + 1) * cell) + dx, Math.Min(height, (b.Y1 + 1) * cell) + dy);
        }
        catch (OperationCanceledException) { return null; }
        catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException or UnauthorizedAccessException or InvalidOperationException) { return null; }
        finally { session?.Dispose(); pool?.Dispose(); device?.Dispose(); }
    }
    // Average brightness per cell, sampling every fourth pixel; enough to see motion, cheap on a 1080p frame.
    private static float[] Luminance(SoftwareBitmap bitmap, int cell)
    {
        int w = bitmap.PixelWidth, h = bitmap.PixelHeight, cols = w / cell, rows = h / cell;
        var buffer = new Windows.Storage.Streams.Buffer((uint)(w * h * 4)); bitmap.CopyToBuffer(buffer);
        var bytes = new byte[buffer.Length]; using (var reader = Windows.Storage.Streams.DataReader.FromBuffer(buffer)) reader.ReadBytes(bytes);
        var grid = new float[cols * rows];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                float sum = 0; int n = 0;
                for (int y = r * cell; y < (r + 1) * cell; y += 4)
                    for (int x = c * cell; x < (c + 1) * cell; x += 4) { var i = (y * w + x) * 4; sum += bytes[i] * .114f + bytes[i + 1] * .587f + bytes[i + 2] * .299f; n++; }
                grid[r * cols + c] = sum / Math.Max(1, n);
            }
        return grid;
    }
    // Cells that changed in at least a third of the frame pairs, then the largest connected patch of them.
    private static (int X0, int Y0, int X1, int Y1)? MovingArea(List<float[]> grids, int cols, int rows)
    {
        var changes = new int[cols * rows];
        for (int f = 1; f < grids.Count; f++) for (int i = 0; i < changes.Length; i++) if (Math.Abs(grids[f][i] - grids[f - 1][i]) > 6) changes[i]++;
        var need = Math.Max(2, (grids.Count - 1) / 3);
        var seen = new bool[changes.Length]; (int X0, int Y0, int X1, int Y1, int Count) best = default;
        for (int start = 0; start < changes.Length; start++)
        {
            if (seen[start] || changes[start] < need) continue;
            var queue = new Queue<int>(); queue.Enqueue(start); seen[start] = true;
            int x0 = cols, y0 = rows, x1 = 0, y1 = 0, count = 0;
            while (queue.Count > 0)
            {
                var i = queue.Dequeue(); int x = i % cols, y = i / cols; count++;
                x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y);
                foreach (var (nx, ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                {
                    if (nx < 0 || ny < 0 || nx >= cols || ny >= rows) continue;
                    var j = ny * cols + nx; if (seen[j] || changes[j] < need) continue;
                    seen[j] = true; queue.Enqueue(j);
                }
            }
            if (count > best.Count) best = (x0, y0, x1, y1, count);
        }
        var area = (best.X1 - best.X0 + 1) * (best.Y1 - best.Y0 + 1);
        // A real video is a sizeable patch, not a blinking caret or a progress bar.
        if (best.Count < 12 || area < cols * rows / 40 || best.X1 - best.X0 < 6 || best.Y1 - best.Y0 < 4) return null;
        return (best.X0, best.Y0, best.X1, best.Y1);
    }
    private static GraphicsCaptureItem CreateItem(nint window)
    {
        var factory = WinRT.ActivationFactory.Get("Windows.Graphics.Capture.GraphicsCaptureItem");
        var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factory.ThisPtr);
        var iid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
        Marshal.ThrowExceptionForHR(interop.CreateForWindow(window, ref iid, out var pointer));
        try { return GraphicsCaptureItem.FromAbi(pointer); } finally { Marshal.Release(pointer); }
    }
    private static IDirect3DDevice CreateDevice()
    {
        Marshal.ThrowExceptionForHR(D3D11CreateDevice(0, 1, 0, 0x20, 0, 0, 7, out var d3d, out _, out var context));
        try
        {
            var dxgiIid = new Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(d3d, in dxgiIid, out var dxgi));
            try
            {
                Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi, out var inspectable));
                try { return WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(inspectable); } finally { Marshal.Release(inspectable); }
            }
            finally { Marshal.Release(dxgi); }
        }
        finally { Marshal.Release(d3d); if (context != 0) Marshal.Release(context); }
    }
    // Windows 11 can capture without the yellow border; on Windows 10 the border shows for the 1.5 s of watching.
    private static void HideBorder(GraphicsCaptureSession session)
    {
        try
        {
            var iid = new Guid("F2CDD966-22AE-5EA1-9596-3A289344C3BE");
            var native = ((WinRT.IWinRTObject)session).NativeObject.ThisPtr;
            if (Marshal.QueryInterface(native, in iid, out var pointer) != 0) return;
            try { ((IGraphicsCaptureSession3)Marshal.GetObjectForIUnknown(pointer)).put_IsBorderRequired(false); } finally { Marshal.Release(pointer); }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException) { }
    }
    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        [PreserveSig] int CreateForWindow(nint window, ref Guid iid, out nint result);
        [PreserveSig] int CreateForMonitor(nint monitor, ref Guid iid, out nint result);
    }
    [ComImport, Guid("F2CDD966-22AE-5EA1-9596-3A289344C3BE"), InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    private interface IGraphicsCaptureSession3
    {
        [PreserveSig] int get_IsBorderRequired(out byte value);
        [PreserveSig] int put_IsBorderRequired([MarshalAs(UnmanagedType.U1)] bool value);
    }
    [DllImport("d3d11.dll")] private static extern int D3D11CreateDevice(nint adapter, int driverType, nint software, uint flags, nint levels, uint count, uint sdkVersion, out nint device, out int level, out nint context);
    [DllImport("d3d11.dll")] private static extern int CreateDirect3D11DeviceFromDXGIDevice(nint dxgiDevice, out nint graphicsDevice);
}
