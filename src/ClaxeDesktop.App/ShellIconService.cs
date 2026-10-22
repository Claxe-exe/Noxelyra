using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace ClaxeDesktop.App;
public sealed record ShellAsset(ImageSource? Icon, string? Target);
public sealed class ShellIconService : IDisposable
{
    private readonly BlockingCollection<(string Path, TaskCompletionSource<ShellAsset> Completion)> queue = new();
    private readonly Dictionary<string, Task<ShellAsset>> cache = new(StringComparer.OrdinalIgnoreCase);
    public ShellIconService()
    {
        var worker = new Thread(Work) { IsBackground = true, Name = "Noxelyra shell icons" }; worker.SetApartmentState(ApartmentState.STA); worker.Start();
    }
    public Task<ShellAsset> GetAsync(string path)
    {
        if (cache.TryGetValue(path, out var known)) return known;
        if (cache.Count >= 256) { var done = cache.FirstOrDefault(p => p.Value.IsCompleted); if (done.Key is not null) cache.Remove(done.Key); }
        var source = new TaskCompletionSource<ShellAsset>(TaskCreationOptions.RunContinuationsAsynchronously); cache[path] = source.Task;
        if (queue.IsAddingCompleted) source.TrySetResult(new(null, null)); else queue.Add((path, source));
        return source.Task;
    }
    private void Work()
    {
        var com = CoInitializeEx(0, 2);
        try
        {
        foreach (var item in queue.GetConsumingEnumerable())
        {
            nint icon = 0;
            try
            {
                if (!Path.IsPathFullyQualified(item.Path) || item.Path.StartsWith(@"\\") || (!File.Exists(item.Path) && !Directory.Exists(item.Path))) { item.Completion.SetResult(new(null, null)); continue; }
                var target = item.Path;
                if (Path.GetExtension(target).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    dynamic? shell = null; dynamic? shortcut = null;
                    try
                    {
                        var type = Type.GetTypeFromProgID("WScript.Shell");
                        if (type is not null)
                        {
                            shell = Activator.CreateInstance(type); shortcut = shell!.CreateShortcut(target); target = (string)shortcut.TargetPath;
                            var iconLocation = Environment.ExpandEnvironmentVariables((string)shortcut.IconLocation).Split(',')[0].Trim().Trim('"');
                            if (!ClaxeDesktop.Core.LaunchPolicy.IsAllowed(target) || (iconLocation.Length > 0 && (!Path.IsPathFullyQualified(iconLocation) || iconLocation.StartsWith(@"\\"))))
                            { item.Completion.SetResult(new(null, null)); continue; }
                        }
                        else { item.Completion.SetResult(new(null, null)); continue; }
                    }
                    finally { if (shortcut is not null) Marshal.FinalReleaseComObject(shortcut); if (shell is not null) Marshal.FinalReleaseComObject(shell); }
                }
                var info = new ShellFileInfo(); _ = SHGetFileInfo(item.Path, 0, ref info, (uint)Marshal.SizeOf<ShellFileInfo>(), 0x100); icon = info.Icon;
                BitmapSource? bitmap = icon == 0 ? null : Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bitmap?.Freeze(); item.Completion.SetResult(new(bitmap, target));
            }
            catch (Exception) { item.Completion.TrySetResult(new(null, null)); }
            finally { if (icon != 0) DestroyIcon(icon); }
        }
        }
        finally { if (com >= 0) CoUninitialize(); }
    }
    public void Dispose() => queue.CompleteAdding();
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct ShellFileInfo
    { public nint Icon; public int Index; public uint Attributes; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern nint SHGetFileInfo(string path, uint attributes, ref ShellFileInfo info, uint size, uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
}
