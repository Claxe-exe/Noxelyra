using System.Text.Json;
namespace ClaxeDesktop.Core;

public interface IExternalWallpaperService
{
    Task<string> ReadAsync(string executable, CancellationToken token = default);
    Task ApplyAsync(string executable, string file, CancellationToken token = default);
}
public sealed record ExternalWallpaperLease(int Version, string Executable, string Previous, string Applied, TaskbarOwner? Owner = null);
public sealed class ExternalWallpaperSession(IExternalWallpaperService service, string journal, TaskbarOwner? owner = null, Func<Task>? ensureGuardian = null)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private static void Validate(ExternalWallpaperLease lease)
    {
        if ((lease.Version is not (1 or 2) || (lease.Version == 2 && (lease.Owner is null || lease.Owner.ProcessId <= 0 || lease.Owner.StartedUtcTicks <= 0))) || new[] { lease.Executable, lease.Previous, lease.Applied }.Any(p => string.IsNullOrWhiteSpace(p) || p.Length > 4096 || !Path.IsPathFullyQualified(p) || p.StartsWith(@"\\")) || Path.GetFileName(lease.Executable).ToLowerInvariant() is not ("wallpaper32.exe" or "wallpaper64.exe")) throw new InvalidDataException("Wallpaper Engine kurtarma kaydı geçersiz.");
    }
    public async Task ApplyAsync(string executable, string file, CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try
        {
            using var fileGate = await LockAsync(token);
            await RestoreCoreAsync(token);
            var previous = await service.ReadAsync(executable, token);
            var lease = new ExternalWallpaperLease(owner is null ? 1 : 2, executable, previous, file, owner); Validate(lease);
            if (!File.Exists(previous) || !File.Exists(file)) throw new IOException("Önceki veya seçilen Wallpaper Engine dosyası bulunamadı; geri dönüş güvencesi olmadan değişiklik yapılmadı.");
            Directory.CreateDirectory(Path.GetDirectoryName(journal)!);
            using (var stream = new FileStream(journal + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)) { JsonSerializer.Serialize(stream, lease); stream.Flush(true); }
            File.Move(journal + ".tmp", journal, true);
            if (ensureGuardian is not null) await ensureGuardian();
            await service.ApplyAsync(executable, file, token);
            if (!string.Equals(await service.ReadAsync(executable, token), file, StringComparison.OrdinalIgnoreCase)) throw new IOException("Wallpaper Engine seçili dosyayı doğrulamadı; kurtarma kaydı korunuyor.");
        }
        finally { gate.Release(); }
    }
    public async Task RestoreAsync(CancellationToken token = default, TaskbarOwner? expectedOwner = null)
    {
        await gate.WaitAsync(token); try { using var fileGate = await LockAsync(token); await RestoreCoreAsync(token, expectedOwner); } finally { gate.Release(); }
    }
    private async Task<FileStream> LockAsync(CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(journal)!);
        for (int i = 0; ; i++) try { return new FileStream(journal + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) when (i < 40) { await Task.Delay(50, token); }
    }
    private async Task RestoreCoreAsync(CancellationToken token, TaskbarOwner? expectedOwner = null)
    {
        if (!File.Exists(journal)) return;
        if (new FileInfo(journal).Length > 16384) throw new InvalidDataException("Wallpaper Engine kurtarma kaydı çok büyük.");
        var lease = JsonSerializer.Deserialize<ExternalWallpaperLease>(await File.ReadAllTextAsync(journal, token)) ?? throw new InvalidDataException(); Validate(lease);
        if (expectedOwner is not null && lease.Owner != expectedOwner) return;
        if (string.Equals(await service.ReadAsync(lease.Executable, token), lease.Applied, StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(lease.Previous)) throw new IOException("Önceki WE dosyası eksik; kurtarma kaydı korundu.");
            await service.ApplyAsync(lease.Executable, lease.Previous, token);
            if (!string.Equals(await service.ReadAsync(lease.Executable, token), lease.Previous, StringComparison.OrdinalIgnoreCase)) throw new IOException("Wallpaper Engine geri dönüşü doğrulanamadı.");
        }
        File.Delete(journal); // A user's external change is preserved.
    }
}
