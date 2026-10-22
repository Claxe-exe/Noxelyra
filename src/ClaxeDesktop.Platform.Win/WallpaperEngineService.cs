using System.Diagnostics;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.Platform.Win;

public static class WallpaperEngineService
{
    public static IReadOnlyList<string> Arguments(string command) => command switch
    {
        "getWallpaper" or "pause" or "play" => ["-control", command],
        _ => throw new ArgumentException("Desteklenmeyen Wallpaper Engine komutu.")
    };
    public static async Task<string> SendAsync(string executable, string command, CancellationToken token = default, string? wallpaper = null)
    {
        if (!Path.IsPathFullyQualified(executable) || executable.StartsWith(@"\\")) throw new InvalidDataException("Yerel Wallpaper Engine uygulamasını seç.");
        executable = Path.GetFullPath(executable);
        var name = Path.GetFileNameWithoutExtension(executable).ToLowerInvariant();
        if (name is not ("wallpaper32" or "wallpaper64") || !Path.IsPathFullyQualified(executable) || executable.StartsWith(@"\\") || !File.Exists(executable) || (File.GetAttributes(executable) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Yerel wallpaper32.exe veya wallpaper64.exe seç.");
        var running = false;
        foreach (var process in Process.GetProcessesByName(name))
        {
            using (process) try { if (string.Equals(process.MainModule?.FileName, executable, StringComparison.OrdinalIgnoreCase)) running = true; } catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
        }
        if (!running) throw new InvalidOperationException("Wallpaper Engine bu konumdan çalışmıyor. Önce kendin açıp yeniden dene.");
        if (command == "getWallpaper") return await WallpaperEngineConsole.QueryAsync(executable, token);
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        if (command == "openWallpaper")
        {
            if (wallpaper is null || !Path.IsPathFullyQualified(wallpaper) || wallpaper.StartsWith(@"\\") || !File.Exists(wallpaper)) throw new InvalidDataException("Yerel Wallpaper Engine dosyası geçersiz.");
            foreach (var arg in new[] { "-control", "openWallpaper", "-file", wallpaper }) info.ArgumentList.Add(arg);
        }
        else foreach (var arg in Arguments(command)) info.ArgumentList.Add(arg);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token); linked.CancelAfter(TimeSpan.FromSeconds(4));
        using var child = Process.Start(info) ?? throw new IOException("Wallpaper Engine komutu başlatılamadı.");
        // Bound untrusted child output; drain both streams to avoid pipe deadlocks.
        async Task<string> ReadBounded(StreamReader stream)
        {
            var buffer = new char[4097]; int count = 0;
            while (count < buffer.Length) { var read = await stream.ReadAsync(buffer.AsMemory(count), linked.Token); if (read == 0) break; count += read; }
            if (count == buffer.Length) throw new IOException("Wallpaper Engine yanıtı çok uzun.");
            return new string(buffer, 0, count).Trim();
        }
        var output = ReadBounded(child.StandardOutput); var errors = ReadBounded(child.StandardError);
        await child.WaitForExitAsync(linked.Token); var text = await output; await errors;
        if (child.ExitCode != 0) throw new IOException("Wallpaper Engine komutu başarısız.");
        return command == "getWallpaper" ? text.Trim('"') : "Komut gönderildi. Oynatma durumunu Wallpaper Engine'den kontrol edebilirsin.";
    }
}

public sealed class WindowsExternalWallpaperService : IExternalWallpaperService
{
    public Task<string> ReadAsync(string executable, CancellationToken token = default) => WallpaperEngineService.SendAsync(executable, "getWallpaper", token);
    public async Task ApplyAsync(string executable, string file, CancellationToken token = default) => await WallpaperEngineService.SendAsync(executable, "openWallpaper", token, file);
}
