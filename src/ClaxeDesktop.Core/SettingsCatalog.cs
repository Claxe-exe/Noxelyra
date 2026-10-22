using System.Globalization;
using System.Text.Json;

namespace ClaxeDesktop.Core;
public sealed record SettingChoice(string Value, string Label)
{
    public override string ToString() => Label;
}
public sealed record SettingDefinition(string Key, string Label, string Category, string Description, string Kind, double Minimum = 0, double Maximum = 1, double Step = 1, IReadOnlyList<SettingChoice>? Choices = null)
{
    public override string ToString() => Label;
    public bool Matches(string query)
    {
        var culture = CultureInfo.GetCultureInfo("tr-TR").CompareInfo;
        var text = $"{Label} {Description} {Key} {Category}";
        return query.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(part => culture.IndexOf(text, part, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0 || text.Contains(part, StringComparison.OrdinalIgnoreCase));
    }
}
public static class SettingsCatalog
{
    public static IReadOnlyList<SettingDefinition> Supported { get; } = Load();
    private static IReadOnlyList<SettingDefinition> Load()
    {
        using var stream = typeof(SettingsCatalog).Assembly.GetManifestResourceStream("ClaxeDesktop.SettingsCatalog.json") ?? throw new InvalidDataException("Ayar kataloğu eksik.");
        using var document = JsonDocument.Parse(stream);
        var source = document.RootElement.GetProperty("categories").EnumerateArray().SelectMany(c => c.GetProperty("settings").EnumerateArray()).ToDictionary(s => s.GetProperty("key").GetString()!, s => s.Clone());
        SettingDefinition From(string key, string category, string kind, double min = 0, double max = 1, double step = 1, params SettingChoice[] choices)
        {
            var entry = source[key];
            if (entry.TryGetProperty("constraints", out var constraints) && constraints.ValueKind == JsonValueKind.Object && constraints.TryGetProperty("min", out var lower))
            { min = Math.Max(min, lower.GetDouble()); max = Math.Min(max, constraints.GetProperty("max").GetDouble()); }
            var description = key switch
            {
                "themes.activeTheme" => "Hazır renk paletlerinden birini seç.", "themes.panelStyle" => "Saydam, opak veya vurgulu kenarlı yüzeyler.",
                "appearance.accentPrimary" => "Düğmelerin ve seçili öğelerin ana rengi.", "appearance.accentSecondary" => "Bellek göstergesi gibi ikincil vurguların rengi.",
                "appearance.cornerRadius" => "Panel, dock ve saat kartının köşeleri.", "appearance.borderWidth" => "Yüzeyleri çevreleyen çizginin kalınlığı.",
                "dock.position" => "Dock'u serbest bırak veya ekran kenarına bağla.", "dock.iconSizeDip" => "Dock simgelerinin ölçekten bağımsız boyutu.",
                "dock.magnification" => "İmleç simgenin üzerindeyken uygulanacak büyüme.", "dock.showRunningApps" => "Açık uygulamalara dock üzerinden ulaş.",
                "dock.groupWindows" => "Aynı uygulamanın pencerelerini tek simgede topla.", "dock.hideDelayMs" => "İmleç ayrıldıktan sonra gizlenmeden önce beklenecek süre, milisaniye.",
                "dock.alwaysOnTop" => "Normal uygulamaların önünde kalır; tam ekranda geri çekilir.",
                "panels.heightDip" => "Üst panelin ölçekten bağımsız yüksekliği.", "panels.clockFormat" => "Saat 12 veya 24 saat biçiminde gösterilir.",
                "system.clockSeconds" => "Panelde ve saat kartında saniyeyi göster.",
                "animations.enabled" => "Pencere açılış/kapanış, dock ve giriş animasyonlarının tamamını aç veya kapat.", "animations.introSequence" => "Noxelyra açılırken yaklaşık 3 saniyelik giriş; tıklayarak veya Esc ile geçilebilir.", "animations.durationMs" => "Simge büyüme geçişinin süresi, milisaniye.", "animations.hoverEffects" => "İmleç üzerindeyken dock simgeleri büyür.",
                "editor.snapToGrid" => "Taşınan panel, dock ve saat kartını ızgaraya hizala.", "editor.gridSizeDip" => "Yerleşim ızgarasının aralığı; daha büyük değer daha geniş adımlar sağlar.",
                _ => entry.GetProperty("description").GetString()!
            };
            return new(key, entry.GetProperty("label").GetString()!, category, description, kind, min, max, step, choices);
        }
        return new[]
        {
            From("themes.activeTheme", "Görünüm", "choice", choices: ThemePresets.All.Select(t => new SettingChoice(t.Id, t.Name)).ToArray()),
            From("themes.panelStyle", "Görünüm", "choice", choices: [new("glass", "Glass · saydam"), new("flat", "Flat · opak"), new("acrylic", "Gerçek cam · Windows 11, deneysel; yeniden başlatınca"), new("neon", "Neon · vurgulu kenar")]),
            From("appearance.accentPrimary", "Görünüm", "color"), From("appearance.accentSecondary", "Görünüm", "color"),
            From("appearance.surfaceOpacity", "Görünüm", "number", .35, 1, .05),
            From("appearance.cornerRadius", "Görünüm", "number", 0, 32), From("appearance.borderWidth", "Görünüm", "number", 0, 4, .5),
            From("dock.position", "Dock", "choice", choices: [new("floating", "Serbest"), new("top", "Üst çalışma alanı kenarı"), new("bottom", "Alt çalışma alanı kenarı")]),
            From("dock.iconSizeDip", "Dock", "number", 24, 96, 4), From("dock.magnification", "Dock", "number", 1, 2, .02),
            From("dock.showRunningApps", "Dock", "bool"), From("dock.groupWindows", "Dock", "bool"),
            From("dock.autoHide", "Dock", "bool"), From("dock.hideDelayMs", "Dock", "number", 0, 2500, 50), From("dock.alwaysOnTop", "Dock", "bool"),
            new SettingDefinition("panels.widthDip", "Panel genişliği", "Paneller", "Panel genişliğini DIP olarak değiştirir; ekranın kullanılabilir alanında tutulur.", "number", 640, 1800, 20),
            From("panels.heightDip", "Paneller", "number", 42, 80, 2),
            From("panels.clockFormat", "Paneller", "choice", choices: [new("24h", "24 saat"), new("12h", "12 saat")]),
            From("system.clockSeconds", "Paneller", "bool"),
            new SettingDefinition("panels.clockOnLeft", "Saati solda göster", "Paneller", "Panel saatini sol veya sağ alana taşır.", "bool"),
            From("animations.enabled", "Animasyonlar", "bool"), From("animations.introSequence", "Animasyonlar", "bool"), From("animations.durationMs", "Animasyonlar", "number", 0, 1200, 10), From("animations.hoverEffects", "Animasyonlar", "bool"),
            From("editor.snapToGrid", "Yerleşim", "bool"), From("editor.gridSizeDip", "Yerleşim", "number", 4, 64, 2)
        };
    }
    public static object Value(Settings s, string key) => key switch
    {
        "themes.activeTheme" => s.ThemeId, "themes.panelStyle" => s.Material,
        "appearance.accentPrimary" => ThemePresets.Resolve(s).Accent, "appearance.accentSecondary" => ThemePresets.Resolve(s).Secondary,
        "appearance.surfaceOpacity" => s.Opacity, "appearance.cornerRadius" => s.CornerRadius, "appearance.borderWidth" => s.BorderWidth,
        "dock.position" => s.DockEdge, "dock.iconSizeDip" => s.IconSize, "dock.magnification" => s.Magnification,
        "dock.showRunningApps" => s.ShowRunningApps, "dock.groupWindows" => s.GroupWindows, "dock.autoHide" => s.DockAutoHide,
        "dock.hideDelayMs" => s.DockHideDelayMs, "dock.alwaysOnTop" => s.DockOnTop,
        "panels.widthDip" => s.PanelWidth, "panels.heightDip" => s.PanelHeight, "panels.clockFormat" => s.ClockFormat,
        "system.clockSeconds" => s.ShowSeconds, "panels.clockOnLeft" => s.ClockOnLeft,
        "animations.enabled" => s.AnimationsEnabled, "animations.introSequence" => s.IntroSequence, "animations.durationMs" => s.AnimationDurationMs, "animations.hoverEffects" => s.HoverAnimation,
        "editor.snapToGrid" => s.SnapToGrid, "editor.gridSizeDip" => s.GridSize,
        _ => throw new InvalidDataException("Bu ayar henüz desteklenmiyor.")
    };
    public static Settings Apply(Settings settings, string key, object? raw)
    {
        var definition = Supported.SingleOrDefault(s => s.Key == key) ?? throw new InvalidDataException("Bu ayar henüz desteklenmiyor.");
        string Text() => raw as string ?? throw new InvalidDataException("Metin değeri bekleniyor.");
        bool Flag() => raw is bool b ? b : throw new InvalidDataException("Açık veya kapalı değeri bekleniyor.");
        double Number()
        {
            if (raw is not (double or int)) throw new InvalidDataException("Sayısal değer bekleniyor.");
            var value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
            if (!double.IsFinite(value) || value < definition.Minimum || value > definition.Maximum) throw new InvalidDataException($"Değer {definition.Minimum}–{definition.Maximum} aralığında olmalı.");
            return value;
        }
        int Whole() { var value = Number(); if (value != Math.Truncate(value)) throw new InvalidDataException("Tam sayı bekleniyor."); return (int)value; }
        if (definition.Kind == "choice" && !definition.Choices!.Any(c => c.Value == Text())) throw new InvalidDataException("Desteklenen bir seçenek seçin.");
        var next = key switch
        {
            "themes.activeTheme" => settings with { ThemeId = Text(), PrimaryAccent = null, SecondaryAccent = null },
            "themes.panelStyle" => settings with { Material = Text() },
            "appearance.accentPrimary" => settings with { PrimaryAccent = Text().ToUpperInvariant() }, "appearance.accentSecondary" => settings with { SecondaryAccent = Text().ToUpperInvariant() },
            "appearance.surfaceOpacity" => settings with { Opacity = Number() }, "appearance.cornerRadius" => settings with { CornerRadius = Whole() }, "appearance.borderWidth" => settings with { BorderWidth = Number() },
            "dock.position" => settings with { DockEdge = Text() }, "dock.iconSizeDip" => settings with { IconSize = Whole() }, "dock.magnification" => settings with { Magnification = Number() },
            "dock.showRunningApps" => settings with { ShowRunningApps = Flag() }, "dock.groupWindows" => settings with { GroupWindows = Flag() }, "dock.autoHide" => settings with { DockAutoHide = Flag() },
            "dock.hideDelayMs" => settings with { DockHideDelayMs = Whole() }, "dock.alwaysOnTop" => settings with { DockOnTop = Flag() },
            "panels.widthDip" => settings with { PanelWidth = Whole() }, "panels.heightDip" => settings with { PanelHeight = Whole() }, "panels.clockFormat" => settings with { ClockFormat = Text() },
            "system.clockSeconds" => settings with { ShowSeconds = Flag() }, "panels.clockOnLeft" => settings with { ClockOnLeft = Flag() },
            "animations.enabled" => settings with { AnimationsEnabled = Flag() }, "animations.introSequence" => settings with { IntroSequence = Flag() }, "animations.durationMs" => settings with { AnimationDurationMs = Whole() }, "animations.hoverEffects" => settings with { HoverAnimation = Flag() },
            "editor.snapToGrid" => settings with { SnapToGrid = Flag() }, "editor.gridSizeDip" => settings with { GridSize = Whole() },
            _ => throw new InvalidDataException("Ayar desteklenmiyor.")
        };
        next.Validate(); return next;
    }
    public static Settings Reset(Settings current, IEnumerable<string> keys)
    {
        var defaults = new Settings(); var result = current;
        foreach (var key in keys.Distinct()) result = Apply(result, key, Value(defaults, key));
        return result;
    }
}
