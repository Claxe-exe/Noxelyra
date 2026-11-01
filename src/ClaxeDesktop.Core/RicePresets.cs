namespace ClaxeDesktop.Core;

/// <summary>A ready-made look: colours, surface, corners and sizes together. It contains no images, so nothing here needs a licence.</summary>
public sealed record RicePreset(string Id, string Name, string Description, string ThemeId, string Material, int CornerRadius, double Opacity, double BorderWidth, int IconSize, double Magnification, int PanelHeight)
{
    public ThemePalette Palette => ThemePresets.All.Single(t => t.Id == ThemeId);
    public Settings ApplyTo(Settings current) => current with { ThemeId = ThemeId, Material = Material, CornerRadius = CornerRadius, Opacity = Opacity, BorderWidth = BorderWidth, IconSize = IconSize, Magnification = Magnification, PanelHeight = PanelHeight, PrimaryAccent = null, SecondaryAccent = null };
    public bool Matches(Settings settings) => settings.ThemeId == ThemeId && settings.Material == Material && settings.CornerRadius == CornerRadius && settings.IconSize == IconSize && settings.PanelHeight == PanelHeight;
}
public static class RicePresets
{
    public static IReadOnlyList<RicePreset> All { get; } =
    [
        new("gece-cami", "Gece Camı", "Noxelyra'nın kendisi: mor cam, yumuşak köşeler", "midnight-claxe", "glass", 16, .82, 1, 48, 1.16, 48),
        new("yumusak-mocha", "Yumuşak Mocha", "Pastel Catppuccin, büyük köşeler, belirgin dock dalgası", "catppuccin-mocha", "glass", 22, .80, 1, 52, 1.24, 48),
        new("tokyo-keskin", "Tokyo Keskin", "Tokyo Night, düz yüzey, az köşe, sıkı boyutlar", "tokyo-night", "flat", 6, .96, 1, 44, 1.08, 44),
        new("sakura-aksami", "Sakura Akşamı", "Pembe tonlar, yuvarlak ve havadar", "sakura", "glass", 24, .78, 1, 52, 1.22, 50),
        new("kutup-sade", "Kutup Sade", "Nord renkleri, dalgasız dock, dikkat dağıtmaz", "nord", "flat", 10, .94, 1, 44, 1, 44),
        new("retro-gruvbox", "Retro Gruvbox", "Sıcak tonlar, kalın kenar, neredeyse köşesiz", "gruvbox-dark", "flat", 4, .98, 2, 46, 1.1, 46),
        new("neon-dracula", "Neon Dracula", "Dracula renkleri, parlayan kenarlar", "dracula", "neon", 14, .86, 1, 48, 1.2, 48),
        new("orman", "Orman", "Everforest yeşilleri, akrilik yüzey", "everforest-dark", "acrylic", 18, .84, 1, 48, 1.14, 48),
    ];
}
