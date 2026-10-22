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
    private WindowsLookSession? lightenSession;
    private WindowsLookSession Lightening => lightenSession ??= new(lookService, Path.Combine(desktopData, "lighten.json"), Lighten.AllowedKeys);
    private LightenWindow? lightenWindow;
    public void ShowLighten()
    {
        if (lightenWindow is null) { lightenWindow = new(this); lightenWindow.Closed += (_, _) => lightenWindow = null; lightenWindow.Show(); }
        lightenWindow.Activate();
    }
    internal bool PhoneInBackground => lookService.Read(Lighten.Background + @"\" + Lighten.CrossDevice, "Disabled") is not { Exists: true, Dword: 1 };
    internal bool WebSearchInStart => lookService.Read(Lighten.ExplorerPolicy, "DisableSearchBoxSuggestions") is not { Exists: true, Dword: 1 };
    internal bool LightenChanged { get { try { return Lightening.HasChanges; } catch (InvalidDataException) { return false; } } }
    internal string SetStartup(StartupEntry entry, bool enabled) => ChangeLighten([Lighten.Startup(entry, enabled, DateTime.UtcNow)], entry.Name + (enabled ? " açılışta başlayacak." : " artık açılışta başlamayacak."));
    internal string SetPhoneInBackground(bool allowed) => ChangeLighten(Lighten.PhoneInBackground(allowed), allowed ? "Telefon Bağlantısı arka planda çalışabilir." : "Telefon Bağlantısı arka plan izni kapatıldı; etkisi oturum yenilenince tamamlanabilir.");
    internal string SetWebSearch(bool enabled) => ChangeLighten(Lighten.WebSearch(enabled), enabled ? "Başlat aramasında web önerileri açıldı." : "Başlat aramasında web önerileri kapatıldı; Explorer yeniden başlayınca veya oturum yenilenince uygulanır.");
    private string ChangeLighten(IReadOnlyList<LookValue> values, string done)
    {
        if (safe || ReadOnly) return "Güvenli/salt okunur modda Windows değiştirilmez.";
        try { Lightening.Apply(values); Log.Write("lighten-applied"); return done; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidDataException or InvalidOperationException) { Log.Write("lighten-failed", ex); return "Değiştirilemedi: " + ex.Message; }
    }
    internal string RestoreLighten()
    {
        try { Lightening.Restore(); Log.Write("lighten-restored"); return "Hafifletme değişiklikleri Noxelyra'dan önceki haline döndü."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidDataException) { Log.Write("lighten-restore-failed", ex); return "Geri alınamadı: " + ex.Message; }
    }
    private readonly WindowsServiceControl serviceControl = new();
    private string ServiceJournal => Path.Combine(desktopData, ServiceTweaks.JournalName);
    internal ServiceState? ReadService(string name) { try { return serviceControl.Read(name); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or System.Security.SecurityException) { return null; } }
    internal bool ServicesChanged { get { try { return new ServiceSession(serviceControl, ServiceJournal).HasChanges; } catch (InvalidDataException) { return true; } } }
    internal Task<string> DisableServicesAsync(IReadOnlyList<string> names) => RunServiceHelperAsync(["apply", ServiceJournal, .. names], "Seçilen servisler kapatıldı.");
    internal Task<string> RestoreServicesAsync() => RunServiceHelperAsync(["restore", ServiceJournal], "Servisler Noxelyra'dan önceki haline döndü.");
    private async Task<string> RunServiceHelperAsync(IReadOnlyList<string> arguments, string done)
    {
        if (safe || ReadOnly) return "Güvenli/salt okunur modda servis değiştirilmez.";
        var info = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas", Arguments = "--services " + string.Join(' ', arguments.Select(a => "\"" + a + "\"")) };
        try
        {
            using var process = System.Diagnostics.Process.Start(info) ?? throw new InvalidOperationException("Yardımcı başlatılamadı.");
            await process.WaitForExitAsync(lifetime.Token);
            Log.Write("services-helper-exit-" + process.ExitCode);
            return process.ExitCode switch { ServiceHelper.Ok => done, ServiceHelper.Partial => "Bazı servisler değiştirilemedi; kayıt korundu, yeniden deneyebilir veya geri alabilirsin.", ServiceHelper.NotElevated => "Yönetici izni verilmedi.", _ => "Servis yardımcısı isteği reddetti." };
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { return "Yönetici izni iptal edildi; hiçbir şey değişmedi."; }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { Log.Write("services-helper-start-failed", ex); return "Servis yardımcısı açılamadı: " + ex.Message; }
    }
    internal string RestoreWindowsLook()
    {
        try { Look.Restore(); Log.Write("windows-look-restored"); return "Windows görünümü Noxelyra'dan önceki haline döndü."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidDataException) { Log.Write("windows-look-restore-failed", ex); return "Windows görünümü geri alınamadı: " + ex.Message; }
    }
}
