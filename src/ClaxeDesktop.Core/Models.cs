namespace ClaxeDesktop.Core;

public sealed record Placement(string MonitorId, double X, double Y);
public sealed record Shortcut(string Name, string Target);
public sealed record Settings
{
    public int SchemaVersion { get; init; } = 3;
    public bool IntegratedDesktop { get; init; }
    public DesktopSpaceOptions DesktopSpace { get; init; } = new();
    public string? ActiveProfileId {get;init;}
    public DateTimeOffset ProfileManualUntil {get;init;}
    public bool LayoutShortcut {get;init;}
    public MascotOptions Mascot {get;init;}=new();
    public KeyboardOptions Keyboard { get; init; } = new();
    public bool HideWindowsTaskbar { get; init; }
    public bool GlobalShortcuts { get; init; }
    public bool WallpaperEnabled { get; init; }
    public List<WallpaperAssignment> Wallpapers { get; init; } = [];
    public List<WallpaperRotation> WallpaperRotations { get; init; } = [];
    public List<WidgetInstance> Widgets { get; init; } = [WidgetInstance.Default];
    public double Opacity { get; init; } = .82;
    public string ThemeId { get; init; } = "midnight-claxe";
    public string Material { get; init; } = "glass";
    public string? PrimaryAccent { get; init; }
    public string? SecondaryAccent { get; init; }
    public int CornerRadius { get; init; } = 16;
    public double BorderWidth { get; init; } = 1;
    public int GridSize { get; init; } = 12;
    public int DockHideDelayMs { get; init; } = 450;
    public double Magnification { get; init; } = 1.16;
    public bool AnimationsEnabled { get; init; } = true;
    public bool IntroSequence { get; init; } = true;
    public int AnimationDurationMs { get; init; } = 230;
    public string ClockFormat { get; init; } = "24h";
    public int IconSize { get; init; } = 48;
    public int PanelWidth { get; init; } = 960;
    public int PanelHeight { get; init; } = 48;
    public bool ShowSeconds { get; init; }
    public bool SnapToGrid { get; init; } = true;
    public bool FirstRunComplete { get; init; }
    public bool ShowRunningApps { get; init; } = true;
    public bool GroupWindows { get; init; } = true;
    public string DockEdge { get; init; } = "floating";
    public bool DockAutoHide { get; init; }
    public bool DockOnTop { get; init; }
    public bool HoverAnimation { get; init; } = true;
    public bool ClockOnLeft { get; init; }
    public bool DynamicIsland { get; init; } = true;
    /// <summary>Colours generated from a wallpaper, used while <see cref="ThemeId"/> is "wallpaper".</summary>
    public ThemePalette? CustomPalette { get; init; }
    /// <summary>City for the island card's weather; empty means the first weather widget's city, or none.</summary>
    public string IslandCity { get; init; } = "";
    public Placement? Panel { get; init; }
    public Placement? Dock { get; init; }
    public Placement? Widget { get; init; }
    public const int MaxShortcuts = 64;
    public List<Shortcut> Shortcuts { get; init; } = [];
    public void Validate()
    {
        if (SchemaVersion != 3) throw new InvalidDataException("Desteklenmeyen ayar sürümü.");
        WidgetLayout.Validate(Widgets);
        if (DesktopSpace is null) throw new InvalidDataException("Masaüstü alanı tercihi geçersiz."); DesktopSpace.Validate();
        if(ActiveProfileId is not null&&!Guid.TryParseExact(ActiveProfileId,"N",out _))throw new InvalidDataException("Profil kimliği geçersiz.");
        if(Mascot is null)throw new InvalidDataException("Maskot tercihi geçersiz.");Mascot.Validate();
        if (Keyboard is null) throw new InvalidDataException("Klavye tercihi geçersiz."); Keyboard.Validate();
        if (IslandCity is null || IslandCity.Length > OpenMeteo.MaxCityLength || IslandCity.Any(char.IsControl)) throw new InvalidDataException("Ada şehri geçersiz.");
        if(WallpaperRotations is null||WallpaperRotations.Count>32||WallpaperRotations.Any(r=>r is null||string.IsNullOrWhiteSpace(r.MonitorId)||r.IntervalSeconds is <30 or >3600||r.AssetPaths is null||r.AssetPaths.Count is <2 or >20||r.AssetPaths.Any(p=>string.IsNullOrWhiteSpace(p)||!Path.IsPathFullyQualified(p)||p.StartsWith(@"\\")))||WallpaperRotations.Select(r=>r.MonitorId).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=WallpaperRotations.Count)throw new InvalidDataException("Arka plan listesi geçersiz.");
        if(Wallpapers is null || Wallpapers.Count>32 || Wallpapers.Any(w=>w is null || string.IsNullOrWhiteSpace(w.MonitorId) || string.IsNullOrWhiteSpace(w.AssetPath) || !Path.IsPathFullyQualified(w.AssetPath) || w.AssetPath.StartsWith(@"\\")) || Wallpapers.Select(w=>w.MonitorId).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=Wallpapers.Count) throw new InvalidDataException("Arka plan atamaları geçersiz.");
        if (ThemeId == ThemePresets.WallpaperId) { if (CustomPalette is null) throw new InvalidDataException("Duvar kâğıdı paleti eksik."); ThemePresets.ValidateCustom(CustomPalette); }
        if (!ThemePresets.Known(ThemeId) || Material is not ("glass" or "flat" or "neon" or "acrylic")) throw new InvalidDataException("Tema veya yüzey stili geçersiz.");
        if ((PrimaryAccent is not null && !ThemePresets.IsColor(PrimaryAccent)) || (SecondaryAccent is not null && !ThemePresets.IsColor(SecondaryAccent))) throw new InvalidDataException("Renk #RRGGBB biçiminde olmalı.");
        if (CornerRadius is < 0 or > 32 || !double.IsFinite(BorderWidth) || BorderWidth is < 0 or > 4 || GridSize is < 4 or > 64 || DockHideDelayMs is < 0 or > 2500 || !double.IsFinite(Magnification) || Magnification is < 1 or > 2 || AnimationDurationMs is < 0 or > 1200 || ClockFormat is not ("24h" or "12h")) throw new InvalidDataException("Görünüm veya davranış değeri izin verilen aralığın dışında.");
        if (DockEdge is not ("floating" or "top" or "bottom")) throw new InvalidDataException("Dock kenarı geçersiz.");
        if (!double.IsFinite(Opacity) || Opacity is < .35 or > 1 || IconSize is < 24 or > 96 || PanelWidth is < 640 or > 1800 || PanelHeight is < 42 or > 80)
            throw new InvalidDataException("Görünüm değerleri izin verilen aralığın dışında.");
        foreach (var p in new[] { Panel, Dock, Widget })
            if (p is not null && (string.IsNullOrWhiteSpace(p.MonitorId) || !double.IsFinite(p.X) || !double.IsFinite(p.Y) || Math.Abs(p.X) > 100000 || Math.Abs(p.Y) > 100000))
                throw new InvalidDataException("Yerleşim geçersiz.");
        if (Shortcuts is null || Shortcuts.Count > MaxShortcuts || Shortcuts.Any(s => s is null || string.IsNullOrWhiteSpace(s.Name) || s.Name.Length > 80 || !LaunchPolicy.IsAllowed(s.Target)))
            throw new InvalidDataException("Kısayol geçersiz; yerel .exe veya .lnk seçin.");
    }
}

