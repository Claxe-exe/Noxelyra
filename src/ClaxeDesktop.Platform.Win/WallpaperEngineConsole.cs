using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace ClaxeDesktop.Platform.Win;

// WE 2.8 writes getWallpaper through the parent's console, not a redirected pipe.
// A disposable child owns that console; the interactive user's console is never attached/read.
public static class WallpaperEngineConsole
{
    private sealed record Reply(string? Path, string? Error);
    private static string QueryDirectory(string nonce)
    {
        if (!Guid.TryParseExact(nonce, "N", out _)) throw new InvalidDataException("Geçersiz WE sorgu kimliği.");
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Noxelyra.EngineQueries");
        var target = System.IO.Path.Combine(root, nonce);
        for (var current = target; current is not null; current = System.IO.Path.GetDirectoryName(current))
            if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("WE sorgu yolu bağlantı içeriyor.");
        return target;
    }
    public static async Task<string> QueryAsync(string executable, CancellationToken token)
    {
        var nonce = Guid.NewGuid().ToString("N"); var directory = QueryDirectory(nonce); Directory.CreateDirectory(directory);
        var result = System.IO.Path.Combine(directory, "result.json");
        Process? helper = null;
        try
        {
            var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            foreach (var value in new[] { "--we-query", executable, nonce }) info.ArgumentList.Add(value);
            helper = Process.Start(info) ?? throw new IOException("WE sorgu yardımcısı açılamadı.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await helper.WaitForExitAsync(timeout.Token);
            if (!File.Exists(result) || new FileInfo(result).Length > 16384) throw new IOException("WE sorgu yanıtı alınamadı.");
            var reply = JsonSerializer.Deserialize<Reply>(await File.ReadAllTextAsync(result, token)) ?? throw new InvalidDataException();
            if (reply.Error is not null) throw new IOException(reply.Error);
            return Normalize(reply.Path);
        }
        finally
        {
            if (helper is not null)
            {
                try { if (!helper.HasExited) helper.Kill(); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
                helper.Dispose();
            }
            // Only the known result file and this invocation's empty directory are removed.
            try { QueryDirectory(nonce); if (File.Exists(result)) File.Delete(result); Directory.Delete(directory, false); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
    private static string Normalize(string? path)
    {
        path = path?.Trim().Trim('"');
        if (string.IsNullOrEmpty(path) || path.Length > 4096 || !System.IO.Path.IsPathFullyQualified(path) || path.StartsWith(@"\\") || path.Contains('\0') || path.Contains('\n') || path.Contains('\r')) throw new IOException("WE geçerli yerel arka plan yolu döndürmedi; geri dönüş güvencesi olmadan değişiklik yapılmadı.");
        return System.IO.Path.GetFullPath(path);
    }
    public static async Task<int> RunHelperAsync(string executable, string nonce)
    {
        string directory;
        try { directory = QueryDirectory(nonce); if (!Directory.Exists(directory)) return 2; }
        catch { return 2; }
        Reply reply;
        bool allocated = false;
        try
        {
            if (!System.IO.Path.IsPathFullyQualified(executable) || executable.StartsWith(@"\\") || System.IO.Path.GetFileName(executable).ToLowerInvariant() is not ("wallpaper32.exe" or "wallpaper64.exe") || (File.GetAttributes(executable) & FileAttributes.ReparsePoint) != 0) throw new IOException("WE uygulama yolu geçersiz.");
            if (!AllocConsole()) throw new IOException("Özel WE sorgu konsolu açılamadı.");
            allocated = true; ShowWindow(GetConsoleWindow(), 0);
            var info = new ProcessStartInfo(executable) { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden };
            info.ArgumentList.Add("-control"); info.ArgumentList.Add("getWallpaper");
            using var child = Process.Start(info) ?? throw new IOException("WE sorgusu başlatılamadı.");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4)); await child.WaitForExitAsync(timeout.Token);
            if (child.ExitCode != 0) throw new IOException("WE sorgusu başarısız.");
            var buffer = new StringBuilder(8192);
            if (!ReadConsoleOutputCharacter(GetStdHandle(-11), buffer, 8192, default, out _)) throw new IOException("Özel WE konsol çıktısı okunamadı.");
            reply = new(Normalize(buffer.ToString()), null);
        }
        catch (Exception ex) { reply = new(null, ex is OperationCanceledException ? "WE sorgusu zaman aşımına uğradı." : ex.Message); }
        finally { if (allocated) FreeConsole(); }
        try { await File.WriteAllTextAsync(System.IO.Path.Combine(directory, "result.json"), JsonSerializer.Serialize(reply)); return reply.Error is null ? 0 : 2; } catch { return 2; }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Coord { public short X, Y; }
    [DllImport("kernel32.dll")] private static extern bool AllocConsole();
    [DllImport("kernel32.dll")] private static extern bool FreeConsole();
    [DllImport("kernel32.dll")] private static extern nint GetConsoleWindow();
    [DllImport("kernel32.dll")] private static extern nint GetStdHandle(int id);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool ReadConsoleOutputCharacter(nint handle, StringBuilder text, uint length, Coord position, out uint read);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int command);
}
