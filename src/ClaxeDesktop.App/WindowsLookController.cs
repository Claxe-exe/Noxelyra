using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

public sealed partial class DesktopController
{
    private readonly WindowsLookService lookService = new();
    private WindowsLookSession? lookSession;
    private WindowsLookSession Look => lookSession ??= new(lookService, Path.Combine(desktopData, "windows-look.json"));
    internal bool WindowsDark => WindowsLook.IsDark(lookService);
    internal bool WindowsTransparency => WindowsLook.HasTransparency(lookService);
    internal bool WindowsLookChanged { get { try { return Look.HasChanges; } catch (InvalidDataException) { return false; } } }
    internal string SetWindowsDark(bool dark) => ChangeWindowsLook(WindowsLook.Dark(dark), dark ? "Windows koyu moda alındı." : "Windows açık moda alındı.");
    internal string SetWindowsTransparency(bool enabled) => ChangeWindowsLook(WindowsLook.Transparency(enabled), enabled ? "Windows saydamlık efektleri açıldı." : "Windows saydamlık efektleri kapatıldı.");
    internal string MatchWindowsAccent() => ChangeWindowsLook(WindowsLook.AccentFrom(ThemePresets.Resolve(ViewModel.Settings).Accent), "Windows vurgu rengi " + ViewModel.ThemeName + " paletine eşlendi. Bazı yerler oturumu kapatıp açınca güncellenir.");
    private string ChangeWindowsLook(IReadOnlyList<LookValue> values, string done)
    {
        if (safe || ReadOnly) return "Güvenli/salt okunur modda Windows görünümü değiştirilmez.";
        try { Look.Apply(values); Log.Write("windows-look-applied"); return done; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidDataException or InvalidOperationException) { Log.Write("windows-look-failed", ex); return "Windows görünümü değiştirilemedi: " + ex.Message; }
    }
    internal string RestoreWindowsLook()
    {
        try { Look.Restore(); Log.Write("windows-look-restored"); return "Windows görünümü Noxelyra'dan önceki haline döndü."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidDataException) { Log.Write("windows-look-restore-failed", ex); return "Windows görünümü geri alınamadı: " + ex.Message; }
    }
}
