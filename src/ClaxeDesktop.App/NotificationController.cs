using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;

/// <summary>Noxelyra's own notifications (updates, timers, shortcut conflicts), the island timer and the quick settings panel.</summary>
public sealed partial class DesktopController
{
    private NotificationLog? notificationLog;
    private QuickSettingsWindow? quickSettings;
    internal NotificationLog Notifications => notificationLog ??= new(Path.Combine(desktopData, "notifications.json"));
    /// <summary>Raised for a notification the island should show (not raised while do-not-disturb is on).</summary>
    internal event Action<NotificationItem>? NotificationPosted;
    internal event Action? NotificationsChanged;
    internal event Action? TimerChanged;
    internal IslandTimer? Timer { get; private set; }
    internal bool IsLocked => locked;
    internal void Notify(string title, string body, string kind = "info")
    {
        if (exiting) return;
        var item = Notifications.Add(title, body, kind, DateTimeOffset.Now);
        Log.Write("notification-" + item.Kind);
        if (!Notifications.DoNotDisturb) NotificationPosted?.Invoke(item);
        NotificationsChanged?.Invoke();
    }
    internal void SetDoNotDisturb(bool on)
    {
        Notifications.DoNotDisturb = on; NotificationsChanged?.Invoke();
        ViewModel.Status = on ? "Rahatsız etme açık: bildirimler sessizce listeye eklenir." : "Rahatsız etme kapalı.";
    }
    internal void MarkNotificationsRead() { Notifications.MarkAllRead(); NotificationsChanged?.Invoke(); }
    internal void ClearNotifications() { Notifications.Clear(); NotificationsChanged?.Invoke(); }
    /// <summary>Starts a countdown of the given minutes, or a stopwatch when null.</summary>
    internal void StartTimer(int? minutes)
    {
        Timer = new(DateTimeOffset.Now, minutes is { } m ? TimeSpan.FromMinutes(m) : null); TimerChanged?.Invoke();
        ViewModel.Status = minutes is { } length ? $"{length} dakikalık zamanlayıcı başladı." : "Kronometre başladı.";
    }
    internal void StopTimer() { if (Timer is null) return; Timer = null; TimerChanged?.Invoke(); }
    // Runs every second, also while locked, so a countdown still ends on time.
    private void TimerTick()
    {
        if (Timer is not { } timer || !timer.Finished(DateTimeOffset.Now)) return;
        Timer = null; TimerChanged?.Invoke();
        Notify("Zamanlayıcı bitti", $"{(int)timer.Length!.Value.TotalMinutes} dakika doldu.", "timer");
        try { System.Media.SystemSounds.Asterisk.Play(); } catch (Exception) { /* A missing sound scheme is not an error. */ }
    }
    /// <summary>The panel that slides down from the bar; a second call closes it.</summary>
    public void ShowQuickSettings()
    {
        if (exiting) return;
        if (quickSettings is { IsVisible: true }) { quickSettings.Leave(); return; }
        if (quickSettings is null) { quickSettings = new(this); quickSettings.Closed += (_, _) => quickSettings = null; }
        var panel = windows.OfType<PanelWindow>().FirstOrDefault();
        quickSettings.Present(panel);
    }
}
