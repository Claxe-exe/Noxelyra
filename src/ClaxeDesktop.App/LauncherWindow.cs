using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

/// <summary>
/// Full-screen overlay launcher in two styles: a centred search card (Rofi/Spotlight) or a full-screen app grid
/// (GNOME/Launchpad). Win tap, the bar's N button or the launcher shortcut open it; Esc, a click outside or focus loss close it.
/// Besides apps it finds open windows, files in the user's folders and Windows commands, calculates, and offers emoji and clipboard text.
/// </summary>
internal sealed class LauncherWindow : Window
{
    private sealed record LauncherAction(string Name, string Glyph, Action Run, string? Icon = null, bool Confirm = false, string? Font = null);
    private const string IconFont = "Segoe Fluent Icons, Segoe MDL2 Assets";
    private readonly DesktopController controller;
    private readonly TextBox search = new() { FontSize = 20, BorderThickness = new(0), Background = Brushes.Transparent, Padding = new(6, 10, 6, 10), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly ListBox results = new() { Background = Brushes.Transparent, BorderThickness = new(0) };
    private readonly TextBlock status = new() { FontSize = 12, Margin = new(4, 10, 4, 0) };
    private readonly DispatcherTimer debounce = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly ShellIconService icons = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly Border card = new() { CornerRadius = new(22), BorderThickness = new(1), Padding = new(18), RenderTransformOrigin = new(.5, .3) };
    private readonly Grid stage = new();
    private readonly List<LauncherAction> builtins;
    private IReadOnlyList<Shortcut> apps = [];
    private bool grid, closed, leaving;
    private int generation;
    private nint previous;
    private LauncherAction? armed;
    private DateTime armedUntil;
    private Task<IReadOnlyList<string>>? fileIndex;
    internal int ApplicationCount => apps.Count;
    internal IReadOnlyList<Shortcut> InstalledApplications => apps;
    internal TextBox Search => search;
    internal ListBox Results => results;
    public LauncherWindow(DesktopController controller)
    {
        this.controller = controller;
        AltTab.Hide(this); Title = "Noxelyra · Uygulamalar"; WindowStyle = WindowStyle.None; AllowsTransparency = true; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; Topmost = true; Background = Brushes.Transparent; SetResourceReference(ForegroundProperty, "Text");
        builtins =
        [
            new("Hızlı ayarlar", "", controller.ShowControls), new("Kontrol merkezi", "", controller.ShowFullControls),
            new("Klavye ve kısayollar", "", controller.ShowKeyboard), new("Hafifletme", "", controller.ShowLighten),
            new("Widget atölyesi", "", controller.Widgets.ShowEditor), new("Ayarlar", "", controller.ShowHub), new("Kısayollar ve ipuçları", "", controller.ShowShortcuts),
            new("Noxelyra masaüstü", "", controller.ShowDesktopSpace), new("Windows uygulamaları", "", () => controller.OpenSystem("apps")),
            new("Ekranı kilitle", "\uE72E", () => controller.Power("lock")), new("Uyku", "\uE708", () => controller.Power("sleep")),
            new("Yeniden başlat", "\uE777", () => controller.Power("restart"), Confirm: true), new("Bilgisayarı kapat", "\uE7E8", () => controller.Power("shutdown"), Confirm: true),
            new("Ekran alıntısı", "\uE722", () => Start("ms-screenclip:")), new("Görev Yöneticisi", "\uE9D9", () => Start("taskmgr.exe")), new("Dosya Gezgini", "\uE8B7", () => Start("explorer.exe")),
            new("Windows Ayarları", "\uE713", () => Start("ms-settings:")), new("Geri dönüşüm kutusu", "\uE74D", ShellFiles.OpenRecycleBin), new("Son kullanılanları temizle", "\uE74D", () => { controller.ClearLauncherHistory(); Render(); }),
            new("Tema galerisi", "\uE790", controller.ShowThemeGallery),
        ];
        // Dimmed backdrop: clicking it closes the launcher.
        var dim = new Border(); dim.SetResourceReference(Border.BackgroundProperty, "Canvas"); dim.Opacity = .72; dim.MouseLeftButtonDown += (_, _) => Leave();
        stage.Children.Add(dim); Content = stage;
        card.SetResourceReference(Border.BorderBrushProperty, "Line"); card.SetResourceReference(Border.BackgroundProperty, "Surface");
        card.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 40, ShadowDepth = 8, Opacity = .45, Color = Colors.Black };
        var body = new DockPanel(); card.Child = body;
        var field = new Border { CornerRadius = new(14), BorderThickness = new(1), Padding = new(12, 0, 12, 0), Margin = new(0, 0, 0, 12) };
        field.SetResourceReference(Border.BackgroundProperty, "Card"); field.SetResourceReference(Border.BorderBrushProperty, "Line");
        var fieldRow = new DockPanel(); field.Child = fieldRow;
        var glass = new TextBlock { Text = "", FontFamily = new FontFamily(IconFont), FontSize = 18, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 8, 0) };
        glass.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); DockPanel.SetDock(glass, Dock.Left); fieldRow.Children.Add(glass);
        var hint = new TextBlock { Text = "Uygulama, dosya, komut veya hesap", FontSize = 20, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false, Margin = new(8, 0, 0, 0) };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        var input = new Grid(); input.Children.Add(hint); input.Children.Add(search); fieldRow.Children.Add(input);
        search.SetResourceReference(ForegroundProperty, "Text"); search.SetResourceReference(TextBox.CaretBrushProperty, "Accent");
        System.Windows.Automation.AutomationProperties.SetName(search, "Uygulama, dosya, komut veya hesap ara");
        DockPanel.SetDock(field, Dock.Top); body.Children.Add(field);
        status.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); DockPanel.SetDock(status, Dock.Bottom); body.Children.Add(status);
        ScrollViewer.SetHorizontalScrollBarVisibility(results, ScrollBarVisibility.Disabled);
        body.Children.Add(results);
        stage.Children.Add(card);
        search.TextChanged += (_, _) => { hint.Visibility = search.Text.Length == 0 ? Visibility.Visible : Visibility.Hidden; debounce.Stop(); debounce.Start(); };
        debounce.Tick += (_, _) => { debounce.Stop(); Render(); };
        results.PreviewMouseLeftButtonUp += (_, e) => { if (ItemsControl.ContainerFromElement(results, (DependencyObject)e.OriginalSource) is ListBoxItem) RunSelected(); };
        search.PreviewKeyDown += (_, e) => Navigate(e);
        results.PreviewKeyDown += (_, e) => Navigate(e);
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Leave(); e.Handled = true; } };
        Deactivated += (_, _) => { if (IsVisible) Leave(); };
        Loaded += async (_, _) => await LoadAsync();
        Closed += (_, _) => { closed = true; lifetime.Cancel(); debounce.Stop(); icons.Dispose(); lifetime.Dispose(); };
    }
    /// <summary>Opens over the monitor under the pointer in the chosen style.</summary>
    internal void ShowOverlay()
    {
        grid = controller.Keyboard.LauncherStyle == "grid"; previous = WindowOps.Foreground; armed = null;
        var monitor = controller.MonitorUnderPointer();
        Left = monitor.X / monitor.Scale; Top = monitor.Y / monitor.Scale; Width = monitor.Width / monitor.Scale; Height = monitor.Height / monitor.Scale;
        card.Width = grid ? Math.Min(Width - 160, 1180) : 660; card.MaxHeight = grid ? Height - 120 : Math.Min(Height * .7, 620);
        card.VerticalAlignment = grid ? VerticalAlignment.Center : VerticalAlignment.Top; card.Margin = grid ? new(0) : new(0, Height * .16, 0, 0);
        results.Height = double.NaN;
        leaving = false; search.Clear(); Render();
        if (!IsVisible) Show();
        WindowPlacement.Move(new WindowInteropHelper(this).Handle, monitor.X, monitor.Y);
        Activate(); search.Focus(); Keyboard.Focus(search);
        if (Motion.Enabled(controller.ViewModel))
        {
            var scale = new ScaleTransform(.96, .96); card.RenderTransform = scale; stage.Opacity = 0;
            Motion.To(stage, OpacityProperty, 1, 160); Motion.To(scale, ScaleTransform.ScaleXProperty, 1, 260); Motion.To(scale, ScaleTransform.ScaleYProperty, 1, 260);
        }
    }
    internal void Leave()
    {
        if (leaving || !IsVisible) return; leaving = true;
        // The file names are only needed while searching; the next search reads them fresh.
        fileIndex = null;
        if (!Motion.Enabled(controller.ViewModel)) { Hide(); return; }
        Motion.To(stage, OpacityProperty, 0, 120, () => { Hide(); stage.BeginAnimation(OpacityProperty, null); stage.Opacity = 1; });
    }
    private void Navigate(KeyEventArgs e)
    {
        var columns = grid ? Math.Max(1, (int)((card.ActualWidth - 36) / 150)) : 1;
        int step = e.Key switch { Key.Down => columns, Key.Up => -columns, Key.Right when grid && !search.IsKeyboardFocused => 1, Key.Left when grid && !search.IsKeyboardFocused => -1, Key.Tab => Keyboard.Modifiers == ModifierKeys.Shift ? -1 : 1, _ => 0 };
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control && results.SelectedItem is ListBoxItem { Tag: Shortcut elevated } && !LaunchPolicy.IsPackagedApp(elevated.Target)) { RunElevated(elevated); Leave(); e.Handled = true; return; }
        if (e.Key == Key.Enter) { RunSelected(); e.Handled = true; return; }
        if (step == 0 || results.Items.Count == 0) return;
        results.SelectedIndex = Math.Clamp(results.SelectedIndex + step, 0, results.Items.Count - 1);
        results.ScrollIntoView(results.SelectedItem); e.Handled = true;
    }
    private async Task LoadAsync()
    {
        status.Text = "Windows Başlat menüsü okunuyor…";
        try { var found = await Task.Run(() => new WindowsApplicationIndex().Read(lifetime.Token), lifetime.Token); if (!closed) { apps = found; Render(); } }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!closed) status.Text = "Uygulama dizini okunamadı. Sabitlenmiş kısayollar kullanılabilir."; controller.Log.Write("application-index-failed", ex); }
    }
    internal void Render()
    {
        if (closed) return;
        var current = ++generation; results.Items.Clear();
        var factory = new FrameworkElementFactory(grid ? typeof(WrapPanel) : typeof(StackPanel)); results.ItemsPanel = new ItemsPanelTemplate(factory);
        var query = search.Text.Trim(); armed = null;
        if (query.StartsWith(':') || FuzzyMatcher.Fold(query) == "emoji") { RenderEmoji(query.StartsWith(':') ? query[1..] : "", current); return; }
        if (FuzzyMatcher.Fold(query) == "pano") { RenderClipboard(current); return; }
        var everything = controller.ViewModel.Shortcuts.Concat(apps).DistinctBy(a => a.Target, StringComparer.OrdinalIgnoreCase).ToArray(); var now = DateTimeOffset.Now;
        if (Calculator.Evaluate(query) is { } value)
        {
            var answer = Calculator.Format(value);
            results.Items.Add(Row("= " + answer, "Enter: kopyala", new LauncherAction(answer, "\uE8EF", () => Copy(answer)), null, current));
        }
        if (query.Length == 0)
        {
            // Empty search: what is used most, then pinned apps, then the rest alphabetically.
            var recent = controller.LauncherUsage.Top(6, now).Select(t => everything.FirstOrDefault(a => string.Equals(a.Target, t, StringComparison.OrdinalIgnoreCase))).OfType<Shortcut>().ToArray();
            if (grid) foreach (var action in builtins) results.Items.Add(Row(action.Name, "Noxelyra", action, null, current));
            foreach (var app in recent) results.Items.Add(Row(app.Name, "Son kullanılan", app, app.Target, current));
            _ = AddRecentFilesAsync(results.Items.Count, current);
            foreach (var app in everything.Except(recent).OrderBy(a => controller.IsPinned(a.Target) ? 0 : 1).ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).Take(grid ? 120 : 40))
                results.Items.Add(Row(app.Name, controller.IsPinned(app.Target) ? "Dock'ta" : "Uygulama", app, app.Target, current));
        }
        else
        {
            // Apps, windows and commands compete on one list: match quality first, then how much it is used.
            var ranked = new List<(double Score, ListBoxItem Item)>();
            foreach (var app in everything) if (FuzzyMatcher.Score(app.Name, query) is { } score) ranked.Add((score + Math.Min(controller.LauncherUsage.Score(app.Target, now) * 25, 150) + (controller.IsPinned(app.Target) ? 5 : 0), Row(app.Name, controller.IsPinned(app.Target) ? "Dock'ta" : "Uygulama", app, app.Target, current)));
            foreach (var action in builtins) if (FuzzyMatcher.Score(action.Name, query) is { } score) ranked.Add((score - 1, Row(action.Name, "Noxelyra", action, null, current)));
            foreach (var window in controller.LastSnapshot) if (FuzzyMatcher.Score(window.Title, query) is >= FuzzyMatcher.Literal and var score) { var target = window; ranked.Add((score! - 2d, Row(window.Title, "Açık pencere", new LauncherAction(window.Title, "\uE8A7", () => controller.ActivateWindow(target), window.Executable), window.Executable, current))); }
            foreach (var item in ranked.OrderByDescending(r => r.Score).Take(grid ? 120 : 24)) results.Items.Add(item.Item);
            if (WebTarget.Address(query) is { } address) results.Items.Add(Row(address, "Tarayıcıda aç", new LauncherAction(address, "\uE774", () => Start(address)), null, current));
            results.Items.Add(webRow = Row($"Web'de ara: {query}", "Enter: tarayıcıda aç", new LauncherAction(query, "\uE721", () => Start(WebTarget.SearchUrl(query))), null, current));
            if (query.Length >= 3) _ = AddFilesAsync(query, current);
        }
        if (results.Items.Count > 0) results.SelectedIndex = 0;
        status.Text = query.Length == 0 ? $"{apps.Count} uygulama · 2+2 hesap · :gül emoji · pano · dosya adı" + (controller.Keyboard.SuperLauncher ? " · Win kapatır" : "") : results.Items.Count == 0 ? "Eşleşme yok." : "Enter aç · Ctrl+Enter yönetici · oklar gez · Esc kapat";
    }
    private ListBoxItem? webRow;
    private void RenderEmoji(string query, int current)
    {
        var emoji = new FontFamily("Segoe UI Emoji");
        foreach (var item in EmojiCatalog.Search(query)) results.Items.Add(Row(item.Keywords, "Enter: yapıştır", new LauncherAction(item.Glyph, item.Glyph, () => Paste(item.Glyph), Font: "Segoe UI Emoji"), null, current));
        if (results.Items.Count > 0) results.SelectedIndex = 0;
        status.Text = results.Items.Count == 0 ? "Bu adla emoji yok." : "Enter: seçtiğin yere yapıştırır · oklar gez · Esc kapat";
    }
    private void RenderClipboard(int current)
    {
        if (!controller.Keyboard.ClipboardHistory)
        {
            results.Items.Add(Row("Pano geçmişini aç", "Kopyaladıkların bellekte tutulur, diske yazılmaz", new LauncherAction("Pano geçmişini aç", "\uE8C8", () => _ = controller.SetKeyboardAsync(o => o with { ClipboardHistory = true })), null, current));
            results.SelectedIndex = 0; status.Text = "Pano geçmişi kapalı."; return;
        }
        foreach (var text in controller.ClipboardItems.Items)
        {
            var line = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)); if (line.Length > 120) line = line[..120] + "…";
            results.Items.Add(Row(line, "Enter: yapıştır", new LauncherAction(line, "\uE8C8", () => Paste(text)), null, current));
        }
        if (controller.ClipboardItems.Items.Count > 0) results.Items.Add(Row("Pano geçmişini temizle", "Noxelyra", new LauncherAction("Pano geçmişini temizle", "\uE74D", controller.ClipboardItems.Clear), null, current));
        if (results.Items.Count > 0) results.SelectedIndex = 0;
        status.Text = results.Items.Count == 0 ? "Henüz bir şey kopyalamadın." : "Enter: seçtiğin yere yapıştırır · oklar gez · Esc kapat";
    }
    private LauncherAction OpenFile(string path) => new(path, "\uE8A5", () => Start(path), path);
    private async Task AddRecentFilesAsync(int at, int current)
    {
        var files = await RecentFiles.ReadAsync();
        if (closed || generation != current) return;
        foreach (var file in files.Take(5)) results.Items.Insert(at++, Row(file.Name, "Son dosya", OpenFile(file.Path), file.Path, current));
    }
    // File names arrive after the instant results and take their place above the web row.
    private async Task AddFilesAsync(string query, int current)
    {
        try
        {
            string Home(string name) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), name);
            var roots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Home("Downloads"), Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), Environment.GetFolderPath(Environment.SpecialFolder.MyVideos) }.Where(r => r.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var token = lifetime.Token;
            fileIndex ??= Task.Run(() => FileIndex.Build(roots, 30000, 5, token), token);
            var index = await fileIndex;
            // Recent files come first and may live anywhere, not only in the user's folders.
            var recent = (await RecentFiles.ReadAsync()).Select(f => f.Path).ToArray();
            var matches = await Task.Run(() => recent.Where(p => FuzzyMatcher.Score(Path.GetFileName(p), query) >= FuzzyMatcher.Literal).Concat(index.Select(p => (Path: p, Score: FuzzyMatcher.Score(Path.GetFileName(p), query))).Where(m => m.Score >= FuzzyMatcher.Literal).OrderByDescending(m => m.Score).ThenBy(m => m.Path.Length).Select(m => m.Path)).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray(), token);
            if (closed || generation != current) return;
            var at = webRow is not null ? results.Items.IndexOf(webRow) : results.Items.Count;
            foreach (var path in matches)
            {
                var file = path;
                results.Items.Insert(at++, Row(Path.GetFileName(path), Path.GetDirectoryName(path) is { } parent ? Path.GetFileName(parent) : "Dosya", OpenFile(file), file, current));
            }
            if (results.SelectedIndex < 0 && results.Items.Count > 0) results.SelectedIndex = 0;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { controller.Log.Write("launcher-file-search-failed", ex); }
    }
    private void Start(string target)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { controller.ViewModel.Status = "Açılamadı: " + target; controller.Log.Write("launcher-start-failed", ex); }
    }
    private bool Copy(string text)
    {
        try { Clipboard.SetText(text); return true; }
        catch (System.Runtime.InteropServices.COMException) { controller.ViewModel.Status = "Pano meşgul; yeniden dene."; return false; }
    }
    // Copy, go back to the window that was active before the launcher, and press Ctrl+V there. Noxelyra's own windows only get the copy.
    private void Paste(string text)
    {
        if (!Copy(text) || !WindowOps.IsAlive(previous) || WindowOps.ProcessId(previous) == Environment.ProcessId) return;
        var target = previous; var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(170) };
        timer.Tick += (_, _) => { timer.Stop(); if (WindowOps.Focus(target)) KeyboardHook.Chord(0x11, 0x56); };
        timer.Start();
    }
    private void RunElevated(Shortcut app)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(app.Target) { UseShellExecute = true, Verb = "runas" }); controller.RecordLaunch(app.Target); }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { /* The user said no to the UAC prompt. */ }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { controller.ViewModel.Status = app.Name + " yönetici olarak açılamadı."; controller.Log.Write("launcher-elevate-failed", ex); }
    }
    private ListBoxItem Row(string name, string kind, object tag, string? iconPath, int current)
    {
        FrameworkElement visual;
        if (iconPath is null) { var glyph = new TextBlock { Text = ((LauncherAction)tag).Glyph, FontFamily = new FontFamily(((LauncherAction)tag).Font ?? IconFont), FontSize = grid ? 30 : 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; glyph.SetResourceReference(TextBlock.ForegroundProperty, "Accent"); visual = glyph; }
        else { var image = new Image { Width = grid ? 48 : 30, Height = grid ? 48 : 30 }; _ = SetIconAsync(image, iconPath, current); visual = image; }
        var label = new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, FontSize = 14 };
        FrameworkElement content;
        if (grid)
        {
            label.TextAlignment = TextAlignment.Center; label.TextWrapping = TextWrapping.Wrap; label.MaxHeight = 38; label.FontSize = 13; label.Margin = new(0, 8, 0, 0);
            var box = new StackPanel { Width = 130, Margin = new(4, 6, 4, 6) }; box.Children.Add(new Border { Height = 52, Child = visual }); box.Children.Add(label); content = box;
        }
        else
        {
            var line = new DockPanel { Margin = new(2, 0, 2, 0) };
            var holder = new Border { Width = 34, Height = 34, Margin = new(0, 0, 12, 0), Child = visual }; DockPanel.SetDock(holder, Dock.Left); line.Children.Add(holder);
            var meta = new TextBlock { Text = kind, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new(12, 0, 0, 0) }; meta.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
            DockPanel.SetDock(meta, Dock.Right); line.Children.Add(meta); line.Children.Add(label); content = line;
        }
        var item = new ListBoxItem { Content = content, Tag = tag, ToolTip = name, Padding = new(10, 8, 10, 8) };
        System.Windows.Automation.AutomationProperties.SetName(item, name);
        if (tag is Shortcut app)
        {
            var pin = new MenuItem { Header = controller.IsPinned(app.Target) ? "Dock'tan kaldır" : "Dock'a sabitle" };
            pin.Click += (_, _) => { controller.TogglePin(app); Render(); };
            item.ContextMenu = new ContextMenu { Items = { pin } };
            if (!LaunchPolicy.IsPackagedApp(app.Target))
            {
                var admin = new MenuItem { Header = "Yönetici olarak çalıştır  ·  Ctrl+Enter" }; admin.Click += (_, _) => { RunElevated(app); Leave(); }; item.ContextMenu.Items.Add(admin);
                var where = new MenuItem { Header = "Dosya konumunu aç" };
                where.Click += (_, _) => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe") { ArgumentList = { "/select," + app.Target }, UseShellExecute = false }); Leave(); } catch (System.ComponentModel.Win32Exception ex) { controller.Log.Write("launcher-reveal-failed", ex); } };
                item.ContextMenu.Items.Add(where);
            }
        }
        return item;
    }
    private async Task SetIconAsync(Image image, string path, int current) { var asset = await icons.GetAsync(path); if (!closed && generation == current) image.Source = asset.Icon; }
    internal void RunSelected()
    {
        if (results.SelectedItem is not ListBoxItem item) return;
        if (item.Tag is LauncherAction { Confirm: true } risky && (armed != risky || DateTime.UtcNow > armedUntil))
        {
            // Restart and shutdown need a second Enter or click within four seconds.
            armed = risky; armedUntil = DateTime.UtcNow.AddSeconds(4); status.Text = $"{risky.Name}: emin misin? Onaylamak için tekrar Enter."; return;
        }
        if (item.Tag is Shortcut app) { controller.RecordLaunch(app.Target); controller.Launch(app); }
        else if (item.Tag is LauncherAction action) action.Run();
        Leave();
    }
}
