using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace ClaxeDesktop.Core;

/// <summary>The non-theme parts of a look: bar, dock, motion, launcher and tiling gap.</summary>
public sealed record RiceLook
{
    public int PanelHeight { get; init; } = 48;
    public int PanelWidth { get; init; } = 960;
    public string ClockFormat { get; init; } = "24h";
    public bool ShowSeconds { get; init; }
    public bool ClockOnLeft { get; init; }
    public bool DynamicIsland { get; init; } = true;
    public int IconSize { get; init; } = 48;
    public double Magnification { get; init; } = 1.16;
    public string DockEdge { get; init; } = "floating";
    public bool DockAutoHide { get; init; }
    public bool HoverAnimation { get; init; } = true;
    public bool AnimationsEnabled { get; init; } = true;
    public int AnimationDurationMs { get; init; } = 230;
    public string LauncherStyle { get; init; } = "center";
    public int TilingGap { get; init; } = 10;
}

/// <summary>
/// A shareable look ("rice"): theme, look, widget layout and optionally the Noxelyra desktop image and intro clip, in one .noxrice zip.
/// Personal content stays out: note text, weather city, web widget packages, shortcuts, hotkeys and Windows tweaks are never exported.
/// </summary>
public sealed record RiceManifest
{
    public string Kind { get; init; } = RiceFiles.KindName;
    public int SchemaVersion { get; init; } = 1;
    public string Name { get; init; } = "Noxelyra rice";
    public DateTimeOffset Created { get; init; }
    public ThemeConfiguration Theme { get; init; } = new();
    public RiceLook Look { get; init; } = new();
    public List<WidgetInstance> Widgets { get; init; } = [];
    /// <summary>Entry name of the bundled desktop image, or null.</summary>
    public string? Wallpaper { get; init; }
    /// <summary>Entry name of the bundled intro clip, or null.</summary>
    public string? Intro { get; init; }
}

