using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
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
    private readonly DispatcherTimer previewDelay = new() { Interval = TimeSpan.FromMilliseconds(380) };
    private DockFlyout? flyout;
    private Action? pendingPreview;
    private bool reordering, suppressClick;
    private long binItems;
    private int binTicks;
    private readonly DispatcherTimer visibilityTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    public DockWindow(DesktopViewModel vm, Action<Shortcut> launch, Action settings, IWindowService windowService, ShellIconService icons, Action persistShortcuts) : base("Dock", Glass.Wanted(vm.Settings))
    {
        this.vm = vm; this.launch = launch; this.settings = settings; this.windowService = windowService; this.icons = icons;
        this.persistShortcuts = persistShortcuts;
        Frame.Padding = new(8, 6, 8, 6); Frame.Child = row;
        // Drop .exe/.lnk files from Explorer or the desktop to pin them, like the Windows taskbar.
        AllowDrop = true;
        DragOver += (_, e) => { e.Effects = DroppedApps(e).Any() ? DragDropEffects.Link : DragDropEffects.None; e.Handled = true; };
        Drop += (_, e) => { foreach (var app in DroppedApps(e)) Pin?.Invoke(app); e.Handled = true; };
        row.MouseMove += (_, e) => Magnify(e.GetPosition(row).X); row.MouseLeave += (_, _) => Magnify(null);
        PreviewMouseWheel += (_, e) => { if (pages > 1) { ShowPage(page + (e.Delta < 0 ? 1 : -1)); e.Handled = true; } };
        vm.Shortcuts.CollectionChanged += ShortcutsChanged;
        SourceInitialized += (_, _) => attached = true;
        Closed += (_, _) => { closed = true; visibilityTimer.Stop(); previewDelay.Stop(); CloseFlyout(); vm.Shortcuts.CollectionChanged -= ShortcutsChanged; };
        previewDelay.Tick += (_, _) => { previewDelay.Stop(); pendingPreview?.Invoke(); pendingPreview = null; };
        _ = RefreshRecycleBinAsync();
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
        if (rebuildPending && flyout is null && !menuOpen && !IsKeyboardFocusWithin && !IsDragging && !IsMouseOver) { signature = next; Rebuild(); }
    }
    public Func<Shortcut, bool>? Pin { get; set; }
    public Action? AddApps { get; set; }
    private readonly List<Button> items = [];
    private readonly Dictionary<Button, ContentControl> visuals = [];
    // macOS-style wave: the icon under the pointer grows and rises most, and its neighbours follow on a gaussian falloff.
    private void Magnify(double? pointer)
    {
        foreach (var (button, visual) in visuals)
        {
            if (!button.IsVisible) continue;
            double scale = 1.0, lift = 0;
            if (pointer is { } x)
            {
                var center = button.TranslatePoint(new Point(button.ActualWidth / 2, 0), row).X;
                (scale, lift) = DockHover.At((x - center) / (vm.IconSize + 12), vm.Settings.Magnification);
            }
            Animate(visual, scale, lift);
        }
    }
    private int page, pages = 1;
    private static IEnumerable<Shortcut> DroppedApps(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] files ? files.Where(f => LaunchPolicy.IsAllowed(f) || Directory.Exists(f)).Take(Settings.MaxShortcuts).Select(f => Directory.Exists(f) ? new Shortcut(new DirectoryInfo(f).Name, f.TrimEnd('\\') + '\\') : new Shortcut(Path.GetFileNameWithoutExtension(f), f)) : [];
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
        Item(menu, "Klasör ekle…", () =>
        {
            var picker = new Microsoft.Win32.OpenFolderDialog { Title = "Dock'a eklenecek klasör" };
            if (picker.ShowDialog(this) == true) Pin?.Invoke(new(new DirectoryInfo(picker.FolderName).Name, picker.FolderName.TrimEnd('\\') + '\\'));
        }, Pin is not null);
        menu.Items.Add(new Separator());
        Item(menu, "Dock ayarları", settings);
        menu.Opened += (_, _) => menuOpen = true; menu.Closed += (_, _) => { menuOpen = false; awaySince = DateTime.UtcNow; };
        menu.IsOpen = true;
    }
    public void Rebuild()
    {
        if (closed) return;
        if (reordering) { rebuildPending = true; return; }
        rebuildPending = false; CloseFlyout();
        row.Children.Clear(); items.Clear(); visuals.Clear();
        var handle = new TextBlock { Text = "", FontFamily = new FontFamily(IconFont), FontSize = 14, Width = 18, Opacity = .6, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center }; handle.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); MakeDragHandle(handle); row.Children.Add(handle);
        IReadOnlyList<AppWindow> available = vm.Settings.ShowRunningApps ? running : [];
        var consumed = new HashSet<nint>();
        // Exact paths claim their windows first, so looser name rules can never take a window from the icon it really belongs to.
        var exact = available.Where(w => vm.Shortcuts.Any(s => DockMatch.Exact(targets.GetValueOrDefault(s.Target, s.Target), w.Executable))).Select(w => w.Handle).ToHashSet();
        foreach (var shortcut in vm.Shortcuts)
        {
            var target = targets.GetValueOrDefault(shortcut.Target, shortcut.Target);
            var matched = available.Where(w => !consumed.Contains(w.Handle) && (DockMatch.Exact(target, w.Executable) || (!exact.Contains(w.Handle) && DockMatch.Likely(shortcut.Name, target, w.Executable)))).OrderBy(w => w.Handle).ToList();
            if (!vm.Settings.GroupWindows) matched = matched.Take(1).ToList();
            foreach (var w in matched) consumed.Add(w.Handle);
            AddButton(shortcut.Name, LaunchPolicy.IsFolderPin(shortcut.Target) ? shortcut.Target.TrimEnd('\\') : shortcut.Target, matched, shortcut);
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
        var bin = vm.Settings.DockRecycleBin && !SafeMode ? RecycleTile() : null;
        if (bin is not null) row.Children.Add(bin);
        var config = new Button { Content = plus, ToolTip = "Uygulama ekle · .exe/.lnk dosyasını dock'a sürükleyebilirsin", Width = vm.IconSize + 8, Height = vm.IconSize + 8, Margin = new(2, 0, 2, 0), Style = (Style)FindResource("DockTile") };
        System.Windows.Automation.AutomationProperties.SetName(config, "Uygulama ekle"); config.Click += (_, _) => AddMenu(config); row.Children.Add(config);
        var desired = 40 + (Math.Min(items.Count, perPage) + 1 + (bin is null ? 0 : 1)) * (vm.IconSize + 12) + (pages > 1 ? pages * 24 + 8 : 0);
        Width = Math.Max(120, Math.Min(desired, vm.Monitors.Count > 0 ? vm.Monitors.Min(m => m.Width / m.Scale) - 48 : desired)); expandedHeight = vm.IconSize + (Width < desired ? 40 : 22); Height = collapsed ? 6 : expandedHeight;
        if (IsLoaded) Reposition();
    }
    private void AddButton(string name, string? path, IReadOnlyList<AppWindow> windows, Shortcut? shortcut)
    {
        var grid = new Grid();
        var visual = new ContentControl { Content = name[..1].ToUpperInvariant(), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new(.5, .5), RenderTransform = new TransformGroup { Children = { new ScaleTransform(1, 1), new TranslateTransform() } } }; grid.Children.Add(visual);
        // Running apps get a short accent bar under the icon, longer for several windows, like a Linux dock.
        if (windows.Count > 0) { var marker = new Border { Height = 3, Width = windows.Count > 1 ? 16 : 6, CornerRadius = new(2), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new(0, 0, 0, -3) }; marker.SetResourceReference(Border.BackgroundProperty, "Accent"); grid.Children.Add(marker); }
        var button = new Button { Content = grid, ToolTip = windows.Count == 0 ? name : vm.Settings.DockPreviews ? null : string.Join("\n", windows.Select(w => w.Title)), Width = vm.IconSize + 8, Height = vm.IconSize + 8, Padding = new(4), Margin = new(2, 0, 2, 0), FontSize = 20, Style = (Style)FindResource("DockTile") };
        System.Windows.Automation.AutomationProperties.SetName(button, windows.Count > 0 ? $"{name}, {windows.Count} açık pencere" : name + " uygulamasını aç");
        button.Click += (_, _) =>
        {
            if (suppressClick) { suppressClick = false; return; }
            CloseFlyout();
            if (shortcut is not null && LaunchPolicy.IsFolderPin(shortcut.Target)) { ShowStack(button, shortcut); return; }
            if (windows.Count == 0) { if (shortcut is not null) { launch(shortcut); Bounce(grid); } return; }
            // A lone window that already has focus goes down, like the Windows taskbar.
            if (windows.Count == 1 && windows[0].Handle == windowService.Foreground) { WindowOps.Minimize(windows[0].Handle); return; }
            var key = shortcut?.Target ?? windows[0].GroupKey;
            var previous = windows.Any(w => w.Handle == windowService.Foreground) ? windowService.Foreground : lastActivated.GetValueOrDefault(key);
            var next = WindowGrouping.Next(windows, previous)!; Activate(next); lastActivated[key] = next.Handle;
        };
        var menu = new ContextMenu();
        foreach (var window in windows) { var item = new MenuItem { Header = window.Title }; item.Click += (_, _) => Activate(window); menu.Items.Add(item); }
        if (windows.Count > 0)
        {
            menu.Items.Add(new Separator());
            Item(menu, windows.Count > 1 ? $"Tümünü küçült ({windows.Count})" : "Küçült", () => { foreach (var window in windows) WindowOps.Minimize(window.Handle); });
            Item(menu, windows.Count > 1 ? $"Tümünü kapat ({windows.Count})" : "Kapat", () => { foreach (var window in windows) WindowOps.Close(window.Handle); });
            menu.Items.Add(new Separator());
        }
        if (shortcut is not null)
        {
            Item(menu, LaunchPolicy.IsFolderPin(shortcut.Target) ? "Klasörü aç" : "Yeni pencere / uygulamayı aç", () => { launch(shortcut); Bounce(grid); });
            Item(menu, "Dock'tan kaldır", () => { vm.Remove(shortcut); persistShortcuts(); });
            Item(menu, "Sola taşı", () => { vm.MoveShortcut(shortcut, -1); persistShortcuts(); }, vm.Shortcuts.IndexOf(shortcut) > 0);
            Item(menu, "Sağa taşı", () => { vm.MoveShortcut(shortcut, 1); persistShortcuts(); }, vm.Shortcuts.IndexOf(shortcut) < vm.Shortcuts.Count - 1);
        }
        else if (path is not null && LaunchPolicy.IsAllowed(path)) Item(menu, "Dock'a sabitle", () => Pin?.Invoke(new(Path.GetFileNameWithoutExtension(path), path)), Pin is not null && vm.Shortcuts.Count < Settings.MaxShortcuts && !vm.Shortcuts.Any(s => string.Equals(s.Target, path, StringComparison.OrdinalIgnoreCase)));
        button.ContextMenu = menu;
        if (windows.Count > 0 && vm.Settings.DockPreviews)
        {
            button.MouseEnter += (_, _) => { if (flyout?.Anchor == button) { flyout.KeepOpen(); return; } pendingPreview = () => ShowPreview(button, windows); previewDelay.Stop(); previewDelay.Start(); };
            button.MouseLeave += (_, _) => { pendingPreview = null; previewDelay.Stop(); flyout?.CloseSoon(); };
        }
        if (shortcut is not null) MakeReorderable(button, shortcut);
        var recentAdded = false;
        menu.Opened += async (_, _) =>
        {
            menuOpen = true; CloseFlyout(); pendingPreview = null;
            if (recentAdded || path is null || (shortcut is not null && LaunchPolicy.IsFolderPin(shortcut.Target)) || LaunchPolicy.IsPackagedApp(path)) return;
            recentAdded = true;
            // Recent files of the types this app opens by default, the closest thing to a jump list with documented APIs.
            var recent = await RecentFiles.ForAppAsync(targets.GetValueOrDefault(path, path));
            if (recent.Count == 0 || !menu.IsOpen) return;
            var list = new MenuItem { Header = "Son dosyalar" };
            foreach (var file in recent) Item(list, file.Name, () => { try { RecentFiles.Open(file.Path); } catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { vm.Status = file.Name + " açılamadı."; } });
            menu.Items.Insert(0, new Separator()); menu.Items.Insert(0, list);
        };
        menu.Closed += (_, _) => { menuOpen = false; awaySince = DateTime.UtcNow; Rebuild(); };
        visuals[button] = visual;
        items.Add(button);
        if (path is not null) _ = LoadIconAsync(path, visual);
    }
    // ---- Reordering ----
    // Dragging a pinned icon slides its neighbours out of the way and drops it into the gap; the list changes only on release.
    private void MakeReorderable(Button button, Shortcut shortcut)
    {
        Point origin = default; var pressed = false; var slot = vm.IconSize + 12d; var target = 0; var from = 0;
        button.PreviewMouseLeftButtonDown += (_, e) => { origin = e.GetPosition(row); pressed = true; };
        button.PreviewMouseMove += (_, e) =>
        {
            if (!pressed || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) { pressed = false; return; }
            var dx = e.GetPosition(row).X - origin.X;
            if (!reordering)
            {
                if (Math.Abs(dx) < 8) return;
                reordering = true; from = vm.Shortcuts.IndexOf(shortcut); target = from; if (from < 0) { reordering = false; pressed = false; return; }
                button.CaptureMouse(); CloseFlyout(); pendingPreview = null; previewDelay.Stop(); Magnify(null); Panel.SetZIndex(button, 10);
                button.RenderTransform = new TranslateTransform();
            }
            ((TranslateTransform)button.RenderTransform).X = dx;
            var perPage = PerPage; var first = page * perPage; var last = Math.Min(first + perPage, vm.Shortcuts.Count) - 1;
            var next = Math.Clamp(from + (int)Math.Round(dx / slot), first, Math.Max(first, last));
            if (next == target) return; target = next;
            for (var i = first; i <= last; i++)
            {
                if (i == from || i >= items.Count) continue;
                var shift = from < target && i > from && i <= target ? -slot : from > target && i < from && i >= target ? slot : 0;
                if (items[i].RenderTransform is not TranslateTransform slide) items[i].RenderTransform = slide = new TranslateTransform();
                Motion.To(slide, TranslateTransform.XProperty, shift, Motion.Enabled(vm) ? 140 : 0);
            }
        };
        button.PreviewMouseLeftButtonUp += (_, _) =>
        {
            pressed = false; if (!reordering) return;
            button.ReleaseMouseCapture(); reordering = false; suppressClick = true;
            if (target != from) { vm.MoveShortcutTo(shortcut, target); persistShortcuts(); } else Rebuild();
            Dispatcher.BeginInvoke(() => suppressClick = false, DispatcherPriority.Input);
        };
    }
    private void Bounce(UIElement icon)
    {
        if (!Motion.Enabled(vm)) return;
        var lift = new TranslateTransform(); icon.RenderTransform = lift;
        var bounce = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(620) };
        foreach (var (value, at) in new[] { (-9d, .2), (0d, .45), (-4d, .68), (0d, 1d) }) bounce.KeyFrames.Add(new EasingDoubleKeyFrame(value, KeyTime.FromPercent(at), new SineEase { EasingMode = EasingMode.EaseInOut }));
        lift.BeginAnimation(TranslateTransform.YProperty, bounce);
    }

    // ---- Flyouts ----
    private void CloseFlyout() { var open = flyout; flyout = null; open?.Close(); }
    private DockFlyout NewFlyout(Button anchor)
    {
        CloseFlyout();
        var created = new DockFlyout(anchor, icons, ThemePresets.Resolve(vm.Settings), EffectiveEdge != "top");
        created.Closed += (_, _) => { if (flyout == created) flyout = null; awaySince = DateTime.UtcNow; };
        return flyout = created;
    }
    private void ShowPreview(Button anchor, IReadOnlyList<AppWindow> windows)
    {
        if (closed || !anchor.IsMouseOver || menuOpen || reordering || IsDragging || !anchor.IsVisible) return;
        try { NewFlyout(anchor).ShowWindows(windows, Activate, window => WindowOps.Close(window.Handle)); }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { CloseFlyout(); vm.Status = "Pencere önizlemesi açılamadı."; }
    }
    private void ShowStack(Button anchor, Shortcut folder)
    {
        var path = folder.Target.TrimEnd('\\');
        if (!Directory.Exists(path)) { vm.Status = folder.Name + " klasörü bulunamadı."; return; }
        NewFlyout(anchor).ShowFolder(folder.Name, path, item =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item) { UseShellExecute = true }); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { vm.Status = "Açılamadı: " + Path.GetFileName(item); }
        });
    }

    // ---- Recycle Bin ----
    private Button RecycleTile()
    {
        var grid = new Grid();
        var glyph = new TextBlock { Text = "\uE74D", FontFamily = new FontFamily(IconFont), FontSize = vm.IconSize * .42, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; glyph.SetResourceReference(TextBlock.ForegroundProperty, "Text"); grid.Children.Add(glyph);
        if (binItems > 0) { var dot = new Border { Width = 7, Height = 7, CornerRadius = new(4), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 4, 4, 0) }; dot.SetResourceReference(Border.BackgroundProperty, "Accent"); grid.Children.Add(dot); }
        var description = binItems > 0 ? $"Geri dönüşüm kutusu · {binItems} öğe" : "Geri dönüşüm kutusu · boş";
        var tile = new Button { Content = grid, ToolTip = description + " · dosyaları buraya sürükleyebilirsin", Width = vm.IconSize + 8, Height = vm.IconSize + 8, Margin = new(2, 0, 2, 0), Style = (Style)FindResource("DockTile"), AllowDrop = true };
        System.Windows.Automation.AutomationProperties.SetName(tile, description);
        void Open() { try { ShellFiles.OpenRecycleBin(); } catch (System.ComponentModel.Win32Exception) { vm.Status = "Geri dönüşüm kutusu açılamadı."; } }
        tile.Click += (_, _) => Open();
        tile.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; };
        tile.Drop += async (_, e) =>
        {
            e.Handled = true; if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
            vm.Status = await ShellFiles.RecycleAsync(files, new System.Windows.Interop.WindowInteropHelper(this).Handle) ? $"{files.Length} öğe geri dönüşüm kutusuna taşındı." : "Silme iptal edildi veya tamamlanamadı.";
            _ = RefreshRecycleBinAsync();
        };
        var menu = new ContextMenu(); Item(menu, "Aç", Open);
        Item(menu, "Boşalt…", () => { ShellFiles.EmptyRecycleBin(new System.Windows.Interop.WindowInteropHelper(this).Handle); _ = RefreshRecycleBinAsync(); }, binItems > 0);
        menu.Opened += (_, _) => menuOpen = true; menu.Closed += (_, _) => { menuOpen = false; awaySince = DateTime.UtcNow; };
        tile.ContextMenu = menu;
        return tile;
    }
    // Asking Windows can wake a sleeping disk, so this runs off the interface thread and only every half a minute.
    private async Task RefreshRecycleBinAsync()
    {
        if (!vm.Settings.DockRecycleBin) return;
        var count = await Task.Run(ShellFiles.RecycleBinItems) ?? binItems;
        if (closed || count == binItems) return;
        binItems = count; rebuildPending = true;
    }
    private async Task LoadIconAsync(string path, ContentControl visual)
    {
        var asset = await icons.GetAsync(path); if (closed) return;
        if (asset.Icon is not null) visual.Content = new Image { Source = asset.Icon, Width = vm.IconSize * .64, Height = vm.IconSize * .64 };
        if (asset.Target is not null && !string.Equals(path, asset.Target, StringComparison.OrdinalIgnoreCase) && !string.Equals(targets.GetValueOrDefault(path), asset.Target, StringComparison.OrdinalIgnoreCase))
        { targets[path] = asset.Target; rebuildPending = true; }
    }
    private void Animate(ContentControl visual, double scale, double lift)
    {
        if (!vm.Settings.AnimationsEnabled || !vm.Settings.HoverAnimation || !SystemParameters.ClientAreaAnimation || SystemParameters.HighContrast) { scale = 1; lift = 0; }
        var parts = ((TransformGroup)visual.RenderTransform).Children; var transform = (ScaleTransform)parts[0];
        // Short, decelerating steps keep the wave glued to the pointer; the duration setting caps it.
        var duration = Math.Min(160, vm.Settings.AnimationDurationMs);
        Motion.To(transform, ScaleTransform.ScaleXProperty, scale, duration);
        Motion.To(transform, ScaleTransform.ScaleYProperty, scale, duration);
        Motion.To(parts[1], TranslateTransform.YProperty, -lift, duration);
    }
    private void Activate(AppWindow window)
    {
        var activated = windowService.Activate(window); ActivationCompleted?.Invoke(activated);
        if (activated) { DismissSummon(); vm.Status = "Pencereye geçildi."; }
        else { vm.Status = "Windows pencereye odak vermedi veya pencere kapandı. Alt+Tab ile geçiş yapabilirsin."; settings(); }
    }
    private static void Item(ItemsControl menu, string text, Action action, bool enabled = true) { var item = new MenuItem { Header = text, IsEnabled = enabled }; item.Click += (_, _) => action(); menu.Items.Add(item); }
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
        if (++binTicks >= 200) { binTicks = 0; _ = RefreshRecycleBinAsync(); }
        if (rebuildPending && flyout is null && !menuOpen && !IsKeyboardFocusWithin && !IsDragging && !IsMouseOver) Rebuild();
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
        if (full) { CloseFlyout(); if (IsVisible) Hide(); return; }
        if (!IsVisible) Show();
        if (IsMouseOver || IsKeyboardFocusWithin || menuOpen || IsDragging || flyout is not null) awaySince = DateTime.UtcNow;
        var hide = !SafeMode && DockVisibilityPolicy.Collapse(vm.Settings.DockAutoHide, EffectiveEdge, IsMouseOver || flyout is not null, IsKeyboardFocusWithin, menuOpen, IsDragging, DateTime.UtcNow - awaySince, vm.Settings.DockHideDelayMs);
        if (hide == collapsed) return;
        SetCollapsed(hide);
    }
}

