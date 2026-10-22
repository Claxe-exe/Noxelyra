using System.Text.Json;
namespace ClaxeDesktop.Core;

/// <summary>One per-user (HKCU) personalization value. <see cref="Exists"/> false means the value was absent and is deleted on restore.</summary>
public sealed record LookValue(string Key, string Name, bool Exists, uint Dword = 0, byte[]? Binary = null)
{
    public string Id => Key + "\\" + Name;
    public bool SameAs(LookValue other) => Id == other.Id && Exists == other.Exists && Dword == other.Dword && (Binary ?? []).SequenceEqual(other.Binary ?? []);
}

public interface IWindowsLookService
{
    LookValue Read(string key, string name);
    void Write(LookValue value);
    void Broadcast();
}

/// <summary>Windows dark mode, transparency and accent colour, expressed as the HKCU values the Settings app itself writes.</summary>
public static class WindowsLook
{
    public const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    public const string Accent = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    public const string Dwm = @"Software\Microsoft\Windows\DWM";
    public static IReadOnlyList<LookValue> Dark(bool dark) => [new(Personalize, "AppsUseLightTheme", true, dark ? 0u : 1u), new(Personalize, "SystemUsesLightTheme", true, dark ? 0u : 1u)];
    public static IReadOnlyList<LookValue> Transparency(bool enabled) => [new(Personalize, "EnableTransparency", true, enabled ? 1u : 0u)];
    // Accent keys are not a documented API; the caller labels this experimental.
    public static IReadOnlyList<LookValue> AccentFrom(string hex)
    {
        var palette = Palette(hex);
        return
        [
            new(Accent, "AccentPalette", true, Binary: palette),
            new(Accent, "AccentColorMenu", true, Abgr(hex)),
            new(Accent, "StartColorMenu", true, Abgr(Shade(hex, -.35))),
            new(Dwm, "AccentColor", true, Abgr(hex)),
            new(Dwm, "ColorizationColor", true, 0xC4000000u | Rgb(hex)),
            new(Dwm, "ColorizationAfterglow", true, 0xC4000000u | Rgb(hex)),
        ];
    }
    public static IReadOnlyList<(string Key, string Name)> Keys(IEnumerable<LookValue> values) => values.Select(v => (v.Key, v.Name)).ToList();
    public static bool IsDark(IWindowsLookService service) => Dark(true).All(v => service.Read(v.Key, v.Name) is { Exists: true, Dword: 0 });
    public static bool HasTransparency(IWindowsLookService service) => service.Read(Personalize, "EnableTransparency") is not { Exists: true, Dword: 0 };
    /// <summary>Windows stores colours as 0xAABBGGRR.</summary>
    public static uint Abgr(string hex) { var (r, g, b) = Parse(hex); return 0xFF000000u | (uint)b << 16 | (uint)g << 8 | r; }
    private static uint Rgb(string hex) { var (r, g, b) = Parse(hex); return (uint)r << 16 | (uint)g << 8 | b; }
    /// <summary>Eight RGBA shades from light to dark, the layout Windows uses for AccentPalette.</summary>
    public static byte[] Palette(string hex)
    {
        var result = new List<byte>(32);
        foreach (var amount in new[] { .6, .4, .2, 0, -.2, -.4, -.6, -.8 }) { var (r, g, b) = Parse(Shade(hex, amount)); result.AddRange([r, g, b, 0]); }
        return result.ToArray();
    }
    public static string Shade(string hex, double amount)
    {
        var (r, g, b) = Parse(hex);
        byte Mix(byte c) => (byte)Math.Round(amount >= 0 ? c + (255 - c) * amount : c * (1 + amount));
        return $"#{Mix(r):X2}{Mix(g):X2}{Mix(b):X2}";
    }
    private static (byte R, byte G, byte B) Parse(string hex)
    {
        if (!ThemePresets.IsColor(hex)) throw new ArgumentException("Renk #RRGGBB biçiminde olmalı.");
        return (Convert.ToByte(hex[1..3], 16), Convert.ToByte(hex[3..5], 16), Convert.ToByte(hex[5..7], 16));
    }
}

/// <summary>Remembers each value's state before Noxelyra first changed it, so "restore" returns to the user's own Windows look.</summary>
public sealed class WindowsLookSession(IWindowsLookService service, string journal)
{
    private static readonly HashSet<string> Allowed = [WindowsLook.Personalize, WindowsLook.Accent, WindowsLook.Dwm];
    public bool HasChanges => Load().Count > 0;
    public void Apply(IReadOnlyList<LookValue> values)
    {
        if (values.Any(v => !Allowed.Contains(v.Key) || !v.Exists)) throw new InvalidOperationException("Yalnız Noxelyra'nın tanıdığı görünüm değerleri yazılabilir.");
        var originals = Load();
        foreach (var value in values) if (!originals.Any(o => o.Id == value.Id)) originals.Add(service.Read(value.Key, value.Name));
        Save(originals);
        foreach (var value in values) service.Write(value);
        service.Broadcast();
    }
    public void Restore()
    {
        var originals = Load();
        foreach (var value in originals) service.Write(value);
        service.Broadcast();
        if (File.Exists(journal)) File.Delete(journal);
    }
    private List<LookValue> Load()
    {
        if (!File.Exists(journal)) return [];
        if (new FileInfo(journal).Length > 16384) throw new InvalidDataException("Windows görünüm kaydı çok büyük.");
        List<LookValue> values;
        try { values = JsonSerializer.Deserialize<List<LookValue>>(File.ReadAllText(journal)) ?? throw new InvalidDataException("Windows görünüm kaydı boş."); }
        catch (JsonException) { throw new InvalidDataException("Windows görünüm kaydı okunamadı."); }
        if (values.Count > 32 || values.Any(v => v is null || !Allowed.Contains(v.Key) || string.IsNullOrWhiteSpace(v.Name) || v.Name.Length > 64 || (v.Binary?.Length ?? 0) > 64) || values.Select(v => v.Id).Distinct().Count() != values.Count)
            throw new InvalidDataException("Windows görünüm kaydı geçersiz.");
        return values;
    }
    private void Save(List<LookValue> values)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(journal)!);
        var temporary = journal + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)) { JsonSerializer.Serialize(stream, values); stream.Flush(true); }
        File.Move(temporary, journal, true);
    }
}
