using System.Diagnostics;
using System.Net.Http;
namespace ClaxeDesktop.App;

/// <summary>
/// Installs or opens Noxelyra Music, the companion player whose video shows behind the island card. Only on the user's click:
/// the latest setup is downloaded over HTTPS from the project's own GitHub releases (redirects allowed only to GitHub's
/// download hosts) and its normal installer is started. The installer is unsigned, so Windows may show SmartScreen.
/// </summary>
internal static class MusicApp
{
    internal const string Repository = "https://github.com/Claxe-exe/Noxelyra-Music";
    private static readonly Uri Download = new(Repository + "/releases/latest/download/Noxelyra-Music-Setup.exe");
    private static readonly HashSet<string> Hosts = new(StringComparer.OrdinalIgnoreCase) { "github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com" };
    private const long MaxBytes = 400L * 1024 * 1024;
    internal static string ExecutablePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Noxelyra Music", "Noxelyra Music.exe");
    internal static bool Installed => File.Exists(ExecutablePath);
    internal static string Open()
    {
        try { Process.Start(new ProcessStartInfo(ExecutablePath) { UseShellExecute = true }); return "Noxelyra Music açılıyor."; }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException) { return "Noxelyra Music açılamadı: " + ex.Message; }
    }
    /// <summary>Downloads the latest setup and starts it. <paramref name="progress"/> gets short Turkish status lines.</summary>
    internal static async Task<string> InstallAsync(Action<string> progress, CancellationToken token)
    {
        var file = Path.Combine(Path.GetTempPath(), "Noxelyra-Music-Setup.exe");
        try
        {
            using var client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromMinutes(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Noxelyra");
            var uri = Download; HttpResponseMessage? response = null;
            for (int hop = 0; hop < 6; hop++)
            {
                if (uri.Scheme != Uri.UriSchemeHttps || !Hosts.Contains(uri.Host)) return "İndirme beklenmeyen bir adrese yönlendi; durduruldu.";
                response?.Dispose();
                response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
                if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } next) { uri = next.IsAbsoluteUri ? next : new Uri(uri, next); continue; }
                break;
            }
            using (response)
            {
                if (response is null || !response.IsSuccessStatusCode) return "Noxelyra Music indirilemedi" + (response is null ? "." : $" (GitHub {(int)response.StatusCode}).");
                var total = response.Content.Headers.ContentLength;
                if (total > MaxBytes) return "Kurulum dosyası beklenenden büyük; durduruldu.";
                await using var input = await response.Content.ReadAsStreamAsync(token);
                await using (var output = File.Create(file))
                {
                    var buffer = new byte[81920]; long done = 0; int read, shown = -1;
                    while ((read = await input.ReadAsync(buffer, token)) > 0)
                    {
                        done += read; if (done > MaxBytes) throw new IOException("Kurulum dosyası beklenenden büyük.");
                        await output.WriteAsync(buffer.AsMemory(0, read), token);
                        var percent = total is > 0 ? (int)(done * 100 / total.Value) : -1;
                        if (percent != shown && percent % 5 == 0) { shown = percent; progress(percent >= 0 ? $"İndiriliyor… %{percent}" : $"İndiriliyor… {done / 1048576} MB"); }
                    }
                }
            }
            Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
            return "Noxelyra Music kurulumu başladı. Windows uyarı verirse 'Ek bilgi → Yine de çalıştır' de.";
        }
        catch (OperationCanceledException) { return "İndirme iptal edildi."; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { return "Noxelyra Music kurulamadı: " + ex.Message; }
    }
}
