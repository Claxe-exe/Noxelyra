using System.Diagnostics;
namespace ClaxeDesktop.Platform.Win;

public sealed record DesktopFile(string Path, string Name, bool Directory);
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
                        files.Add(new(path, Path.GetFileNameWithoutExtension(path), attributes.HasFlag(FileAttributes.Directory)));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return files.GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderBy(f => !f.Directory).ThenBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase).Take(300).ToArray();
    }
    public static void Open(DesktopFile file)
    {
        var full = Path.GetFullPath(file.Path);
        if (!Roots.Any(root => string.Equals(Path.GetDirectoryName(full), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))) throw new IOException("Öğe masaüstü klasöründe değil.");
        if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw new IOException("Bağlantı öğesi kullanılamıyor.");
        Process.Start(new ProcessStartInfo(full) { UseShellExecute = true });
    }
}