public static class RiceFiles
{
    public const string KindName = "Noxelyra.Rice", ManifestEntry = "rice.json", Extension = ".noxrice";
    public const long MaxManifest = 128 * 1024, MaxWallpaper = 25L * 1024 * 1024, MaxIntro = 40L * 1024 * 1024, MaxTotal = 70L * 1024 * 1024;
    public static readonly string[] WallpaperTypes = [".png", ".jpg", ".jpeg", ".bmp"];
    public static readonly string[] IntroTypes = [".mp4", ".m4v", ".wmv", ".gif"];
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 16 };

    /// <summary>A shareable rice. <paramref name="personal"/> keeps note text and the weather city, for local backups only.</summary>
    public static RiceManifest Capture(Settings settings, string name, DateTimeOffset now, bool wallpaper, string? wallpaperFile, bool intro, string? introFile, bool personal = false)
    {
        settings.Validate();
        var clean = new string((name ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        return new()
        {
            Name = clean.Length is 0 or > 60 ? "Noxelyra rice" : clean, Created = now, Theme = ThemeConfiguration.From(settings),
            Look = new()
            {
                PanelHeight = settings.PanelHeight, PanelWidth = settings.PanelWidth, ClockFormat = settings.ClockFormat, ShowSeconds = settings.ShowSeconds, ClockOnLeft = settings.ClockOnLeft,
                DynamicIsland = settings.DynamicIsland, IconSize = settings.IconSize, Magnification = settings.Magnification, DockEdge = settings.DockEdge, DockAutoHide = settings.DockAutoHide,
                HoverAnimation = settings.HoverAnimation, AnimationsEnabled = settings.AnimationsEnabled, AnimationDurationMs = settings.AnimationDurationMs,
                LauncherStyle = settings.Keyboard.LauncherStyle, TilingGap = settings.Keyboard.TilingGap
            },
            // Layout only: personal text is blanked and web widgets (local packages) are left out.
            Widgets = settings.Widgets.Where(w => w.Kind != "web").Select(w => personal ? w : w with { Text = w.Kind == "note" ? "Not" : w.Kind == "weather" ? "" : w.Text, GroupId = null }).ToList(),
            Wallpaper = wallpaper && wallpaperFile is not null ? "wallpaper" + Path.GetExtension(wallpaperFile).ToLowerInvariant() : null,
            Intro = intro && introFile is not null ? "intro" + Path.GetExtension(introFile).ToLowerInvariant() : null
        };
    }
    /// <summary>The settings with the rice applied. Widgets are replaced only when asked; everything personal is kept.</summary>
    public static Settings Apply(RiceManifest rice, Settings current, bool replaceWidgets) { Validate(rice); return Merge(rice, current, replaceWidgets, freshIds: true); }
    public static void Validate(RiceManifest rice)
    {
        if (rice is null || rice.Kind != KindName || rice.SchemaVersion != 1) throw new InvalidDataException("Bu dosya desteklenen bir Noxelyra rice'ı değil.");
        if (string.IsNullOrWhiteSpace(rice.Name) || rice.Name.Length > 60 || rice.Name.Any(char.IsControl)) throw new InvalidDataException("Rice adı geçersiz.");
        if (rice.Theme is null || rice.Look is null || rice.Widgets is null) throw new InvalidDataException("Rice eksik alan içeriyor.");
        if (rice.Theme.Kind != "ClaxeDesktop.Theme" || rice.Theme.SchemaVersion != 1) throw new InvalidDataException("Rice teması desteklenmiyor.");
        if (rice.Widgets.Any(w => w is null || w.Kind == "web")) throw new InvalidDataException("Rice web widget paketi içeremez.");
        if (rice.Wallpaper is not null && !Allowed(rice.Wallpaper, "wallpaper", WallpaperTypes)) throw new InvalidDataException("Rice görsel adı geçersiz.");
        if (rice.Intro is not null && !Allowed(rice.Intro, "intro", IntroTypes)) throw new InvalidDataException("Rice açılış klibi adı geçersiz.");
        // Settings.Validate inside Merge covers every range: panel size, dock edge, launcher style, gap, widgets and the theme itself.
        _ = Merge(rice, new Settings(), replaceWidgets: true, freshIds: false);
    }
    private static Settings Merge(RiceManifest rice, Settings current, bool replaceWidgets, bool freshIds)
    {
        var look = rice.Look;
        var next = rice.Theme.ApplyTo(current) with
        {
            PanelHeight = look.PanelHeight, PanelWidth = look.PanelWidth, ClockFormat = look.ClockFormat, ShowSeconds = look.ShowSeconds, ClockOnLeft = look.ClockOnLeft, DynamicIsland = look.DynamicIsland,
            IconSize = look.IconSize, Magnification = look.Magnification, DockEdge = look.DockEdge, DockAutoHide = look.DockAutoHide, HoverAnimation = look.HoverAnimation,
            AnimationsEnabled = look.AnimationsEnabled, AnimationDurationMs = look.AnimationDurationMs,
            Keyboard = current.Keyboard with { LauncherStyle = look.LauncherStyle, TilingGap = look.TilingGap }
        };
        if (replaceWidgets) next = next with { Widgets = rice.Widgets.Select(w => freshIds ? w with { Id = Guid.NewGuid().ToString("N") } : w).ToList() };
        next.Validate(); return next;
    }
    private static bool Allowed(string entry, string stem, string[] types) => entry.Length < 32 && Path.GetFileNameWithoutExtension(entry) == stem && types.Contains(Path.GetExtension(entry)) && entry == Path.GetFileName(entry);
    public static string Serialize(RiceManifest rice) { Validate(rice); return JsonSerializer.Serialize(rice, Json); }
    public static RiceManifest Parse(string json)
    {
        if (json.Length > MaxManifest) throw new InvalidDataException("rice.json çok büyük.");
        try
        {
            using (var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 }))
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Rice nesnesi bekleniyor.");
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in document.RootElement.EnumerateObject()) if (!names.Add(property.Name)) throw new InvalidDataException("Tekrarlanan rice alanı.");
            }
            var rice = JsonSerializer.Deserialize<RiceManifest>(json, Json) ?? throw new InvalidDataException("Rice boş.");
            Validate(rice); return rice;
        }
        catch (JsonException) { throw new InvalidDataException("rice.json okunamadı veya bilinmeyen alan içeriyor."); }
    }
    /// <summary>Writes the zip: rice.json plus the optional media, copied as they are.</summary>
    public static void Write(string path, RiceManifest rice, string? wallpaperFile, string? introFile)
    {
        var json = Serialize(rice);
        if (rice.Wallpaper is not null && (wallpaperFile is null || new FileInfo(wallpaperFile).Length > MaxWallpaper)) throw new InvalidDataException("Masaüstü görseli bulunamadı veya 25 MB'tan büyük.");
        if (rice.Intro is not null && (introFile is null || new FileInfo(introFile).Length > MaxIntro)) throw new InvalidDataException("Açılış klibi bulunamadı veya 40 MB'tan büyük.");
        var temporary = path + ".tmp";
        using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(zip.CreateEntry(ManifestEntry, CompressionLevel.Optimal).Open())) writer.Write(json);
            if (rice.Wallpaper is not null) zip.CreateEntryFromFile(wallpaperFile!, rice.Wallpaper, CompressionLevel.NoCompression);
            if (rice.Intro is not null) zip.CreateEntryFromFile(introFile!, rice.Intro, CompressionLevel.NoCompression);
        }
        File.Move(temporary, path, true);
    }
    /// <summary>
    /// Reads and checks a .noxrice. Media is extracted into <paramref name="extractTo"/> (a fresh folder) under fixed names only;
    /// unknown entries, paths, oversize or zip-bomb style entries are rejected before anything is written.
    /// </summary>
    public static (RiceManifest Rice, string? Wallpaper, string? Intro) Read(string path, string extractTo)
    {
        if (new FileInfo(path).Length > MaxTotal) throw new InvalidDataException("Rice dosyası 70 MB sınırını aşıyor.");
        using var zip = ZipFile.OpenRead(path);
        if (zip.Entries.Count is 0 or > 3) throw new InvalidDataException("Rice dosyasında beklenmeyen içerik var.");
        var byName = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName != entry.Name || entry.Name.Length is 0 or > 31 || !byName.TryAdd(entry.Name, entry)) throw new InvalidDataException("Rice dosyasında klasör veya tekrarlanan dosya olamaz.");
            if (entry.Length > MaxIntro || entry.Length < 0) throw new InvalidDataException("Rice içindeki bir dosya çok büyük.");
        }
        if (!byName.TryGetValue(ManifestEntry, out var manifest) || manifest.Length > MaxManifest) throw new InvalidDataException("rice.json eksik veya çok büyük.");
        string json; using (var reader = new StreamReader(manifest.Open())) json = reader.ReadToEnd();
        var rice = Parse(json);
        var expected = new[] { ManifestEntry, rice.Wallpaper, rice.Intro }.Where(n => n is not null).ToHashSet();
        if (byName.Keys.Any(n => !expected.Contains(n)) || expected.Any(n => !byName.ContainsKey(n!))) throw new InvalidDataException("Rice içeriği rice.json ile uyuşmuyor.");
        if (rice.Wallpaper is { } w && byName[w].Length > MaxWallpaper) throw new InvalidDataException("Rice görseli 25 MB'tan büyük.");
        Directory.CreateDirectory(extractTo);
        string? Extract(string? name)
        {
            if (name is null) return null;
            var target = Path.Combine(extractTo, name);
            using (var source = byName[name].Open()) using (var output = File.Create(target)) { CopyBounded(source, output, byName[name].Length); }
            return target;
        }
        return (rice, Extract(rice.Wallpaper), Extract(rice.Intro));
    }
    // Stops at the declared size even if the entry header lies about it.
    private static void CopyBounded(Stream source, Stream target, long limit)
    {
        var buffer = new byte[81920]; long total = 0; int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read; if (total > limit) throw new InvalidDataException("Rice içindeki dosya bildirilen boyuttan büyük.");
            target.Write(buffer, 0, read);
        }
    }
}
