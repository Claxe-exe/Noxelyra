using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.Platform.Win;

/// <summary>
/// Recent files from Windows' own Recent folder, so turning off "show recently opened items" in Windows also empties this list.
/// An app's recent files are matched by default file association; per-app jump lists have no documented format.
/// </summary>
public static class RecentFiles
{
    private static readonly Dictionary<string, string?> openers = new(StringComparer.OrdinalIgnoreCase);
    private static IReadOnlyList<RecentFile> cached = [];
    private static long readAt = -60000;
    public static async Task<IReadOnlyList<RecentFile>> ReadAsync()
    {
        if (Environment.TickCount64 - readAt < 30000) return cached;
        cached = await Task.Run(() => Read(40)); readAt = Environment.TickCount64;
        return cached;
    }
    public static IReadOnlyList<RecentFile> Read(int limit)
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
        if (!Directory.Exists(folder)) return [];
        var found = new List<RecentFile>(); object? shell = null;
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell"); if (type is null) return [];
            shell = Activator.CreateInstance(type); dynamic host = shell!;
            foreach (var link in new DirectoryInfo(folder).EnumerateFiles("*.lnk").OrderByDescending(f => f.LastWriteTimeUtc).Take(limit * 3))
            {
                if (found.Count >= limit) break;
                try
                {
                    dynamic shortcut = host.CreateShortcut(link.FullName);
                    string target = shortcut.TargetPath; Marshal.ReleaseComObject(shortcut);
                    if (target.Length > 0 && !target.StartsWith(@"\\") && File.Exists(target)) found.Add(new(target, Path.GetFileName(target), link.LastWriteTime));
                }
                catch (COMException) { }
            }
        }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or InvalidCastException) { }
        finally { if (shell is not null) Marshal.ReleaseComObject(shell); }
        return found;
    }
    /// <summary>The app Windows opens this extension with (AssocQueryString), or null.</summary>
    public static string? OpenerOf(string extension)
    {
        if (extension.Length == 0) return null;
        lock (openers)
        {
            if (openers.TryGetValue(extension, out var known)) return known;
            uint size = 1024; var text = new StringBuilder((int)size);
            var result = AssocQueryString(0, 2, extension, null, text, ref size) == 0 && text.Length > 0 ? text.ToString() : null;
            return openers[extension] = result;
        }
    }
    public static async Task<IReadOnlyList<RecentFile>> ForAppAsync(string executable) => RecentMatch.ForApp(await ReadAsync(), executable, OpenerOf);
    public static void Open(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, EntryPoint = "AssocQueryStringW")] private static extern int AssocQueryString(uint flags, uint what, string association, string? extra, StringBuilder output, ref uint size);
}
