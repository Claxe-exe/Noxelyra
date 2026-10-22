namespace ClaxeDesktop.Core;

public sealed class WebWidgetPackages(string root)
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".html", ".css", ".js", ".json", ".png", ".jpg", ".jpeg", ".webp", ".woff2", ".txt" };
    public string DirectoryFor(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("Paket kimliği geçersiz.");
        return Path.Combine(root, id);
    }
    public WebWidgetManifest Read(string id) => WebWidgetManifest.Parse(File.ReadAllText(WebWidgetPolicy.ResolveFile(DirectoryFor(id), "widget.json")));
    public (string Id, WebWidgetManifest Manifest) Import(string manifestPath)
    {
        var source = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        if(source.StartsWith(@"\\") || new DriveInfo(Path.GetPathRoot(source)!).DriveType == DriveType.Network) throw new InvalidDataException("Paket yerel diskte olmalı.");
        if (Path.GetFileName(manifestPath) != "widget.json") throw new InvalidDataException("widget.json dosyasını seç.");
        if(new FileInfo(manifestPath).Length > 16384) throw new InvalidDataException("Widget manifesti çok büyük.");
        var manifest = WebWidgetManifest.Parse(File.ReadAllText(WebWidgetPolicy.ResolveFile(source, "widget.json")));
        var files = new List<(string Source, string Relative)>(); long total = 0;
        var directories = 0;
        void Scan(string directory)
        {
            if(++directories > 200 || Path.GetRelativePath(source,directory).Split(Path.DirectorySeparatorChar).Length > 12) throw new InvalidDataException("Paket klasör sınırını aşıyor.");
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Paket bağlantı içeremez.");
                if (Directory.Exists(entry)) { Scan(entry); continue; }
                var relative = Path.GetRelativePath(source, entry).Replace('\\', '/');
                if (!WebWidgetManifest.IsRelativePath(relative) || !Extensions.Contains(Path.GetExtension(entry))) throw new InvalidDataException("Paket desteklenmeyen dosya içeriyor.");
                var length = new FileInfo(entry).Length; total += length;
                if (length > 4 * 1024 * 1024 || total > 16 * 1024 * 1024 || files.Count >= 200) throw new InvalidDataException("Paket limiti: 200 dosya, dosya başına 4 MB, toplam 16 MB.");
                files.Add((entry, relative));
            }
        }
        Scan(source);
        if (!files.Any(f => f.Relative == manifest.Entry)) throw new InvalidDataException("Giriş dosyası bulunamadı.");
        var id = Guid.NewGuid().ToString("N"); var destination = DirectoryFor(id);
        try
        {
            foreach (var file in files)
            {
                var target = Path.Combine(destination, file.Relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file.Source, target);
            }
            return (id, manifest);
        }
        catch { if (Directory.Exists(destination)) Directory.Delete(destination, true); throw; }
    }
}
