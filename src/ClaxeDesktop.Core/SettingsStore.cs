using System.Text.Json;

namespace ClaxeDesktop.Core;

public sealed class SettingsStore(string directory) : ISettingsStore
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public string FilePath => Path.Combine(directory, "settings.json");
    private bool preserveBrokenPrimary;
    public async Task<SettingsLoad> LoadAsync(CancellationToken token = default)
    {
        foreach (var path in new[] { FilePath, FilePath + ".bak" })
        {
            if (!File.Exists(path)) continue;
            try
            {
                var content = await File.ReadAllTextAsync(path, token);
                using var document = JsonDocument.Parse(content);
                if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Ayar nesnesi bekleniyor.");
                if (document.RootElement.TryGetProperty("SchemaVersion", out var version) && version.TryGetInt32(out var v) && v > 3)
                    return new(new(), "Bu ayar dosyası daha yeni bir sürüme ait. Dosyayı korumak için kayıt kapalı.", true);
                var settings = JsonSerializer.Deserialize<Settings>(content, Json) ?? throw new InvalidDataException();
                if (settings.SchemaVersion is 1 or 2) settings = settings with { SchemaVersion = 3, Widgets = [WidgetInstance.Default with { Placement = settings.Widget }] };
                settings.Validate();
                return new(settings, path.EndsWith(".bak") ? "Bozuk ayarlar yerine son sağlam yedek yüklendi." : null);
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
            { preserveBrokenPrimary = true; }
        }
        return new(new(), preserveBrokenPrimary ? "Ayarlar okunamadı. Güvenli varsayılanlar yüklendi." : null);
    }
    public async Task SaveAsync(Settings settings, CancellationToken token = default)
    {
        settings.Validate();
        await gate.WaitAsync(token);
        var temp = FilePath + ".tmp";
        try
        {
            Directory.CreateDirectory(directory);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(settings, Json);
            await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { await stream.WriteAsync(bytes, token); await stream.FlushAsync(token); stream.Flush(true); }
            token.ThrowIfCancellationRequested();
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, preserveBrokenPrimary ? null : FilePath + ".bak");
            else File.Move(temp, FilePath);
            preserveBrokenPrimary = false;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); gate.Release(); }
    }
}

public sealed class RecoveryService(string directory)
{
    private string Marker => Path.Combine(directory, "session.lock");
    public bool Begin()
    {
        Directory.CreateDirectory(directory);
        var dirty = File.Exists(Marker);
        File.WriteAllText(Marker, DateTimeOffset.UtcNow.ToString("O"));
        return dirty;
    }
    public void Complete() { if (File.Exists(Marker)) File.Delete(Marker); }
}

public sealed class LocalLog(string directory)
{
    public void Write(string eventName, Exception? error = null)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "events.jsonl");
            if (File.Exists(path) && new FileInfo(path).Length > 1_000_000) File.Move(path, path + ".old", true);
            File.AppendAllText(path, JsonSerializer.Serialize(new { time = DateTimeOffset.UtcNow, eventName, errorType = error?.GetType().Name, errorCode = error?.HResult, stack = error is null ? null : new System.Diagnostics.StackTrace(error, false).ToString() }) + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Logging cannot block safe exit. */ }
    }
}
