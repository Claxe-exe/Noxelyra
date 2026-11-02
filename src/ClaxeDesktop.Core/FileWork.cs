using System.IO.Compression;
namespace ClaxeDesktop.Core;

/// <summary>Zip archives for the desktop. Extraction refuses entries that would land outside the target folder and archives that do not fit on the drive.</summary>
public static class ZipFiles
{
    public static void Create(IReadOnlyList<string> paths, string zipPath, CancellationToken token = default)
    {
        var temporary = zipPath + ".tmp";
        try
        {
            using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
                foreach (var path in paths)
                {
                    if (File.Exists(path)) { token.ThrowIfCancellationRequested(); zip.CreateEntryFromFile(path, Path.GetFileName(path)); continue; }
                    var root = path.TrimEnd('\\'); var parent = Path.GetDirectoryName(root) ?? root;
                    AddFolder(zip, root, parent, token);
                }
            File.Move(temporary, zipPath);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void AddFolder(ZipArchive zip, string folder, string parent, CancellationToken token)
    {
        var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        var empty = true;
        foreach (var entry in Directory.EnumerateFileSystemEntries(folder, "*", options))
        {
            empty = false; token.ThrowIfCancellationRequested();
            if (Directory.Exists(entry)) AddFolder(zip, entry, parent, token);
            else zip.CreateEntryFromFile(entry, Path.GetRelativePath(parent, entry).Replace('\\', '/'));
        }
        if (empty) zip.CreateEntry(Path.GetRelativePath(parent, folder).Replace('\\', '/') + "/");
    }
    /// <summary>Extracts into <paramref name="folder"/>, which must not exist yet; a failed extraction leaves nothing behind.</summary>
    public static void Extract(string zipPath, string folder, CancellationToken token = default)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var root = Path.GetFullPath(folder).TrimEnd('\\') + '\\';
        var targets = new List<(ZipArchiveEntry Entry, string Path)>();
        foreach (var entry in zip.Entries)
        {
            var target = Path.GetFullPath(Path.Combine(root, entry.FullName.Replace('/', '\\')));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Arşiv, klasörün dışına yazmaya çalışan bir öğe içeriyor.");
            targets.Add((entry, target));
        }
        var total = zip.Entries.Sum(e => e.Length);
        if (Path.GetPathRoot(root) is { } drive && total > new DriveInfo(drive).AvailableFreeSpace) throw new InvalidDataException("Arşivi açmak için diskte yeterli yer yok.");
        try
        {
            Directory.CreateDirectory(root);
            foreach (var (entry, target) in targets)
            {
                token.ThrowIfCancellationRequested();
                if (entry.FullName.EndsWith('/')) { Directory.CreateDirectory(target); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, false);
            }
        }
        catch { try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (IOException) { } throw; }
    }
}

/// <summary>The days a month page shows: six Monday-first weeks, like the Windows calendar.</summary>
public static class MonthGrid
{
    public const int Days = 42;
    public static IReadOnlyList<DateOnly> For(int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        var back = ((int)first.DayOfWeek + 6) % 7;
        return Enumerable.Range(0, Days).Select(i => first.AddDays(i - back)).ToArray();
    }
}

/// <summary>Which characters of a clock roll when it changes. Text after the second colon (the seconds) may stay still.</summary>
public static class ClockRoll
{
    public static IReadOnlySet<int> Rolling(string before, string after, bool seconds)
    {
        var result = new HashSet<int>(); if (before.Length != after.Length) return result;
        var first = after.IndexOf(':'); var tail = first < 0 ? -1 : after.IndexOf(':', first + 1);
        for (var i = 0; i < after.Length; i++) if (before[i] != after[i] && char.IsAsciiDigit(after[i]) && (seconds || tail < 0 || i < tail)) result.Add(i);
        return result;
    }
}
/// <summary>The dock's hover wave: the icon under the pointer grows past the chosen magnification and rises a little; its neighbours follow more gently.</summary>
public static class DockHover
{
    public const double ExtraGrowth = .14, MaximumScale = 2, Rise = 7;
    /// <param name="slots">Distance from the pointer in icon widths.</param>
    public static (double Scale, double Lift) At(double slots, double magnification)
    {
        var wave = Math.Exp(-slots * slots * .9);
        return (Math.Min(MaximumScale, 1 + (magnification - 1 + ExtraGrowth) * wave), Rise * Math.Exp(-slots * slots * 2.2));
    }
}

/// <summary>A file Windows lists under recent items, and which of them belong to an app.</summary>
public sealed record RecentFile(string Path, string Name, DateTime When);
public static class RecentMatch
{
    /// <summary>Recent files whose type opens with <paramref name="executable"/>; <paramref name="opener"/> gives the app for an extension, or null.</summary>
    public static IReadOnlyList<RecentFile> ForApp(IEnumerable<RecentFile> files, string executable, Func<string, string?> opener, int limit = 8)
    {
        var name = Path.GetFileName(executable);
        return files.Where(f => opener(Path.GetExtension(f.Path)) is { } app && string.Equals(Path.GetFileName(app), name, StringComparison.OrdinalIgnoreCase)).Take(limit).ToArray();
    }
}
