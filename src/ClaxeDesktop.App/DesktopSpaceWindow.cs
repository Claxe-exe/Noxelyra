using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
using Microsoft.Win32;
namespace ClaxeDesktop.App;

internal sealed class DesktopSpaceWindow : Window
{
    private const double CellWidth = 96, CellHeight = 104, TileWidth = 88, TileHeight = 98, Gutter = 28;
    private const string Hint = "Çift tık / Enter: aç   ·   Sürükle veya Alt+yön: taşı   ·   Sağ tık: menü   ·   Alt+K: dock   ·   Esc: Windows";
    private readonly DesktopController controller;
    private readonly Canvas icons = new() { Background = Brushes.Transparent };
    private readonly Image background = new() { Stretch = Stretch.UniformToFill, IsHitTestVisible = false };
    private readonly MediaElement video = new() { LoadedBehavior = System.Windows.Controls.MediaState.Manual, UnloadedBehavior = System.Windows.Controls.MediaState.Close, Stretch = Stretch.UniformToFill, IsMuted = true, IsHitTestVisible = false };
    private readonly TextBlock status = new() { HorizontalAlignment = HorizontalAlignment.Left, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 720, FontSize = 12, Margin = new(2, 4, 0, 0) };
    private readonly TextBlock count = new() { FontSize = 13, VerticalAlignment = VerticalAlignment.Bottom, Margin = new(10, 0, 0, 3) };
    private readonly TextBox search = new() { Width = 220, Background = Brushes.Transparent, BorderThickness = new(0), Padding = new(4, 7, 4, 7), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly ShellIconService shellIcons = new();
    private readonly DispatcherTimer saveLayout = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private readonly DispatcherTimer reflow = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly List<FileSystemWatcher> watchers = [];
    private readonly DispatcherTimer refreshDelay = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly Dictionary<string, DesktopIconPosition> positions = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<DesktopFile> files = [];
    private Grid? header;
    private double headerInset = 92;
    private Task refreshPending = Task.CompletedTask;
    private bool closed, mediaReady, playing, manualPause;
    private string? loadedVideo, loadedImage = "";
    internal bool MediaReady => mediaReady;
    internal bool Playing => playing;
    internal int ItemCount => files.Count;
    internal List<DesktopIconPosition> IconPositions => positions.Values.Take(300).ToList();
    internal DesktopSpaceWindow(DesktopController controller)
    {
        this.controller = controller;
        Title = "Noxelyra · Masaüstü alanı"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = true; ShowActivated = false;
        Background = Brushes.Black;
        var root = new Grid(); Content = root;
        root.Children.Add(background); root.Children.Add(video);
        // Soft top/bottom scrim keeps labels and the header readable on bright user wallpapers.
        var scrim = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(0, 1) };
        foreach (var (alpha, offset) in new[] { (150, 0d), (40, .16), (0, .3), (0, .78), (120, 1d) }) scrim.GradientStops.Add(new(Color.FromArgb((byte)alpha, 9, 7, 16), offset));
        root.Children.Add(new Rectangle { Fill = scrim, IsHitTestVisible = false });
        if (!SystemParameters.HighContrast) icons.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 0, Opacity = .85 };
        icons.ContextMenuOpening += (_, e) => { if (e.OriginalSource == icons) { icons.ContextMenu = SpaceMenu(); } };
        icons.MouseLeftButtonDown += (_, _) => Keyboard.ClearFocus();
        root.Children.Add(new ScrollViewer { Content = icons, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false });
        root.Children.Add(Header());
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && search.IsKeyboardFocused && search.Text.Length > 0) { search.Clear(); e.Handled = true; }
            else if (e.Key == Key.Escape) { controller.DismissDesktopSpace(); e.Handled = true; }
            else if (e.Key == Key.F5) { _ = RefreshFilesAsync(); e.Handled = true; }
            else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { search.Focus(); search.SelectAll(); e.Handled = true; }
        };
        video.MediaOpened += (_, _) => { mediaReady = true; status.Text = "Yerel video hazır · ses kapalı. Başka uygulamaya geçince oynatma bekler."; UpdateActivity(false); };
        video.MediaEnded += (_, _) => { video.Position = TimeSpan.Zero; if (playing) video.Play(); };
        video.MediaFailed += (_, _) => { mediaReady = false; playing = false; video.Close(); video.Visibility = Visibility.Hidden; status.Text = "Video açılamadı. Windows codec desteğini kontrol et veya başka MP4/WMV seç; arka plan korunuyor."; };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { video.Pause(); playing = false; } else { Place(); _ = RefreshFilesAsync(); UpdateActivity(false); } };
        saveLayout.Tick += async (_, _) => { saveLayout.Stop(); await controller.SaveDesktopIconsAsync(positions.Values.ToList()); };
        reflow.Tick += (_, _) => { reflow.Stop(); Place(); };
        refreshDelay.Tick += async (_, _) => { refreshDelay.Stop(); if (IsVisible) await RefreshFilesAsync(); };
        foreach (var rootPath in DesktopFiles.Roots)
        {
            if (!Directory.Exists(rootPath)) continue;
            try
            {
                var watcher = new FileSystemWatcher(rootPath) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName, EnableRaisingEvents = true };
                void RefreshSoon() { if (!closed) Dispatcher.BeginInvoke(() => { if (!closed) { refreshDelay.Stop(); refreshDelay.Start(); } }); }
                watcher.Created += (_, _) => RefreshSoon(); watcher.Deleted += (_, _) => RefreshSoon(); watcher.Renamed += (_, _) => RefreshSoon(); watchers.Add(watcher);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { status.Text = "Otomatik dosya yenileme açılamadı; F5 kullanabilirsin."; }
        }
        Closing += (_, e) => { if (!controller.IsExiting) { e.Cancel = true; controller.DismissDesktopSpace(); return; } foreach (var watcher in watchers) watcher.Dispose(); refreshDelay.Stop(); saveLayout.Stop(); reflow.Stop(); video.Close(); shellIcons.Dispose(); closed = true; };
        // Clicking the desktop activates this window; keep the bar, dock and widgets above it like a real desktop.
        Activated += (_, _) => controller.RaiseDesktopParts();
        LoadOptions();
    }
    private Grid Header()
    {
        header = new Grid { VerticalAlignment = VerticalAlignment.Top, Margin = new(Gutter, 18, Gutter, 0) };
        header.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var title = new StackPanel { ToolTip = Hint, VerticalAlignment = VerticalAlignment.Center };
        var line = new StackPanel { Orientation = Orientation.Horizontal };
        line.Children.Add(new TextBlock { Text = "Masaüstü", FontSize = 26, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White });
        count.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); line.Children.Add(count);
        title.Children.Add(line); status.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); title.Children.Add(status);
        header.Children.Add(title);
        var pill = new Border { CornerRadius = new(16), BorderThickness = new(1), Padding = new(8, 6, 6, 6), VerticalAlignment = VerticalAlignment.Center };
        pill.SetResourceReference(Border.BorderBrushProperty, "Line"); pill.Background = Glass();
        Grid.SetColumn(pill, 1); header.Children.Add(pill);
        var tools = new StackPanel { Orientation = Orientation.Horizontal }; pill.Child = tools;
        var field = new Grid { VerticalAlignment = VerticalAlignment.Center, Margin = new(4, 0, 6, 0) };
        var placeholder = new TextBlock { Text = "⌕  Masaüstünde ara   Ctrl+F", Margin = new(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        placeholder.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        field.Children.Add(placeholder); field.Children.Add(search);
        System.Windows.Automation.AutomationProperties.SetName(search, "Masaüstü dosyalarında ara");
        search.TextChanged += (_, _) => { placeholder.Visibility = search.Text.Length == 0 ? Visibility.Visible : Visibility.Hidden; RenderIcons(); };
        search.KeyDown += (_, e) => { if (e.Key == Key.Enter) { icons.Children.OfType<Button>().FirstOrDefault()?.Focus(); e.Handled = true; } };
        tools.Children.Add(field);
        var menu = new Button { Content = "Görünüm  ▾", Padding = new(14, 7, 14, 7), Margin = new(2), ToolTip = "Arka plan, video, ekran ve widget seçenekleri" };
        menu.Click += (_, _) => { var m = SpaceMenu(); m.PlacementTarget = menu; m.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom; m.IsOpen = true; };
        tools.Children.Add(menu);
        var back = new Button { Content = "Windows'a dön  ·  Esc", Padding = new(14, 7, 14, 7), Margin = new(2), ToolTip = "Noxelyra alanını geçici gizler; bir sonraki masaüstü ziyaretinde geri gelir" };
        back.Click += (_, _) => controller.DismissDesktopSpace(); tools.Children.Add(back);
        return header;
    }
    private Brush Glass()
    {
        var color = (Color)ColorConverter.ConvertFromString(ThemePresets.Resolve(controller.ViewModel.Settings).Surface); color.A = 0xD8;
        return SystemParameters.HighContrast ? SystemColors.WindowBrush : new SolidColorBrush(color);
    }
    private ContextMenu SpaceMenu()
    {
        var options = controller.ViewModel.Settings.DesktopSpace; var menu = new ContextMenu();
        Item(menu, "Arka plan resmi seç…", PickImage);
        Item(menu, "Yerel video seç…", PickVideo);
        Item(menu, manualPause ? "Videoyu sürdür" : "Videoyu duraklat", () => { manualPause = !manualPause; UpdateActivity(false); }, mediaReady);
        Item(menu, "Özgün gece görünümüne dön", async () => { await controller.SetDesktopVideoAsync(null); await controller.SetDesktopImageAsync(null); LoadOptions(); status.Text = controller.ViewModel.Status; }, options.VideoPath is not null || options.ImagePath is not null);
        Item(menu, "Özgün Nox maskotunu aç", async () => { await controller.UseOriginalMascotAsync(); status.Text = controller.ViewModel.Status; });
        menu.Items.Add(new Separator());
        var screens = new MenuItem { Header = "Ekran" };
        var current = CurrentMonitor();
        foreach (var monitor in controller.ViewModel.Monitors)
        {
            var target = monitor;
            var item = new MenuItem { Header = $"{monitor.Name} · {monitor.Width} × {monitor.Height}{(monitor.Primary ? " · ana" : "")}", IsChecked = monitor.Id == current.Id };
            item.Click += async (_, _) => { await controller.SetDesktopMonitorAsync(target.Id); Place(); };
            screens.Items.Add(item);
        }
        menu.Items.Add(screens);
        var showIcons = new MenuItem { Header = "Masaüstü simgelerini göster", IsChecked = options.ShowIcons };
        showIcons.Click += async (_, _) => { if (await controller.SetDesktopIconsVisibleAsync(!options.ShowIcons)) { RenderIcons(); status.Text = options.ShowIcons ? "Simgeler gizlendi; dosyaların yerinde duruyor. Arama yine çalışır." : Hint; } };
        menu.Items.Add(showIcons);
        Item(menu, "Yenile  ·  F5", () => _ = RefreshFilesAsync());
        Item(menu, "Simgeleri otomatik diz", () => { positions.Clear(); saveLayout.Stop(); saveLayout.Start(); RenderIcons(); status.Text = "Simgeler widget ve dock alanlarının etrafına yeniden dizildi."; }, positions.Count > 0);
        menu.Items.Add(new Separator());
        Item(menu, "Widget atölyesi", controller.Widgets.ShowEditor);
        Item(menu, "Kontrol merkezi", controller.ShowControls);
        Item(menu, "Ayarlar", controller.ShowSettings);
        return menu;
    }
    private static void Item(ItemsControl menu, string text, Action action, bool enabled = true) { var item = new MenuItem { Header = text, IsEnabled = enabled }; item.Click += (_, _) => action(); menu.Items.Add(item); }
    private MonitorInfo CurrentMonitor() => controller.ViewModel.Monitors.FirstOrDefault(m => m.Id == controller.ViewModel.Settings.DesktopSpace.MonitorId) ?? controller.ViewModel.Monitors.First(m => m.Primary);
    internal void LoadOptions()
    {
        var options = controller.ViewModel.Settings.DesktopSpace;
        ShowInTaskbar = !options.FollowDesktop;
        if (!saveLayout.IsEnabled) { positions.Clear(); foreach (var position in options.Icons) positions[position.Path] = position; }
        LoadBackground(options.ImagePath);
        if (loadedVideo is not null && loadedVideo == options.VideoPath) return;
        mediaReady = playing = false; manualPause = false; video.Close(); loadedVideo = options.VideoPath;
        if (loadedVideo is null) { video.Visibility = Visibility.Hidden; if (string.IsNullOrEmpty(status.Text) || status.Text.StartsWith("Yerel video")) status.Text = Hint; return; }
        try { controller.ValidateDesktopVideo(loadedVideo); video.Visibility = Visibility.Visible; video.Source = new Uri(loadedVideo); video.Play(); video.Pause(); status.Text = "Yerel video açılıyor…"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { status.Text = ex.Message; video.Visibility = Visibility.Hidden; }
    }
    private void LoadBackground(string? path)
    {
        if (path == loadedImage) return;
        loadedImage = path;
        if (path is null) { background.Source = OriginalArt.Wallpaper(); return; }
        try
        {
            controller.ValidateDesktopImage(path);
            // Decode near screen width only; large photos otherwise cost hundreds of MB of bitmap memory.
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(path);
            image.DecodePixelWidth = (int)Math.Clamp(CurrentMonitor().Width, 640, 3840); image.EndInit(); image.Freeze();
            background.Source = image;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException)
        { background.Source = OriginalArt.Wallpaper(); status.Text = "Arka plan görseli açılamadı; özgün gece görünümü gösteriliyor."; }
    }
    internal void Reveal(bool activate)
    {
        LoadOptions();
        if (activate) WindowState = WindowState.Normal;
        if (!IsVisible) Show();
        if (activate) Activate();
        else ShowWithoutActivation(new WindowInteropHelper(this).Handle, 4);
        Place();
    }
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "ShowWindow")]
    private static extern bool ShowWithoutActivation(nint window, int command);
    internal void Place()
    {
        if (closed) return;
        var m = CurrentMonitor();
        Width = m.Width / m.Scale; Height = m.Height / m.Scale;
        WindowPlacement.Move(new WindowInteropHelper(this).EnsureHandle(), m.X, m.Y);
        // A floating top bar on this monitor pushes the header down so the two never overlap.
        var top = 0.0;
        foreach (var panel in controller.Windows.OfType<PanelWindow>().Where(p => p.IsVisible))
        {
            try { var p = WindowPlacement.Position(new WindowInteropHelper(panel).Handle); var y = (p.Y - m.Y) / m.Scale; var x = (p.X - m.X) / m.Scale; if (y < 160 && y + panel.ActualHeight > 0 && x < Width && x + panel.ActualWidth > 0) top = Math.Max(top, y + panel.ActualHeight); }
            catch (System.ComponentModel.Win32Exception) { }
        }
        if (header is not null) header.Margin = new(Gutter, 18 + top, Gutter, 0);
        headerInset = 92 + top;
        RenderIcons();
    }
    internal void ScheduleReflow() { if (closed || !IsVisible) return; reflow.Stop(); reflow.Start(); }
    internal void UpdateActivity(bool blocked)
    {
        var ownForeground = new WindowInteropHelper(this).Handle == controller.ForegroundWindow || (controller.ViewModel.Settings.DesktopSpace.FollowDesktop && WindowsDesktopMonitor.Presence() == DesktopPresence.Desktop);
        var shouldPlay = mediaReady && IsVisible && WindowState != WindowState.Minimized && ownForeground && !blocked && !manualPause && controller.ViewModel.Settings.AnimationsEnabled && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast && WindowsPowerState.ConservePower() == false;
        if (playing == shouldPlay) return;
        playing = shouldPlay; if (shouldPlay) video.Play(); else video.Pause();
    }
    private async void PickVideo()
    {
        var picker = new OpenFileDialog { Filter = "Yerel video|*.mp4;*.m4v;*.wmv", CheckFileExists = true };
        if (picker.ShowDialog(this) == true) { await controller.SetDesktopVideoAsync(picker.FileName); LoadOptions(); status.Text = controller.ViewModel.Status; }
    }
    private async void PickImage()
    {
        var picker = new OpenFileDialog { Filter = "Yerel görsel|" + string.Join(";", DesktopSpaceOptions.ImageExtensions.Select(e => "*" + e)), CheckFileExists = true };
        if (picker.ShowDialog(this) == true) { await controller.SetDesktopImageAsync(picker.FileName); LoadOptions(); status.Text = controller.ViewModel.Status; }
    }
    internal Task RefreshFilesAsync()
    {
        if (closed) return Task.CompletedTask;
        if (!refreshPending.IsCompleted) return refreshPending;
        return refreshPending = ReadFilesAsync();
    }
    private async Task ReadFilesAsync()
    {
        try { var next = await Task.Run(DesktopFiles.Read); if (!closed) { files = next; RenderIcons(); } }
        catch (Exception ex) { if (!closed) status.Text = "Masaüstü okunamadı: " + ex.Message; }
    }
    // Visible Noxelyra windows on this monitor (widgets, dock, mascot) in this window's DIP space.
    private List<LayoutRect> ReservedAreas()
    {
        var m = CurrentMonitor(); var result = new List<LayoutRect>();
        foreach (var window in controller.Windows.Where(w => w.IsVisible && w is not PanelWindow && w.ActualWidth > 0))
        {
            try
            {
                var p = WindowPlacement.Position(new WindowInteropHelper(window).Handle);
                var rect = new LayoutRect((p.X - m.X) / m.Scale, (p.Y - m.Y) / m.Scale, window.ActualWidth, Math.Max(window.ActualHeight, window is DockWindow ? 72 : 0));
                if (rect.Intersects(new(0, 0, Width, Height))) result.Add(rect.Inflate(12));
            }
            catch (System.ComponentModel.Win32Exception) { /* A closing window simply stops reserving space. */ }
        }
        return result;
    }
    private void RenderIcons()
    {
        if (closed || !double.IsFinite(Width) || !double.IsFinite(Height)) return;
        icons.Children.Clear();
        // Hidden icons give a clean wallpaper-and-widgets desktop; searching still reveals matching files.
        var visible = controller.ViewModel.Settings.DesktopSpace.ShowIcons || search.Text.Length > 0;
        var filtered = visible ? files.Where(f => f.Name.Contains(search.Text, StringComparison.CurrentCultureIgnoreCase)).ToArray() : [];
        count.Text = search.Text.Length > 0 ? $"{filtered.Length} / {files.Count} öğe" : visible ? $"{files.Count} öğe" : $"{files.Count} öğe · simgeler gizli";
        double maxX = Math.Max(0, Width - CellWidth), height = Math.Max(headerInset + CellHeight, Height - Gutter);
        var saved = filtered.Where(f => positions.ContainsKey(f.Path)).ToDictionary(f => f.Path, f => positions[f.Path], StringComparer.OrdinalIgnoreCase);
        var occupied = saved.Values.Select(p => new LayoutRect(Math.Clamp(p.X, 0, maxX), Math.Max(headerInset, p.Y), CellWidth, CellHeight)).ToList();
        var flow = DesktopIconFlow.Arrange(filtered.Length - saved.Count, Width, Height, new(Gutter, headerInset, Math.Max(CellWidth, Width - Gutter * 2), height - headerInset), CellWidth, CellHeight, ReservedAreas(), occupied);
        int cell = 0;
        foreach (var file in filtered)
        {
            var stack = new StackPanel();
            var image = new Image { Width = 44, Height = 44, Margin = new(0, 2, 0, 6) }; stack.Children.Add(image);
            stack.Children.Add(new TextBlock { Text = file.Name, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center, MaxHeight = 34, FontSize = 12, Foreground = Brushes.White });
            var button = new Button { Content = stack, Width = TileWidth, Height = TileHeight, Padding = new(4, 6, 4, 4), ToolTip = file.Path, Style = (Style)FindResource("DesktopTile") };
            System.Windows.Automation.AutomationProperties.SetName(button, file.Name);
            double x, y;
            if (saved.TryGetValue(file.Path, out var p)) { x = Math.Clamp(p.X, 0, maxX); y = Math.Max(headerInset, p.Y); }
            else if (cell < flow.Count) { x = flow[cell].X + (CellWidth - TileWidth) / 2; y = flow[cell].Y; cell++; }
            else { x = Gutter; y = headerInset; }
            Canvas.SetLeft(button, x); Canvas.SetTop(button, y); icons.Children.Add(button);
            async Task Icon() { try { var icon = await shellIcons.GetAsync(file.Path); if (!closed && icon.Icon is not null) image.Source = icon.Icon; } catch { /* Filename remains accessible if the OS cannot provide an icon. */ } }
            _ = Icon();
            void Open() { try { DesktopFiles.Open(file); } catch (Exception ex) { status.Text = "Öğe açılamadı: " + ex.Message; } }
            void Remember() { positions[file.Path] = new(file.Path, Canvas.GetLeft(button), Canvas.GetTop(button)); saveLayout.Stop(); saveLayout.Start(); }
            var tileMenu = new ContextMenu();
            Item(tileMenu, "Aç", Open);
            Item(tileMenu, "Dosya konumunu göster", () => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe") { ArgumentList = { "/select," + file.Path }, UseShellExecute = false }); } catch (Exception ex) { status.Text = "Explorer açılamadı: " + ex.Message; } });
            Item(tileMenu, "Otomatik yerleşime al", () => { positions.Remove(file.Path); saveLayout.Stop(); saveLayout.Start(); RenderIcons(); }, positions.ContainsKey(file.Path));
            button.ContextMenu = tileMenu;
            button.MouseDoubleClick += (_, e) => { Open(); e.Handled = true; };
            button.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter) { Open(); e.Handled = true; }
                if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey is Key.Left or Key.Right or Key.Up or Key.Down)
                {
                    var nx = Canvas.GetLeft(button) + (e.SystemKey == Key.Left ? -12 : e.SystemKey == Key.Right ? 12 : 0);
                    var ny = Canvas.GetTop(button) + (e.SystemKey == Key.Up ? -12 : e.SystemKey == Key.Down ? 12 : 0);
                    Canvas.SetLeft(button, Math.Clamp(nx, 0, maxX)); Canvas.SetTop(button, Math.Clamp(ny, headerInset, 10000));
                    Remember(); e.Handled = true;
                }
            };
            Point origin = default; Point start = default; bool dragging = false;
            button.PreviewMouseLeftButtonDown += (_, e) => { if (e.ClickCount > 1) return; origin = e.GetPosition(icons); start = new(Canvas.GetLeft(button), Canvas.GetTop(button)); dragging = true; };
            button.PreviewMouseMove += (_, e) =>
            {
                if (!dragging || e.LeftButton != MouseButtonState.Pressed) return;
                var delta = e.GetPosition(icons) - origin;
                if (delta.Length < 5 && !button.IsMouseCaptured) return;
                button.CaptureMouse(); Canvas.SetLeft(button, Math.Clamp(start.X + delta.X, 0, maxX)); Canvas.SetTop(button, Math.Clamp(start.Y + delta.Y, headerInset, 10000));
            };
            button.PreviewMouseLeftButtonUp += (_, e) =>
            {
                dragging = false; if (!button.IsMouseCaptured) return; button.ReleaseMouseCapture();
                Remember(); e.Handled = true;
            };
        }
        icons.Height = Math.Max(Height, icons.Children.OfType<Button>().Select(b => Canvas.GetTop(b) + CellHeight + Gutter).DefaultIfEmpty(0).Max());
    }
}