public static class LaunchPolicy
{
    public static bool IsAllowed(string? path) => !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) && !path.StartsWith(@"\\") &&
        new[] { ".exe", ".lnk" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
public sealed record MonitorInfo(string Id, string Name, int X, int Y, int Width, int Height, double Scale, bool Primary)
{
    public override string ToString() => $"{Name} · {Width} × {Height} · %{Scale * 100:0}{(Primary ? " · Ana ekran" : "")}";
}
public sealed record ResolvedPlacement(MonitorInfo Monitor, double X, double Y);
public static class LayoutEngine
{
    public static ResolvedPlacement Resolve(Placement? saved, IReadOnlyList<MonitorInfo> monitors, double width, double height, double defaultX, double defaultY, bool snap, int gridSize = 12)
    {
        if (monitors.Count == 0) throw new InvalidOperationException("Ekran bulunamadı.");
        var monitor = monitors.FirstOrDefault(m => m.Id == saved?.MonitorId) ?? monitors.FirstOrDefault(m => m.Primary) ?? monitors[0];
        double x = saved?.X ?? defaultX, y = saved?.Y ?? defaultY;
        if (gridSize is < 4 or > 64) throw new ArgumentOutOfRangeException(nameof(gridSize));
        if (snap) { x = Math.Round(x / gridSize) * gridSize; y = Math.Round(y / gridSize) * gridSize; }
        return new(monitor, Math.Clamp(x, 0, Math.Max(0, monitor.Width / monitor.Scale - width)), Math.Clamp(y, 0, Math.Max(0, monitor.Height / monitor.Scale - height)));
    }
}
public interface IMonitorService { IReadOnlyList<MonitorInfo> GetMonitors(); }
public sealed record SystemSample(double? Cpu, double? Memory, double? UsedGb, double? TotalGb);
public interface ISystemMetrics { SystemSample Sample(); }
public interface IAppLauncher { void Launch(Shortcut shortcut); }
public interface ISettingsStore { Task<SettingsLoad> LoadAsync(CancellationToken token = default); Task SaveAsync(Settings settings, CancellationToken token = default); }
public sealed record SettingsLoad(Settings Settings, string? Notice = null, bool ReadOnly = false);
