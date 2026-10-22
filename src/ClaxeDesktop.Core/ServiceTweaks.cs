using System.Text.Json;
namespace ClaxeDesktop.Core;

public enum ServiceStart { Automatic, AutomaticDelayed, Manual, Disabled }
public sealed record ServiceState(string Name, ServiceStart Start, bool Running);
public sealed record ServiceTweak(string Name, string Title, string Effect);

public interface IServiceControl
{
    ServiceState Read(string name);
    void SetStart(string name, ServiceStart start);
    void Stop(string name);
    void Start(string name);
}

/// <summary>The only Windows services the elevated helper may touch. Security, update and shell services are deliberately absent.</summary>
public static class ServiceTweaks
{
    public static IReadOnlyList<ServiceTweak> All { get; } =
    [
        new("DiagTrack", "Telemetri (Bağlı Kullanıcı Deneyimleri)", "Microsoft'a kullanım verisi gönderimini azaltır. Windows'un çalışmasını etkilemez."),
        new("SysMain", "SysMain (önceden yükleme)", "Sık kullanılan uygulamaları RAM'e önceden yükler. SSD'de fark azdır; HDD'de açılışlar yavaşlayabilir."),
        new("WSearch", "Windows Search dizinleme", "Dosya içeriği dizinini kapatır. Başlat ve Dosya Gezgini'nde dosya araması yavaşlar; Outlook araması etkilenebilir."),
    ];
    public static bool Allowed(string name) => All.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    public const string JournalName = "services.json";

    /// <summary>Validates helper arguments: "--services apply|restore &lt;journal&gt; [names...]".</summary>
    public static (bool Restore, string Journal, IReadOnlyList<string> Names) Parse(IReadOnlyList<string> args)
    {
        if (args.Count < 3 || args[0] != "--services" || args[1] is not ("apply" or "restore")) throw new InvalidDataException("Servis yardımcısı argümanları geçersiz.");
        var journal = args[2];
        if (!Path.IsPathFullyQualified(journal) || journal.StartsWith(@"\\") || !Path.GetFileName(journal).Equals(JournalName, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Servis kaydı yolu geçersiz.");
        var names = args.Skip(3).ToList();
        if (args[1] == "restore" && names.Count > 0) throw new InvalidDataException("Geri yükleme servis adı almaz.");
        if (args[1] == "apply" && (names.Count == 0 || names.Count > All.Count || names.Any(n => !Allowed(n)) || names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)) throw new InvalidDataException("Yalnız izin verilen servisler kapatılabilir.");
        return (args[1] == "restore", journal, names);
    }
}

/// <summary>Disables allow-listed services after journaling each one's first known state; restore puts start type and running state back.</summary>
public sealed class ServiceSession(IServiceControl control, string journal)
{
    public bool HasChanges => File.Exists(journal) && Load().Count > 0;
    public IReadOnlyList<string> Disable(IReadOnlyList<string> names)
    {
        var originals = Load(); var failed = new List<string>();
        foreach (var name in names)
        {
            if (!ServiceTweaks.Allowed(name)) throw new InvalidOperationException("İzin verilmeyen servis: " + name);
            if (!originals.Any(o => o.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) { originals.Add(control.Read(name)); Save(originals); }
        }
        foreach (var name in names)
        {
            try { control.SetStart(name, ServiceStart.Disabled); if (control.Read(name).Running) control.Stop(name); }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or TimeoutException) { failed.Add(name); }
        }
        return failed;
    }
    public IReadOnlyList<string> Restore()
    {
        var originals = Load(); var failed = new List<string>();
        foreach (var state in originals)
        {
            try { control.SetStart(state.Name, state.Start); if (state.Running && !control.Read(state.Name).Running) control.Start(state.Name); }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or TimeoutException) { failed.Add(state.Name); }
        }
        if (failed.Count == 0 && File.Exists(journal)) File.Delete(journal);
        return failed;
    }
    private List<ServiceState> Load()
    {
        if (!File.Exists(journal)) return [];
        if (new FileInfo(journal).Length > 8192 || (File.GetAttributes(journal) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Servis kaydı geçersiz.");
        List<ServiceState> values;
        try { values = JsonSerializer.Deserialize<List<ServiceState>>(File.ReadAllText(journal)) ?? []; }
        catch (JsonException) { throw new InvalidDataException("Servis kaydı okunamadı."); }
        if (values.Any(v => v is null || !ServiceTweaks.Allowed(v.Name) || !Enum.IsDefined(v.Start)) || values.Select(v => v.Name.ToLowerInvariant()).Distinct().Count() != values.Count) throw new InvalidDataException("Servis kaydı geçersiz.");
        return values;
    }
    private void Save(List<ServiceState> values)
    {
        var directory = Path.GetDirectoryName(journal)!; Directory.CreateDirectory(directory);
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new IOException("Servis kaydı klasörü geçersiz.");
        var temporary = journal + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)) { JsonSerializer.Serialize(stream, values); stream.Flush(true); }
        File.Move(temporary, journal, true);
    }
}
