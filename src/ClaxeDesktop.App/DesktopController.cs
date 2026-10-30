using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace ClaxeDesktop.App;
public sealed partial class DesktopController
{
    public DesktopViewModel ViewModel { get; }
    public StartupService Startup { get; } = new();
    public LocalLog Log { get; }
    public bool ReadOnly { get; }
    private readonly ISettingsStore store;
    private readonly IMonitorService monitors;
    private readonly ISystemMetrics metrics;
    private readonly IAppLauncher launcher;
    private readonly IWindowService windowService;
    private readonly ShellIconService icons = new();
    private readonly CancellationTokenSource lifetime = new();
    private bool scanning;
    private readonly RecoveryService recovery;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private readonly List<FloatingWindow> windows = [];
    private Settings committed;
    private SettingsWindow? settingsWindow;
    private Forms.NotifyIcon? tray;
    private bool exiting;
    private readonly bool safe;
    private bool locked;
    private bool suspended;
    internal bool IsSuspended => suspended;
    private readonly SemaphoreSlim persistenceGate = new(1, 1);
    private readonly ThemeService themeService = new();
    public WidgetWorkspace Widgets { get; }
    internal IReadOnlyList<FloatingWindow> Windows => windows;
    internal bool IsSafeMode => safe;
    internal bool IsExiting => exiting;
    internal IWindowService ReleaseWindowSignals => windowService;
    public DesktopController(SettingsLoad loaded, ISettingsStore store, IMonitorService monitors, ISystemMetrics metrics, IAppLauncher launcher, RecoveryService recovery, LocalLog log, bool safe, IWindowService? windowService = null)
    {
        this.store = store; this.monitors = monitors; this.metrics = metrics; this.launcher = launcher; this.recovery = recovery; Log = log; this.safe = safe;
        this.windowService = windowService ?? new WindowsWindowService();
        committed = loaded.Settings; ReadOnly = loaded.ReadOnly; ViewModel = new(committed);
        InitializeDesktopServices();
        Widgets = new(this, Environment.GetEnvironmentVariable("CLAXE_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaxeDesktop"));
        ViewModel.Status = loaded.Notice ?? (safe ? "Güvenli mod: yerleşim ana ekranda açıldı. Kayıtlı düzen korundu." : "Midnight Noxelyra · Masaüstün hazır.");
        ViewModel.AppearanceChanged += ApplyAppearance;
    }
    public void Start()
    {
        RefreshMonitors();
        windows.Add(new PanelWindow(ViewModel, this, icons));
        var dock = new DockWindow(ViewModel, Launch, ShowSettings, windowService, icons, () => _ = SaveShortcutsAsync()) { SafeMode = safe, Pin = PinToDock, AddApps = ShowLauncher };
        dock.ActivationCompleted += succeeded => Log.Write(succeeded ? "dock-activation-succeeded" : "dock-activation-declined");
        windows.Add(dock);
        foreach (var window in windows)
        {
            window.Moved += OnMoved; window.Closing += WindowClosing; window.Show();
        }
        StallWatch.Stage = "start:services"; StartDesktopServices(); StallWatch.Stage = "start:widgets"; Widgets.Start(); StallWatch.Stage = "start:advanced"; StartAdvancedServices();
        StallWatch.Stage = "start:desktop-space"; StartDesktopSpace(); TrimMemoryLater();
        StallWatch.Stage = "start:appearance"; ApplyAppearance(); PlaceAll(safe); Tick(); StallWatch.Stage = "start:tray";
        timer.Tick += (_, _) => Tick(); timer.Start();
        saveTimer.Tick += async (_, _) => { saveTimer.Stop(); await SaveCommittedAsync(); };
        SystemEvents.DisplaySettingsChanged += DisplayChanged; SystemEvents.SessionSwitch += SessionChanged; SystemEvents.PowerModeChanged += PowerChanged;
        tray = new Forms.NotifyIcon { Text = "Noxelyra Desktop", Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application, Visible = true };
        var menu = new Forms.ContextMenuStrip(); menu.Items.Add("Noxelyra masaüstü alanını aç", null, (_, _) => ShowDesktopSpace()); menu.Items.Add("Dock'u öne getir · Alt+K", null, (_, _) => SummonDock()); menu.Items.Add("Uygulamalar", null, (_, _) => ShowLauncher()); menu.Items.Add("Hızlı ayarlar ve bildirimler", null, (_, _) => ShowQuickSettings()); menu.Items.Add("Kontrol merkezi", null, (_, _) => ShowFullControls()); menu.Items.Add("Hafifletme", null, (_, _) => ShowLighten()); menu.Items.Add("Profiller ve pencere düzenleri", null, (_, _) => ShowAdvanced()); menu.Items.Add("Ayarlar", null, (_, _) => ShowHub()); menu.Items.Add("Kısayollar ve ipuçları", null, (_, _) => ShowShortcuts()); menu.Items.Add("Widget atölyesi", null, (_, _) => Widgets.ShowEditor()); menu.Items.Add("Widget rafını göster / gizle", null, (_, _) => Widgets.ToggleShelf()); menu.Items.Add("Panelleri göster", null, (_, _) => { foreach (var w in windows.Where(w => w.IsVisible)) w.Activate(); }); menu.Items.Add("Noxelyra'dan çık", null, (_, _) => Exit()); tray.ContextMenuStrip = menu; tray.DoubleClick += (_, _) => ShowHub();
        // Only the installed copy may (re)write the sign-in shortcut; a build or publish folder must never become what Windows starts.
        if (!safe && Environment.GetEnvironmentVariable("CLAXE_DATA_DIR") is null && Startup.Enabled && Updates.Installed)
        {
            try { Startup.SetEnabled(true); } catch (Exception ex) { Log.Write("startup-migration-failed", ex); ViewModel.Status = "Başlangıç kısayolu güncellenemedi; ayarlardan yeniden etkinleştir."; }
        }
        if (safe) ShowSettings();
        Log.Write(safe ? "started-safe" : "started");
    }
    private void Tick()
    {
        TimerTick(); _ = AdvanceProfilesAsync();MascotActivity(); DesktopSpaceActivity(); KeepTaskbarHidden();
        _ = AdvanceWallpaperRotationsAsync();
        Widgets.UpdateActivity();
        if (locked || suspended || !windows.Any(w => w.IsVisible && w.WindowState != WindowState.Minimized)) return;
        try { ViewModel.Tick(metrics.Sample()); } catch (Exception ex) { Log.Write("metrics-unavailable", ex); ViewModel.Tick(new(null, null, null, null)); }
        _ = RefreshWindowsAsync();
    }
    internal bool ShouldPauseWidget(MonitorInfo monitor) => locked || suspended || (!Widgets.Editing && windowService.IsFullscreenOn(monitor));
    private async Task RefreshWindowsAsync()
    {
        if (scanning || exiting) return; scanning = true;
        try
        {
            var snapshot = await Task.Run(windowService.Snapshot, lifetime.Token);
            if (!exiting) {lastWindowSnapshot=snapshot;TrackAppWindow(snapshot);windows.OfType<DockWindow>().Single().UpdateWindows(snapshot);windows.OfType<PanelWindow>().Single().UpdateWindows(snapshot, windowService.Foreground);}
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log.Write("window-snapshot-failed", ex); ViewModel.Status = "Açık pencereler şu anda okunamıyor."; }
        finally { scanning = false; }
    }
    public void ApplyAppearance()
    {
        themeService.Apply(ViewModel.Settings);
        foreach (var w in windows)
        {
            w.ApplyAppearance(ViewModel);
            if (w is PanelWindow panel)
            {
                panel.Integrated = desktopBindings?.Registered == true;
                var target = ViewModel.Monitors.FirstOrDefault(m => !safe && m.Id == ViewModel.Settings.Panel?.MonitorId) ?? ViewModel.Monitors.First(m => m.Primary);
                w.Width = Math.Min(ViewModel.PanelWidth, target.Width / target.Scale); w.Height = ViewModel.PanelHeight;
                panel.UpdateOrder(ViewModel.ClockOnLeft);
            }
            if (w is DockWindow dock) dock.Rebuild();
        }
        if (windows.Count > 0) PlaceAll(safe);
    }
    private Placement? Saved(string kind, bool defaults) => defaults ? null : kind switch { "Panel" => ViewModel.Settings.Panel, "Dock" => ViewModel.Settings.Dock,"Mascot"=>ViewModel.Settings.Mascot.Placement, _ => ViewModel.Settings.Widget };
    private void PlaceAll(bool defaults) { foreach (var w in windows) { if(w is WidgetWindow widget) Widgets.Place(widget); else if(w is not PanelWindow || desktopBindings?.Registered != true) w.Place(Saved(w.Kind, defaults), ViewModel.Monitors, ViewModel.SnapToGrid); } desktopBindings?.Resize(ViewModel.PanelHeight); }
    internal void AddWidgetWindow(FloatingWindow window) { windows.Add(window); window.Moved += OnMoved; window.Closing += WindowClosing; }
    internal void RemoveWidgetWindow(FloatingWindow window) { windows.Remove(window); window.Moved -= OnMoved; window.Closing -= WindowClosing; window.Close(); }
    internal void OnMoved(FloatingWindow window)
    {
        desktopSpace?.ScheduleReflow();
        if(window is MascotWindow pet){_=SaveMascotPlacementAsync(pet);return;}
        if(window is WidgetWindow widget) { Widgets.Move(widget); return; }
        var placement = window.Capture(ViewModel.Monitors); ViewModel.UpdatePlacement(window.Kind, placement);
        if (window is DockWindow && !safe) ViewModel.DockEdge = "floating";
        window.Place(placement, ViewModel.Monitors, ViewModel.SnapToGrid); placement = window.Capture(ViewModel.Monitors); ViewModel.UpdatePlacement(window.Kind, placement);
        if (!safe)
        {
            committed = window.Kind switch { "Panel" => committed with { Panel = placement }, "Dock" => committed with { Dock = placement, DockEdge = "floating" }, _ => committed with { Widget = placement } };
            saveTimer.Stop(); saveTimer.Start();
        }
    }
    public void ShowSettings()
    {
        if (settingsWindow is null) { settingsWindow = new(this); settingsWindow.Closed += (_, _) => settingsWindow = null; settingsWindow.Show(); }
        settingsWindow.WindowState = WindowState.Normal; settingsWindow.Activate();
    }
    public void Launch(Shortcut item)
    {
        try { launcher.Launch(item); ViewModel.Status = item.Name + " açıldı."; }
        catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException or UnauthorizedAccessException) { Log.Write("launch-failed", ex); ViewModel.Status = item.Name + " açılamadı. Dosyayı ve kısayolu kontrol et."; ShowSettings(); }
    }
    public async Task SaveAsync()
    {
        if (ReadOnly) return;
        saveTimer.Stop();
        await persistenceGate.WaitAsync();
        try
        {
            var candidate = ViewModel.Settings with { Widgets = committed.Widgets, IntegratedDesktop=committed.IntegratedDesktop,HideWindowsTaskbar=committed.HideWindowsTaskbar,GlobalShortcuts=committed.GlobalShortcuts,WallpaperEnabled=committed.WallpaperEnabled,Wallpapers=committed.Wallpapers,WallpaperRotations=committed.WallpaperRotations,ActiveProfileId=committed.ActiveProfileId,ProfileManualUntil=committed.ProfileManualUntil,LayoutShortcut=committed.LayoutShortcut,Mascot=committed.Mascot,DesktopSpace=committed.DesktopSpace,Keyboard=committed.Keyboard };
            if (safe) candidate = candidate with { Panel = committed.Panel, Dock = committed.Dock, Widget = committed.Widget };
            await store.SaveAsync(candidate); committed = candidate; if (ViewModel.Settings == candidate) ViewModel.AcceptChanges(); ViewModel.Status = "Ayarlar kaydedildi."; Log.Write("settings-saved");
        }
        catch (Exception ex) { Log.Write("save-failed", ex); ViewModel.Status = "Ayarlar kaydedilemedi. Son kayıt korunuyor; yeniden deneyebilirsin."; }
        finally { persistenceGate.Release(); }
    }
    // Applies and persists one catalog setting, so unrelated previews in an open settings window stay unsaved.
    internal async Task<bool> SetSettingAsync(string key, object value, string? done = null)
    {
        if (ReadOnly || safe) { ViewModel.Status = "Güvenli/salt okunur modda bu ayar kaydedilmez."; return false; }
        await persistenceGate.WaitAsync();
        try
        {
            var next = SettingsCatalog.Apply(committed, key, value); await store.SaveAsync(next); committed = next;
            ViewModel.Edit(SettingsCatalog.Apply(ViewModel.Settings, key, value));
            ViewModel.Status = done ?? "Ayar kaydedildi."; return true;
        }
        catch (Exception ex) { Log.Write("setting-save-failed", ex); ViewModel.Status = "Ayar uygulanamadı: " + ex.Message; return false; }
        finally { persistenceGate.Release(); }
    }
    internal async Task<bool> SetThemeAsync(string id)
    {
        if (!await SetSettingAsync("themes.activeTheme", id)) return false;
        ViewModel.Status = ViewModel.ThemeName + " uygulandı."; return true;
    }
    internal bool IsPinned(string target) => ViewModel.Shortcuts.Any(s => string.Equals(s.Target, target, StringComparison.OrdinalIgnoreCase));
    internal void TogglePin(Shortcut app)
    {
        var existing = ViewModel.Shortcuts.FirstOrDefault(s => string.Equals(s.Target, app.Target, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) { ViewModel.Remove(existing); _ = SaveShortcutsAsync(); ViewModel.Status = app.Name + " dock'tan kaldırıldı."; return; }
        PinToDock(app);
    }
    internal bool PinToDock(Shortcut app)
    {
        if (!LaunchPolicy.IsAllowed(app.Target)) { ViewModel.Status = "Yalnız yerel .exe veya .lnk sabitlenebilir."; return false; }
        if (IsPinned(app.Target)) { ViewModel.Status = app.Name + " zaten dock'ta."; return false; }
        if (ViewModel.Shortcuts.Count >= Settings.MaxShortcuts) { ViewModel.Status = $"Dock en fazla {Settings.MaxShortcuts} uygulama tutar."; return false; }
        ViewModel.Add(app with { Name = app.Name.Length > 80 ? app.Name[..80] : app.Name }); _ = SaveShortcutsAsync(); ViewModel.Status = app.Name + " dock'a sabitlendi."; return true;
    }
    private async Task SaveShortcutsAsync()
    {
        if (ReadOnly) { ViewModel.Status = "Salt okunur ayar dosyasında kısayol değişikliği kaydedilemez."; return; }
        await persistenceGate.WaitAsync();
        try { var next = committed with { Shortcuts = ViewModel.Settings.Shortcuts.ToList() }; await store.SaveAsync(next); committed = next; ViewModel.Status = "Dock kısayolları kaydedildi."; }
        catch (Exception ex) { Log.Write("shortcut-save-failed", ex); ViewModel.Status = "Kısayol kaydedilemedi; ayarlardan yeniden deneyebilirsin."; }
        finally { persistenceGate.Release(); }
    }
    internal async Task<bool> SaveWidgetsAsync(IReadOnlyList<WidgetInstance> widgets)
    {
        if (ReadOnly || safe) return false;
        await persistenceGate.WaitAsync();
        try
        {
            var candidate = committed with { Widgets = widgets.ToList() }; await store.SaveAsync(candidate); committed = candidate;
            ViewModel.SetWidgets(widgets); ViewModel.Status = "Widget düzeni kaydedildi."; Log.Write("widgets-saved"); return true;
        }
        catch(Exception ex) { Log.Write("widget-save-failed",ex); ViewModel.Status = "Widget düzeni kaydedilemedi. Taslak açık; yeniden deneyebilirsin."; return false; }
        finally { persistenceGate.Release(); }
    }
    private async Task SaveCommittedAsync()
    {
        if (ReadOnly) return;
        await persistenceGate.WaitAsync();
        try { await store.SaveAsync(committed); }
        catch (Exception ex) { Log.Write("placement-save-failed", ex); ViewModel.Status = "Konum kaydedilemedi. Ayarlardan yeniden kaydet."; }
        finally { persistenceGate.Release(); }
    }
    public void RevertPreview() { ViewModel.Replace(committed); ViewModel.Status = "Son kaydedilen ayarlara dönüldü."; }
    public void ResetLayout() { ViewModel.Replace(ViewModel.Settings with { Panel = null, Dock = null, Widget = null }); PlaceAll(true); ViewModel.Status = "Yerleşim ana ekranda önizleniyor. Kalıcılaştırmak için Kaydet."; }
    public void RefreshMonitors()
    {
        try
        {
            var found = monitors.GetMonitors(); if (found.Count == 0) throw new InvalidOperationException();
            ViewModel.Monitors.Clear(); foreach (var monitor in found) ViewModel.Monitors.Add(monitor);
            if (windows.Count > 0) ApplyAppearance();
            desktopSpace?.Place();
        }
        catch (Exception ex) { Log.Write("monitor-detection-failed", ex); ViewModel.Status = "Ekran bilgisi alınamadı."; if (ViewModel.Monitors.Count == 0) throw; }
    }
    private void DisplayChanged(object? sender, EventArgs e) => Application.Current.Dispatcher.BeginInvoke(RefreshMonitors, DispatcherPriority.Background);
    private void SessionChanged(object sender, SessionSwitchEventArgs e)
    {
        locked = e.Reason == SessionSwitchReason.SessionLock;
        if (locked) windows.OfType<DockWindow>().Single().DismissSummon();
    }
    private void WindowClosing(object? sender, CancelEventArgs e) { if (!exiting) { e.Cancel = true; ShowSettings(); } }
    private void PowerChanged(object sender, PowerModeChangedEventArgs e) => Application.Current.Dispatcher.BeginInvoke(() => ApplyPowerMode(e.Mode));
    internal void ApplyPowerMode(PowerModes mode)
    {
        if (exiting) return;
        if (mode == PowerModes.Suspend)
        {
            suspended = true; desktopFollowDelay.Stop(); desktopSpace?.UpdateActivity(true); MascotActivity();
        }
        else if (mode == PowerModes.Resume)
        {
            suspended = false; RefreshMonitors(); desktopSpace?.UpdateActivity(locked);
            ProcessDesktopPresence(ClaxeDesktop.Platform.Win.WindowsDesktopMonitor.Presence());
        }
    }
    public async void Exit()
    {
        if (exiting) return; exiting = true;
        // The power-off sequence covers the screen while everything below is saved and closed.
        var outro = safe || emergencyExit ? Task.CompletedTask : IntroWindow.PlayOutroAsync(committed);
        if (desktopSpace is not null) { committed = committed with { DesktopSpace = committed.DesktopSpace with { Icons = desktopSpace.IconPositions } }; ViewModel.SetAdvanced(committed); }
        timer.Stop(); saveTimer.Stop(); lifetime.Cancel(); await SaveCommittedAsync();
        SystemEvents.DisplaySettingsChanged -= DisplayChanged; SystemEvents.SessionSwitch -= SessionChanged; SystemEvents.PowerModeChanged -= PowerChanged;
        tray?.Dispose(); StopDesktopSpace(); StopAdvancedServices();await StopDesktopServicesAsync(); Widgets.Close(); foreach (var window in windows.ToArray()) window.Close(); settingsWindow?.Close(); icons.Dispose();
        try { recovery.Complete(); } catch (Exception ex) { Log.Write("recovery-cleanup-failed", ex); }
        Log.Write("clean-exit"); await outro;
        try { Updates.ApplyOnExit(restartForUpdate); } catch (Exception ex) { Log.Write("update-apply-failed", ex); }
        Application.Current.Shutdown();
    }
}



