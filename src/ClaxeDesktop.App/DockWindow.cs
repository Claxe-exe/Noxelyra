using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;
public sealed class DockWindow : FloatingWindow
{
    private const string IconFont = "Segoe Fluent Icons, Segoe MDL2 Assets";
    public event Action<bool>? ActivationCompleted;
    private readonly StackPanel row = new() { Orientation = Orientation.Horizontal };
    private readonly DesktopViewModel vm;
    private readonly Action<Shortcut> launch;
    private readonly Action settings;
    private readonly IWindowService windowService;
    private readonly ShellIconService icons;
    private readonly Action persistShortcuts;
    private readonly Dictionary<string, string> targets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, nint> lastActivated = new();
    private IReadOnlyList<AppWindow> running = [];
    private string signature = "";
    private bool menuOpen, collapsed, closed, fullscreen, attached, rebuildPending;
    private bool summoned;
    private DateTime summonedUntil;
    private DateTime summonedAt;
    internal bool IsSummoned => summoned;
    private bool fullscreenCandidate;
    private DateTime fullscreenCandidateSince = DateTime.UtcNow;
    private DateTime awaySince = DateTime.UtcNow;
    private double expandedHeight;
    private readonly DispatcherTimer visibilityTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    public DockWindow(DesktopViewModel vm, Action<Shortcut> launch, Action settings, IWindowService windowService, ShellIconService icons, Action persistShortcuts) : base("Dock")
    {
        this.vm = vm; this.launch = launch; this.settings = settings; this.windowService = windowService; this.icons = icons;
        this.persistShortcuts = persistShortcuts;
        Frame.Padding = new(8, 6, 8, 6); Frame.Child = row;
        // Drop .exe/.lnk files from Explorer or the desktop to pin them, like the Windows taskbar.
        AllowDrop = true;
        DragOver += (_, e) => { e.Effects = DroppedApps(e).Any() ? DragDropEffects.Link : DragDropEffects.None; e.Handled = true; };
        Drop += (_, e) => { foreach (var app in DroppedApps(e)) Pin?.Invoke(app); e.Handled = true; };
        PreviewMouseWheel += (_, e) => { if (pages > 1) { ShowPage(page + (e.Delta < 0 ? 1 : -1)); e.Handled = true; } };
        vm.Shortcuts.CollectionChanged += ShortcutsChanged;
        SourceInitialized += (_, _) => attached = true;
        Closed += (_, _) => { closed = true; visibilityTimer.Stop(); vm.Shortcuts.CollectionChanged -= ShortcutsChanged; };
        visibilityTimer.Tick += (_, _) => UpdateVisibility(); visibilityTimer.Start(); Rebuild();
    }
    public void ToggleSummon()
    {
        
        if (closed || vm.Monitors.Count == 0) return;
        summoned = true; summonedAt = DateTime.UtcNow; summonedUntil = summonedAt.AddSeconds(15);
        SetCollapsed(false); Show(); Reposition(); Topmost = true;
        RaiseSummonedDock();
        if (Activate()) { row.Children.OfType<Button>().FirstOrDefault()?.Focus(); }
        else vm.Status = "Dock öne getirildi. Windows klavye odağı vermedi; dock'a tıklayabilirsin.";
    }
    internal void DismissSummon()
    {
        if (!summoned) return;
        summoned = false; awaySince = DateTime.UtcNow;
        UpdateVisibility();
    }
    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape && summoned) { DismissSummon(); e.Handled = true; }
        base.OnPreviewKeyDown(e);
    }
    private void RaiseSummonedDock()
    {
        // Reassert native z-order even when WPF Topmost already equals true.
        SetWindowPos(new System.Windows.Interop.WindowInteropHelper(this).Handle, new nint(-1), 0, 0, 0, 0, 0x0013 | 0x0040);
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    private void SetCollapsed(bool hide)
    {
        collapsed = hide; row.Visibility = hide ? Visibility.Hidden : Visibility.Visible;
        Frame.Padding = hide ? new(0) : new(8, 6, 8, 6); Height = hide ? 6 : expandedHeight; Reposition();
    }
    private void ShortcutsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Rebuild();
    public void UpdateWindows(IReadOnlyList<AppWindow> windows)
    {
        running = windows;
        var next = string.Join("|", windows.OrderBy(w => w.Handle).Select(w => $"{w.Handle}:{w.Title}:{w.Executable}"));
        if (next != signature) { rebuildPending = true; }
        if (rebuildPending && !menuOpen && !IsKeyboardFocusWithin && !IsDragging && !IsMouseOver) { signature = next; Rebuild(); }
    }
    public Func<Shortcut, bool>? Pin { get; set; }
    public Action? AddApps { get; set; }
    private readonly List<Button> items = [];
    private int page, pages = 1;
    private static IEnumerable<Shortcut> DroppedApps(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] files ? files.Where(LaunchPolicy.IsAllowed).Take(Settings.MaxShortcuts).Select(f => new Shortcut(Path.GetFileNameWithoutExtension(f), f)) : [];
    private int PerPage => Math.Max(4, (int)(((vm.Monitors.Count > 0 ? vm.Monitors.Min(m => m.Width / m.Scale) : 1280) * .85 - 160) / (vm.IconSize + 12)));
    private void ShowPage(int next) { var clamped = Math.Clamp(next, 0, pages - 1); if (clamped == page) return; page = clamped; Rebuild(); }
    private void AddMenu(Button anchor)
    {
        var menu = new ContextMenu { PlacementTarget = anchor, Placement = System.Windows.Controls.Primitives.PlacementMode.Top };
        Item(menu, "Başlatıcıdan uygulama ekle", () => AddApps?.Invoke(), AddApps is not null);
        Item(menu, "Dosyadan ekle (.exe / .lnk)…", () =>
        {
            var picker = new Microsoft.Win32.OpenFileDialog { Filter = "Uygulama veya kısayol|*.exe;*.lnk", Multiselect = true, CheckFileExists = true, DereferenceLinks = false, InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms) };
            if (picker.ShowDialog(this) == true) foreach (var file in picker.FileNames.Where(LaunchPolicy.IsAllowed)) Pin?.Invoke(new(Path.GetFileNameWithoutExtension(file), file));
        }, Pin is not null);
        menu.Items.Add(new Separator());
        Item(menu, "Dock ayarları", settings);
        menu.Opened += (_, _) => menuOpen = true; menu.Closed += (_, _) => { menuOpen = false; awaySince = DateTime.UtcNow; };
        menu.IsOpen = true;
    }
    public void Rebuild()
    {
        if (closed) return;
        rebuildPending = false;
        row.Children.Clear(); items.Clear();
        var handle = new TextBlock { Text = "", FontFamily = new FontFamily(IconFont), FontSize = 14, Width = 18, Opacity = .6, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center }; handle.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); MakeDragHandle(handle); row.Children.Add(handle);
        IReadOnlyList<AppWindow> available = vm.Settings.ShowRunningApps ? running : [];
        var consumed = new HashSet<nint>();
        foreach (var shortcut in vm.Shortcuts)
        {
            var target = targets.GetValueOrDefault(shortcut.Target, shortcut.Target);
            var matched = available.Where(w => string.Equals(w.Executable, target, StringComparison.OrdinalIgnoreCase) && !consumed.Contains(w.Handle)).OrderBy(w => w.Handle).ToList();
            if (!vm.Settings.GroupWindows) matched = matched.Take(1).ToList();
            foreach (var w in matched) consumed.Add(w.Handle);
            AddButton(shortcut.Name, shortcut.Target, matched, shortcut);
        }
        foreach (var group in WindowGrouping.Build(available.Where(w => !consumed.Contains(w.Handle)), vm.Settings.GroupWindows))
        {
            var first = group.Windows[0]; AddButton(first.Title, first.Executable, group.Windows, null);
        }
        var plus = new TextBlock { Text = "", FontFamily = new FontFamily(IconFont), FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; plus.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        var perPage = PerPage; pages = Math.Max(1, (items.Count + perPage - 1) / perPage); page = Math.Clamp(page, 0, pages - 1);
        foreach (var item in items.Skip(page * perPage).Take(perPage)) row.Children.Add(item);
        if (pages > 1)
        {
            var pager = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new(4, 0, 2, 0) };
            for (int i = 0; i < pages; i++)
            {
                var number = i; var label = new TextBlock { Text = (i + 1).ToString(), FontSize = 11, FontWeight = i == page ? FontWeights.SemiBold : FontWeights.Normal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                label.SetResourceReference(TextBlock.ForegroundProperty, i == page ? "Accent" : "Muted");
                var dot = new Button { Content = label, Width = 22, Height = 22, Margin = new(1, 0, 1, 0), Style = (Style)FindResource("DockTile"), ToolTip = $"Sayfa {i + 1} / {pages} · tekerlekle de geçebilirsin" };
                System.Windows.Automation.AutomationProperties.SetName(dot, $"Dock sayfası {i + 1}"); dot.Click += (_, _) => ShowPage(number); pager.Children.Add(dot);
            }
            row.Children.Add(pager);
        }
        var config = new Button { Content = plus, ToolTip = "Uygulama ekle · .exe/.lnk dosyasını dock'a sürükleyebilirsin", Width = vm.IconSize + 8, Height = vm.IconSize + 8, Margin = new(2, 0, 2, 0), Style = (Style)FindResource("DockTile") };
        System.Windows.Automation.AutomationProperties.SetName(config, "Uygulama ekle"); config.Click += (_, _) => AddMenu(config); row.Children.Add(config);
        var desired = 40 + (Math.Min(items.Count, perPage) + 1) * (vm.IconSize + 12) + (pages > 1 ? pages * 24 + 8 : 0);
        Width = Math.Max(120, Math.Min(desired, vm.Monitors.Count > 0 ? vm.Monitors.Min(m => m.Width / m.Scale) - 48 : desired)); expandedHeight = vm.IconSize + (Width < desired ? 40 : 22); Height = collapsed ? 6 : expandedHeight;
        if (IsLoaded) Reposition();
    }
    private void AddButton(string name, string? path, IReadOnlyList<AppWindow> windows, Shortcut? shortcut)
    {
        var grid = new Grid();
        var visual = new ContentControl { Content = name[..1].ToUpperInvariant(), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new(.5, .5), RenderTransform = new ScaleTransform(1, 1) }; grid.Children.Add(visual);
        // Running apps get a short accent bar under the icon, longer for several windows, like a Linux dock.
        if (windows.Count > 0) { var marker = new Border { Height = 3, Width = windows.Count > 1 ? 16 : 6, CornerRadius = new(2), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new(0, 0, 0, -3) }; marker.SetResourceReference(Border.BackgroundProperty, "Accent"); grid.Children.Add(marker); }
        var button = new Button { Content = grid, ToolTip = windows.Count > 0 ? string.Join("\n", windows.Select(w => w.Title)) : name, Width = vm.IconSize + 8, Height = vm.IconSize + 8, Padding = new(4), Margin = new(2, 0, 2, 0), FontSize = 20, Style = (Style)FindResource("DockTile") };
        System.Windows.Automation.AutomationProperties.SetName(button, windows.Count > 0 ? $"{name}, {windows.Count} açık pencere" : name + " uygulamasını aç");
        button.Click += (_, _) =>
        {
            if (windows.Count == 0) { if (shortcut is not null) launch(shortcut); return; }
            var key = shortcut?.Target ?? windows[0].GroupKey;
            var previous = windows.Any(w => w.Handle == windowService.Foreground) ? windowService.Foreground : lastActivated.GetValueOrDefault(key);
            var next = WindowGrouping.Next(windows, previous)!; Activate(next); lastActivated[key] = next.Handle;
        };
        var menu = new ContextMenu();
        foreach (var window in windows) { var item = new MenuItem { Header = window.Title }; item.Click += (_, _) => Activate(window); menu.Items.Add(item); }
        if (windows.Count > 0) menu.Items.Add(new Separator());
        if (shortcut is not null)
        {
            Item(menu, "Yeni pencere / uygulamayı aç", () => launch(shortcut));
            Item(menu, "Dock'tan kaldır", () => { vm.Remove(shortcut); persistShortcuts(); });
            Item(menu, "Sola taşı", () => { vm.MoveShortcut(shortcut, -1); persistShortcuts(); }, vm.Shortcuts.IndexOf(shortcut) > 0);
            Item(menu, "Sağa taşı", () => { vm.MoveShortcut(shortcut, 1); persistShortcuts(); }, vm.Shortcuts.IndexOf(shortcut) < vm.Shortcuts.Count - 1);
        }
        else if (path is not null && LaunchPolicy.IsAllowed(path)) Item(menu, "Dock'a sabitle", () => Pin?.Invoke(new(Path.GetFileNameWithoutExtension(path), path)), Pin is not null && vm.Shortcuts.Count < Settings.MaxShortcuts && !vm.Shortcuts.Any(s => string.Equals(s.Target, path, StringComparison.OrdinalIgnoreCase)));
        button.ContextMenu = menu;
        menu.Opened += (_, _) => menuOpen = true;
        menu.Closed += (_, _) => { menuOpen = false; awaySince = DateTime.UtcNow; Rebuild(); };
        button.MouseEnter += (_, _) => Animate(visual, vm.Settings.Magnification); button.MouseLeave += (_, _) => Animate(visual, 1);
        items.Add(button);
        if (path is not null) _ = LoadIconAsync(path, visual);
    }
    private async Task LoadIconAsync(string path, ContentControl visual)
    {
        var asset = await icons.GetAsync(path); if (closed) return;
        if (asset.Icon is not null) visual.Content = new Image { Source = asset.Icon, Width = vm.IconSize * .64, Height = vm.IconSize * .64 };
        if (asset.Target is not null && !string.Equals(path, asset.Target, StringComparison.OrdinalIgnoreCase) && !string.Equals(targets.GetValueOrDefault(path), asset.Target, StringComparison.OrdinalIgnoreCase))
        { targets[path] = asset.Target; rebuildPending = true; }
    }
    private void Animate(ContentControl visual, double scale)
    {
        if (!vm.Settings.AnimationsEnabled || !vm.Settings.HoverAnimation || !SystemParameters.ClientAreaAnimation || SystemParameters.HighContrast) scale = 1;
        var transform = (ScaleTransform)visual.RenderTransform;
        var duration = scale == 1 || !vm.Settings.AnimationsEnabled || !vm.Settings.HoverAnimation ? 0 : vm.Settings.AnimationDurationMs;
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scale, TimeSpan.FromMilliseconds(duration)));
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scale, TimeSpan.FromMilliseconds(duration)));
    }
    private void Activate(AppWindow window)
    {
        var activated = windowService.Activate(window); ActivationCompleted?.Invoke(activated);
        if (activated) { DismissSummon(); vm.Status = "Pencereye geçildi."; }
        else { vm.Status = "Windows pencereye odak vermedi veya pencere kapandı. Alt+Tab ile geçiş yapabilirsin."; settings(); }
    }
    private static void Item(ContextMenu menu, string text, Action action, bool enabled = true) { var item = new MenuItem { Header = text, IsEnabled = enabled }; item.Click += (_, _) => action(); menu.Items.Add(item); }
    private string EffectiveEdge => vm.Settings.IntegratedDesktop ? "bottom" : vm.Settings.DockEdge;
    private MonitorInfo TargetMonitor => vm.Monitors.FirstOrDefault(m => !SafeMode && m.Id == vm.Settings.Dock?.MonitorId) ?? vm.Monitors.First(m => m.Primary);
    internal bool IsCollapsed => collapsed;
    internal IReadOnlyList<Button> AppButtons => items.ToArray();
    public bool SafeMode { get; set; }
    private void Reposition()
    {
        if (vm.Monitors.Count == 0) return;
        if (SafeMode || EffectiveEdge == "floating") { base.Place(SafeMode ? null : vm.Settings.Dock, vm.Monitors, vm.SnapToGrid); return; }
        var m = TargetMonitor; var y = EffectiveEdge == "top" ? 0 : m.Height / m.Scale - Height;
        base.Place(new(m.Id, (m.Width / m.Scale - Width) / 2, y), vm.Monitors, false);
    }
    public override void Place(Placement? placement, IReadOnlyList<MonitorInfo> monitors, bool snap)
    {
        if (SafeMode || EffectiveEdge == "floating") base.Place(placement, monitors, snap); else Reposition();
    }
    private void UpdateVisibility()
    {
        if (!attached || closed || vm.Monitors.Count == 0) return;
        if (rebuildPending && !menuOpen && !IsKeyboardFocusWithin && !IsDragging && !IsMouseOver) Rebuild();
        if (summoned)
        {
            if (IsMouseOver || (IsActive && IsKeyboardFocusWithin) || menuOpen || IsDragging) summonedUntil = DateTime.UtcNow.AddSeconds(15);
            if (DateTime.UtcNow < summonedUntil) { Topmost = true; if (!IsVisible) Show(); return; }
            summoned = false;
        }
        var full = windowService.IsFullscreenOn(TargetMonitor);
        if (full != fullscreenCandidate) { fullscreenCandidate = full; fullscreenCandidateSince = DateTime.UtcNow; }
        if (full != fullscreen && DateTime.UtcNow - fullscreenCandidateSince >= TimeSpan.FromMilliseconds(full ? 300 : 450))
        { fullscreen = full; if (full) Hide(); else Show(); }
        full = fullscreen;
        Topmost = !SafeMode && !full && (vm.Settings.DockOnTop || (vm.Settings.DockAutoHide && EffectiveEdge != "floating"));
        if (full) { if (IsVisible) Hide(); return; }
        if (!IsVisible) Show();
        if (IsMouseOver || IsKeyboardFocusWithin || menuOpen || IsDragging) awaySince = DateTime.UtcNow;
        var hide = !SafeMode && DockVisibilityPolicy.Collapse(vm.Settings.DockAutoHide, EffectiveEdge, IsMouseOver, IsKeyboardFocusWithin, menuOpen, IsDragging, DateTime.UtcNow - awaySince, vm.Settings.DockHideDelayMs);
        if (hide == collapsed) return;
        SetCollapsed(hide);
    }
}

