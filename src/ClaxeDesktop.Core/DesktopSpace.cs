namespace ClaxeDesktop.Core;

public sealed record DesktopIconPosition(string Path, double X, double Y);
public sealed record DesktopSpaceOptions
{
    public bool Enabled { get; init; }
    public bool FollowDesktop { get; init; }
    public string? MonitorId { get; init; }
    public string? VideoPath { get; init; }
    public string? ImagePath { get; init; }
    public bool ShowIcons { get; init; } = true;
    public bool CaptureShowDesktop { get; init; }
    public bool HideTaskbarCompletely { get; init; }
    public string? IntroMediaPath { get; init; }
    public string IntroTransition { get; init; } = "tv";
    public int IntroFadeMs { get; init; } = 1500;
    public string? EnginePath { get; init; }
    public List<DesktopIconPosition> Icons { get; init; } = [];
    public void Validate()
    {
        if (MonitorId?.Length > 512 || Icons is null || Icons.Count > 300 || Icons.Any(i => i is null || !Local(i.Path) || !double.IsFinite(i.X) || !double.IsFinite(i.Y) || i.X is < 0 or > 100000 || i.Y is < 0 or > 100000) || Icons.Select(i => i.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Icons.Count)
            throw new InvalidDataException("Masaüstü alanı yerleşimi geçersiz.");
        if (VideoPath is not null && (!Local(VideoPath) || !new[] { ".mp4", ".wmv", ".m4v" }.Contains(Path.GetExtension(VideoPath), StringComparer.OrdinalIgnoreCase))) throw new InvalidDataException("Yerel MP4, M4V veya WMV seç.");
        if (ImagePath is not null && (!Local(ImagePath) || !ImageExtensions.Contains(Path.GetExtension(ImagePath), StringComparer.OrdinalIgnoreCase))) throw new InvalidDataException("Yerel PNG, JPG veya BMP görsel seç.");
        if (IntroMediaPath is not null && (!Local(IntroMediaPath) || !IntroExtensions.Contains(Path.GetExtension(IntroMediaPath), StringComparer.OrdinalIgnoreCase))) throw new InvalidDataException("Açılış için yerel MP4, M4V, WMV veya GIF seç.");
        if (IntroTransition is not ("tv" or "fade" or "fade-tv")) throw new InvalidDataException("Açılış geçişi geçersiz.");
        if (IntroFadeMs is < 300 or > 6000) throw new InvalidDataException("Kararma süresi 0,3 ile 6 saniye arasında olmalı.");
        if (EnginePath is not null && (!Local(EnginePath) || Path.GetFileName(EnginePath).ToLowerInvariant() is not ("wallpaper32.exe" or "wallpaper64.exe"))) throw new InvalidDataException("Wallpaper Engine'in wallpaper32.exe veya wallpaper64.exe dosyasını seç.");
    }
    public static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".bmp"];
    public static readonly string[] IntroExtensions = [".mp4", ".m4v", ".wmv", ".gif"];
    public const double IntroMaxSeconds = 10;
    private static bool Local(string path) => path.Length <= 4096 && Path.IsPathFullyQualified(path) && !path.StartsWith(@"\\") && !path.StartsWith(@"\?\");
}
