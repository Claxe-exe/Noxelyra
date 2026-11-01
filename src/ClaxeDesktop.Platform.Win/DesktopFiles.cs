using System.Diagnostics;
namespace ClaxeDesktop.Platform.Win;

public sealed record DesktopFile(string Path, string Name, bool Directory, string Extension = "", DateTime Modified = default, long Size = 0);
public static class DesktopFiles
{
    public static IReadOnlyList<string> Roots => new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) }.Where(p => !string.IsNullOrWhiteSpace(p) && !p.StartsWith(@"\\") && new DriveInfo(Path.GetPathRoot(p)!).DriveType != DriveType.Network).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public static IReadOnlyList<DesktopFile> Read()
    {
        var files = new List<DesktopFile>();
        foreach (var root in Roots)
        {
            try
            {
                foreach (var path in Directory.EnumerateFileSystemEntries(root).Take(300))
                {
                    try
                    {
                        var attributes = File.GetAttributes(path);
                        if ((attributes & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint | FileAttributes.Offline)) != 0) continue;
                        var folder = attributes.HasFlag(FileAttributes.Directory);
                        files.Add(folder ? new(path, Path.GetFileName(path), true, "", Directory.GetLastWriteTime(path)) : new(path, Path.GetFileNameWithoutExtension(path), false, Path.GetExtension(path), File.GetLastWriteTime(path), new FileInfo(path).Length));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return files.GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderBy(f => !f.Directory).ThenBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase).Take(300).ToArray();
    }
    /// <summary>Folders first, then by the chosen order: "name", "type", "date" (newest first) or "size" (largest first).</summary>
    public static IReadOnlyList<DesktopFile> Sort(IEnumerable<DesktopFile> files, string order)
    {
        var folders = files.OrderBy(f => !f.Directory);
        var sorted = order switch
        {
            "type" => folders.ThenBy(f => f.Extension, StringComparer.OrdinalIgnoreCase),
            "date" => folders.ThenByDescending(f => f.Modified),
            "size" => folders.ThenByDescending(f => f.Size),
            _ => folders,
        };
        return sorted.ThenBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    public static void Open(DesktopFile file)
    {
        var full = Path.GetFullPath(file.Path);
        if (!Roots.Any(root => string.Equals(Path.GetDirectoryName(full), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))) throw new IOException("Öğe masaüstü klasöründe değil.");
        if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw new IOException("Bağlantı öğesi kullanılamıyor.");
        Process.Start(new ProcessStartInfo(full) { UseShellExecute = true });
    }
}
