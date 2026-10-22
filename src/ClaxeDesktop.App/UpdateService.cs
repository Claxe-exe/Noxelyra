using Velopack;
using Velopack.Sources;
namespace ClaxeDesktop.App;

/// <summary>
/// Self-update through the project's public GitHub releases (Velopack). The only network request Noxelyra makes,
/// visible and switchable in the control center; nothing about the user is sent.
/// </summary>
internal sealed class UpdateService
{
    internal const string Repository = "https://github.com/Claxe-exe/Noxelyra";
    private readonly UpdateManager manager = new(new GithubSource(Repository, null, false));
    private UpdateInfo? ready;
    private bool busy;
    /// <summary>False for development builds and plain copies; only Setup.exe / portable Velopack installs can update.</summary>
    internal bool Installed => manager.IsInstalled;
    internal string Version => manager.CurrentVersion?.ToString() ?? typeof(UpdateService).Assembly.GetName().Version?.ToString(3) ?? "?";
    internal string? ReadyVersion => ready?.TargetFullRelease.Version.ToString();
    /// <summary>Checks GitHub and downloads a newer full or delta package in the background. Returns a status line.</summary>
    internal async Task<string> CheckAsync(CancellationToken token)
    {
        if (!Installed) return "Bu kopya Setup ile kurulmadığı için kendini güncelleyemez.";
        if (busy) return "Güncelleme denetimi sürüyor…";
        busy = true;
        try
        {
            var info = await manager.CheckForUpdatesAsync();
            if (info is null) return $"Noxelyra {Version} güncel.";
            await manager.DownloadUpdatesAsync(info, null, token);
            ready = info;
            return $"Noxelyra {ReadyVersion} indirildi; kapanışta kurulur ya da şimdi yeniden başlatabilirsin.";
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or IOException or InvalidOperationException or TaskCanceledException or UnauthorizedAccessException)
        { return "Güncelleme denetlenemedi: " + ex.Message; }
        finally { busy = false; }
    }
    /// <summary>Called at the very end of exit: hands the downloaded package to Velopack's updater, which waits for this process to end.</summary>
    internal void ApplyOnExit(bool restart)
    {
        if (ready is null) return;
        manager.WaitExitThenApplyUpdates(ready.TargetFullRelease, silent: !restart, restart: restart);
    }
}
