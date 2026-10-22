using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaxeDesktop.Core;
public sealed record ThemePalette(string Id, string Name, string Canvas, string Surface, string Card, string Accent, string Secondary, string Text, string Muted, string Line, string Hover, string Selection);
public static class ThemePresets
{
    public static IReadOnlyList<ThemePalette> All { get; } = new[]
    {
        new ThemePalette("midnight-claxe", "Midnight Noxelyra", "#0C0B13", "#191625", "#242034", "#A884FF", "#7FDBF2", "#F3EFFA", "#B9B0CC", "#514266", "#403154", "#553D7C"),
        new ThemePalette("tempest", "Tempest", "#090F19", "#122133", "#1B2D43", "#64BFFF", "#6FE5C2", "#EDF6FF", "#ADBED4", "#365775", "#203E59", "#28557B"),
        new ThemePalette("sakura", "Sakura", "#170D17", "#281A28", "#382338", "#F0A0CE", "#C0ACFF", "#FFF0F8", "#CCB2C6", "#754660", "#553148", "#75425E")
    };
    public static bool IsColor(string value) => value.Length == 7 && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);
    public static ThemePalette Resolve(Settings settings)
    {
        var palette = All.Single(t => t.Id == settings.ThemeId);
        return palette with { Accent = settings.PrimaryAccent ?? palette.Accent, Secondary = settings.SecondaryAccent ?? palette.Secondary };
    }
}
public sealed record ThemeConfiguration
{
    public string Kind { get; init; } = "ClaxeDesktop.Theme";
    public int SchemaVersion { get; init; } = 1;
    public string ThemeId { get; init; } = "midnight-claxe";
    public string Material { get; init; } = "glass";
    public string? PrimaryAccent { get; init; }
    public string? SecondaryAccent { get; init; }
    public double Opacity { get; init; } = .82;
    public int CornerRadius { get; init; } = 16;
    public double BorderWidth { get; init; } = 1;
    public static ThemeConfiguration From(Settings s) => new() { ThemeId = s.ThemeId, Material = s.Material, PrimaryAccent = s.PrimaryAccent, SecondaryAccent = s.SecondaryAccent, Opacity = s.Opacity, CornerRadius = s.CornerRadius, BorderWidth = s.BorderWidth };
    public Settings ApplyTo(Settings current)
    {
        if (Kind != "ClaxeDesktop.Theme" || SchemaVersion != 1) throw new InvalidDataException("Bu dosya desteklenen bir Noxelyra teması değil.");
        var result = current with { ThemeId = ThemeId, Material = Material, PrimaryAccent = PrimaryAccent, SecondaryAccent = SecondaryAccent, Opacity = Opacity, CornerRadius = CornerRadius, BorderWidth = BorderWidth };
        result.Validate(); return result;
    }
}
public static class ThemeFiles
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 8 };
    public static ThemeConfiguration Parse(string content)
    {
        if (content.Length > 32768) throw new InvalidDataException("Tema dosyası 32 KB sınırını aşıyor.");
        try
        {
            using var document = JsonDocument.Parse(content, new JsonDocumentOptions { MaxDepth = 8 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Tema nesnesi bekleniyor.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject()) if (!names.Add(property.Name)) throw new InvalidDataException("Tekrarlanan tema alanı.");
            if (!names.Contains("Kind") || !names.Contains("SchemaVersion") || !names.Contains("ThemeId")) throw new InvalidDataException("Tema kimliği ve sürümü eksik.");
            var theme = JsonSerializer.Deserialize<ThemeConfiguration>(content, Json) ?? throw new InvalidDataException("Tema boş.");
            _ = theme.ApplyTo(new()); return theme;
        }
        catch (JsonException) { throw new InvalidDataException("Tema JSON'u veya alanları geçersiz."); }
    }
    public static string Serialize(Settings settings) { settings.Validate(); return JsonSerializer.Serialize(ThemeConfiguration.From(settings), Json); }
}
