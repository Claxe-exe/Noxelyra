using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

/// <summary>
/// Quick settings that slide down from the bar's top-right corner: switches, volume, the playing track, palettes, Windows
/// shortcuts and Noxelyra's notifications. A click outside, Esc or the bar button closes it. Every switch is a real setting.
/// </summary>
internal sealed class QuickSettingsWindow : Window
{
    private const string IconFont = "Segoe Fluent Icons, Segoe MDL2 Assets";
    private const double CardWidth = 392, Shadow = 16;
    private readonly DesktopController controller;
    private readonly WindowsAudioService audio = new();
    private readonly MediaService media = new();
    private readonly Border card = new() { Width = CardWidth, CornerRadius = new(22), BorderThickness = new(1), Padding = new(16), Margin = new(Shadow) };
    private readonly TranslateTransform slide = new();
    private readonly ScrollViewer scroller = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly List<(ToggleButton Tile, TextBlock State, Func<bool> Read, bool Editable)> tiles = [];
    private readonly Slider volume = new() { Minimum = 0, Maximum = 100, Margin = new(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, IsSnapToTickEnabled = true, TickFrequency = 1 };
    private readonly TextBlock volumeText = Figure(), volumeGlyph = Glyph("\uE767", 16);
    private readonly Button muteButton;
    private readonly Border mediaCard = new() { CornerRadius = new(16), Padding = new(10), Margin = new(4, 12, 4, 0), Visibility = Visibility.Collapsed };
    private readonly Border mediaArt = new() { Width = 44, Height = 44, CornerRadius = new(10), Margin = new(0, 0, 12, 0) };
    private readonly TextBlock mediaTitle = Line(13), mediaArtist = Line(12), playGlyph = Glyph("\uE768", 16);
    private readonly Button mediaPrevious, mediaPlay, mediaNext;
    private readonly WrapPanel themes = new() { Margin = new(2, 6, 0, 0) };
    private readonly StackPanel notes = new();
    private readonly TextBlock notesHeader = new() { FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock emergencyHint = new() { FontSize = 11, Margin = new(4, 12, 4, 0), TextWrapping = TextWrapping.Wrap };
    private readonly DispatcherTimer poll = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private MediaState? track;
    internal int TileCount => tiles.Count;
    private bool refreshing, leaving, polling;
    public QuickSettingsWindow(DesktopController controller)
    {
        this.controller = controller;
        Title = "Noxelyra · Hızlı ayarlar"; WindowStyle = WindowStyle.None; AllowsTransparency = true; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; Topmost = true; Background = Brushes.Transparent; SizeToContent = SizeToContent.Height; Width = CardWidth + Shadow * 2;
        SetResourceReference(ForegroundProperty, "Text");
        card.SetResourceReference(Border.BackgroundProperty, "Surface"); card.SetResourceReference(Border.BorderBrushProperty, "Line");
        card.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 30, ShadowDepth = 6, Opacity = .45, Color = Colors.Black };
        card.RenderTransform = slide; Content = card;
        var root = new StackPanel(); scroller.Content = root; card.Child = scroller;
        var editable = !controller.IsSafeMode && !controller.ReadOnly;

        // Header: title and the date, with the full control center, settings and the Noxelyra menu on the right.
        var header = new DockPanel { Margin = new(4, 0, 0, 12) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(actions, Dock.Right); header.Children.Add(actions);
        actions.Children.Add(IconButton("\uE8A7", "Kontrol merkezini aç", () => { Leave(); controller.ShowFullControls(); }));
        actions.Children.Add(IconButton("\uE713", "Ayarlar", () => { Leave(); controller.ShowSettings(); }));
        var power = IconButton("\uE7E8", "Noxelyra menüsü", null); actions.Children.Add(power);
        power.Click += (_, _) => PowerMenu(power);
        var titles = new StackPanel(); header.Children.Add(titles);
        titles.Children.Add(new TextBlock { Text = "Hızlı ayarlar", FontSize = 17, FontWeight = FontWeights.SemiBold });
        var date = new TextBlock { FontSize = 12 }; date.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        date.DataContext = controller.ViewModel; date.SetBinding(TextBlock.TextProperty, new Binding(nameof(DesktopViewModel.Date))); titles.Children.Add(date);
        root.Children.Add(header);

        // Switches: three columns of real settings. Locked ones are dimmed in safe or read-only mode.
        var grid = new UniformGrid { Columns = 3, Margin = new(0, 0, 0, 4) }; root.Children.Add(grid);
        Tile(grid, "\uE708", "Rahatsız etme", () => controller.Notifications.DoNotDisturb, on => { controller.SetDoNotDisturb(on); return Task.CompletedTask; }, true);
        Tile(grid, "\uE8A9", "Döşeme", () => controller.Keyboard.Tiling, on => ChangeKeyboard(o => o with { Tiling = on }), editable);
        Tile(grid, "\uE721", "Win başlatıcı", () => controller.Keyboard.SuperLauncher, on => ChangeKeyboard(o => o with { SuperLauncher = on }), editable);
        Tile(grid, "\uE7C4", "Alt+Tab", () => controller.Keyboard.AltTab, on => ChangeKeyboard(o => o with { AltTab = on }), editable);
        Tile(grid, "\uE771", "Koyu Windows", () => controller.WindowsDark, on => { controller.ViewModel.Status = controller.SetWindowsDark(on); return Task.CompletedTask; }, editable);
        Tile(grid, "\uE90E", "Görev çubuğu gizli", () => controller.ViewModel.Settings.HideWindowsTaskbar, on => controller.ConfigureWindowsTaskbarAsync(on), editable);
        Tile(grid, "\uE8D6", "Dinamik ada", () => controller.ViewModel.Settings.DynamicIsland, on => controller.SetSettingAsync("panels.dynamicIsland", on, on ? "Dinamik ada açık." : "Dinamik ada kapalı; barda yalnız saat görünür."), editable);
        Tile(grid, "\uE945", "Animasyonlar", () => controller.ViewModel.Settings.AnimationsEnabled, on => controller.SetSettingAsync("animations.enabled", on, on ? "Animasyonlar açık." : "Animasyonlar kapalı."), editable);
        var desktop = new ToggleButton { Style = (Style)FindResource("QuickTile"), Content = TileContent("\uE7F4", "Masaüstü", "Göster", out _) };
        System.Windows.Automation.AutomationProperties.SetName(desktop, "Noxelyra masaüstünü göster");
        desktop.Click += (_, _) => { desktop.IsChecked = false; Leave(); controller.ToggleNoxelyraDesktop(); };
        BindForeground(desktop); grid.Children.Add(desktop);
        var status = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new(4, 6, 4, 0), DataContext = controller.ViewModel };
        status.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); status.SetBinding(TextBlock.TextProperty, new Binding(nameof(DesktopViewModel.Status)));
        root.Children.Add(status);

        // Volume: Windows' default output device.
        var sound = new DockPanel { Margin = new(4, 14, 4, 0) };
        muteButton = IconButton("", "Sesi kapat / aç", ToggleMute); muteButton.Content = volumeGlyph; DockPanel.SetDock(muteButton, Dock.Left); sound.Children.Add(muteButton);
        volumeText.MinWidth = 46; volumeText.TextAlignment = TextAlignment.Right; DockPanel.SetDock(volumeText, Dock.Right); sound.Children.Add(volumeText);
        sound.Children.Add(volume); root.Children.Add(sound);
        System.Windows.Automation.AutomationProperties.SetName(volume, "Windows ses düzeyi");
        volume.ValueChanged += (_, _) => { volumeText.Text = $"%{volume.Value:0}"; if (!refreshing) TryAudio(() => audio.SetLevel((float)(volume.Value / 100))); };

        // The playing track, when an app publishes one.
        mediaCard.SetResourceReference(Border.BackgroundProperty, "Card");
        var mediaRow = new DockPanel(); mediaCard.Child = mediaRow;
        mediaArt.SetResourceReference(Border.BackgroundProperty, "Line"); DockPanel.SetDock(mediaArt, Dock.Left); mediaRow.Children.Add(mediaArt);
        var mediaButtons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; DockPanel.SetDock(mediaButtons, Dock.Right); mediaRow.Children.Add(mediaButtons);
        mediaPrevious = IconButton("\uE892", "Önceki parça", () => _ = MediaAsync("previous")); mediaButtons.Children.Add(mediaPrevious);
        mediaPlay = IconButton("", "Oynat / duraklat", () => _ = MediaAsync(track?.Playing == true ? "pause" : "play")); mediaPlay.Content = playGlyph; mediaButtons.Children.Add(mediaPlay);
        mediaNext = IconButton("\uE893", "Sonraki parça", () => _ = MediaAsync("next")); mediaButtons.Children.Add(mediaNext);
        var mediaText = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; mediaTitle.FontWeight = FontWeights.SemiBold;
        mediaArtist.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); mediaText.Children.Add(mediaTitle); mediaText.Children.Add(mediaArtist); mediaRow.Children.Add(mediaText);
        root.Children.Add(mediaCard);

        // Palettes and Windows' own settings pages.
        root.Children.Add(Heading("Renk paleti"));
        root.Children.Add(themes);
        root.Children.Add(Heading("Windows ayarları"));
        var links = new UniformGrid { Columns = 4, Margin = new(0, 4, 0, 0) }; root.Children.Add(links);
        foreach (var (glyph, label, key) in new[] { ("\uE701", "Wi-Fi", "network"), ("\uE702", "Bluetooth", "bluetooth"), ("\uE7F4", "Ekran", "display"), ("\uE767", "Ses", "sound") })
        {
            var content = new StackPanel(); var icon = Glyph(glyph, 16); icon.HorizontalAlignment = HorizontalAlignment.Center; content.Children.Add(icon);
            content.Children.Add(new TextBlock { Text = label, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 4, 0, 0) });
            var button = new Button { Content = content, Padding = new(4, 8, 4, 8), ToolTip = label + " · Windows Ayarları'nı açar" };
            System.Windows.Automation.AutomationProperties.SetName(button, label + " ayarlarını aç");
            var target = key; button.Click += (_, _) => { Leave(); controller.OpenSystem(target); };
            links.Children.Add(button);
        }

        // Noxelyra's own notifications, newest first.
        var notesBar = new DockPanel { Margin = new(4, 16, 0, 6) };
        var clear = new Button { Content = "Temizle", Padding = new(10, 4, 10, 4), FontSize = 12 }; DockPanel.SetDock(clear, Dock.Right); notesBar.Children.Add(clear);
        clear.Click += (_, _) => { controller.ClearNotifications(); RenderNotes(); };
        notesBar.Children.Add(notesHeader); root.Children.Add(notesBar); root.Children.Add(notes);

        // Footer.
        var footer = new WrapPanel { Margin = new(0, 12, 0, 0) };
        foreach (var (label, action) in new (string, Action)[] { ("Klavye ve kısayollar", controller.ShowKeyboard), ("Hafifletme", controller.ShowLighten), ("Widget atölyesi", controller.Widgets.ShowEditor) })
        {
            var button = new Button { Content = label, Padding = new(10, 6, 10, 6), FontSize = 12 }; var run = action;
            button.Click += (_, _) => { Leave(); run(); }; footer.Children.Add(button);
        }
        root.Children.Add(footer);
        emergencyHint.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); root.Children.Add(emergencyHint);

        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Leave(); e.Handled = true; } };
        Deactivated += (_, _) => { if (IsVisible) Leave(); };
        poll.Tick += async (_, _) => await RefreshAsync();
        controller.NotificationsChanged += NotificationsChanged;
        Closed += (_, _) => { poll.Stop(); controller.NotificationsChanged -= NotificationsChanged; };
    }
    private Task ChangeKeyboard(Func<KeyboardOptions, KeyboardOptions> change) => controller.SetKeyboardAsync(change).ContinueWith(t => controller.ViewModel.Status = t.Result, TaskScheduler.FromCurrentSynchronizationContext());
    private void Tile(Panel parent, string glyph, string label, Func<bool> read, Func<bool, Task> change, bool editable)
    {
        var tile = new ToggleButton { Style = (Style)FindResource("QuickTile"), IsEnabled = editable };
        tile.Content = TileContent(glyph, label, "", out var state);
        BindForeground(tile);
        System.Windows.Automation.AutomationProperties.SetName(tile, label);
        tile.Click += async (_, _) =>
        {
            var wanted = tile.IsChecked == true; tile.IsEnabled = false;
            try { await change(wanted); }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException) { controller.ViewModel.Status = label + " değiştirilemedi: " + ex.Message; }
            finally { tile.IsEnabled = editable; Sync(); }
        };
        tiles.Add((tile, state, read, editable)); parent.Children.Add(tile);
    }
    private static StackPanel TileContent(string glyph, string label, string state, out TextBlock stateText)
    {
        var content = new StackPanel();
        content.Children.Add(Glyph(glyph, 18));
        content.Children.Add(new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap, MaxHeight = 34 });
        stateText = new TextBlock { Text = state, FontSize = 11, Opacity = .75 }; content.Children.Add(stateText);
        return content;
    }
    // The app-wide TextBlock style pins text to the theme colour; tiles need it to follow the tile (dark text on an accent tile).
    private static void BindForeground(ToggleButton tile)
    {
        if (tile.Content is not Panel content) return;
        foreach (var text in content.Children.OfType<TextBlock>()) text.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(Foreground)) { Source = tile });
    }
    private void Sync()
    {
        foreach (var (tile, state, read, _) in tiles)
        {
            bool on; try { on = read(); } catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException) { on = false; }
            tile.IsChecked = on; state.Text = on ? "Açık" : "Kapalı";
        }
    }
    /// <summary>Opens under the bar's right end on the bar's monitor (or the monitor under the pointer) and slides down.</summary>
    internal void Present(PanelWindow? panel)
    {
        leaving = false;
        Sync(); RenderThemes(); RenderNotes();
        emergencyHint.Text = "Acil çıkış: " + controller.GestureText("emergency") + " · Noxelyra'yı hemen kapatır, Windows'u eski haline getirir.";
        var monitor = controller.MonitorUnderPointer(); int right, top;
        var bar = panel is { IsVisible: true } ? WindowOps.Read(new WindowInteropHelper(panel).Handle) : null;
        if (bar is not null)
        {
            var b = bar.Bounds;
            monitor = controller.ViewModel.Monitors.FirstOrDefault(m => b.X + b.Width / 2 >= m.X && b.X + b.Width / 2 < m.X + m.Width && b.Y >= m.Y - 1 && b.Y < m.Y + m.Height) ?? monitor;
            right = (int)(b.X + b.Width); top = (int)(b.Y + b.Height);
        }
        else { right = monitor.X + monitor.Width - (int)(8 * monitor.Scale); top = monitor.Y + (int)(8 * monitor.Scale); }
        scroller.MaxHeight = Math.Max(320, monitor.Height / monitor.Scale - (top - monitor.Y) / monitor.Scale - 40 - Shadow * 2);
        if (!IsVisible) Show();
        var x = Math.Max(monitor.X, right - (int)((CardWidth + Shadow) * monitor.Scale));
        WindowPlacement.Move(new WindowInteropHelper(this).Handle, x, top - (int)((Shadow - 6) * monitor.Scale));
        Activate();
        if (Motion.Enabled(controller.ViewModel))
        {
            card.Opacity = 0; slide.Y = -14;
            Motion.To(card, OpacityProperty, 1, 200); Motion.To(slide, TranslateTransform.YProperty, 0, 340);
        }
        else { card.BeginAnimation(OpacityProperty, null); slide.BeginAnimation(TranslateTransform.YProperty, null); card.Opacity = 1; slide.Y = 0; }
        poll.Start(); _ = RefreshAsync();
        // Seen now; the bar's unread dot goes away while the list still marks what was new.
        controller.MarkNotificationsRead();
    }
    internal void Leave()
    {
        if (leaving || !IsVisible) return; leaving = true; poll.Stop();
        if (!Motion.Enabled(controller.ViewModel)) { Hide(); return; }
        Motion.To(slide, TranslateTransform.YProperty, -10, 140);
        Motion.To(card, OpacityProperty, 0, 140, () => { Hide(); card.BeginAnimation(OpacityProperty, null); slide.BeginAnimation(TranslateTransform.YProperty, null); card.Opacity = 1; slide.Y = 0; });
    }
    private void NotificationsChanged() { if (IsVisible) RenderNotes(); }
    private void RenderThemes()
    {
        themes.Children.Clear();
        foreach (var palette in ThemePresets.All)
        {
            var current = palette.Id == controller.ViewModel.Settings.ThemeId;
            var dot = new Border { Width = 22, Height = 22, CornerRadius = new(11), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(palette.Accent)) };
            var ring = new Border { CornerRadius = new(15), BorderThickness = new(2), Padding = new(2), Child = dot, BorderBrush = current ? (Brush)FindResource("Text") : Brushes.Transparent };
            var button = new Button { Content = ring, Padding = new(2), Margin = new(2), Background = Brushes.Transparent, BorderThickness = new(0), ToolTip = palette.Name, IsEnabled = !controller.IsSafeMode && !controller.ReadOnly };
            System.Windows.Automation.AutomationProperties.SetName(button, palette.Name + (current ? " · seçili" : ""));
            var id = palette.Id; button.Click += async (_, _) => { await controller.SetThemeAsync(id); RenderThemes(); };
            themes.Children.Add(button);
        }
        // Wallpaper colours: a split dot hints that the palette comes from the picture.
        var fromWallpaper = controller.ViewModel.Settings.ThemeId == ThemePresets.WallpaperId;
        var split = new Border { Width = 22, Height = 22, CornerRadius = new(11), Background = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(ThemePresets.Resolve(controller.ViewModel.Settings).Accent), (Color)ColorConverter.ConvertFromString(ThemePresets.Resolve(controller.ViewModel.Settings).Secondary), 45) };
        var wallpaperRing = new Border { CornerRadius = new(15), BorderThickness = new(2), Padding = new(2), Child = split, BorderBrush = fromWallpaper ? (Brush)FindResource("Text") : Brushes.Transparent };
        var wallpaper = new Button { Content = wallpaperRing, Padding = new(2), Margin = new(2), Background = Brushes.Transparent, BorderThickness = new(0), ToolTip = "Duvar kâğıdından renk al", IsEnabled = !controller.IsSafeMode && !controller.ReadOnly };
        System.Windows.Automation.AutomationProperties.SetName(wallpaper, "Duvar kâğıdından renk al" + (fromWallpaper ? " · seçili" : ""));
        wallpaper.Click += async (_, _) => { await controller.UseWallpaperPaletteAsync(); RenderThemes(); };
        themes.Children.Add(wallpaper);
    }
    private void RenderNotes()
    {
        notes.Children.Clear();
        var items = controller.Notifications.Items;
        notesHeader.Text = items.Count == 0 ? "Bildirimler" : $"Bildirimler · {items.Count}";
        if (controller.Notifications.DoNotDisturb) notesHeader.Text += " · rahatsız etme açık";
        if (items.Count == 0)
        {
            var empty = new TextBlock { Text = "Henüz bildirim yok. Güncellemeler, zamanlayıcılar ve kısayol uyarıları burada görünür.", TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new(4, 0, 4, 0) };
            empty.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); notes.Children.Add(empty); return;
        }
        foreach (var item in items.Take(20))
        {
            var box = new Border { CornerRadius = new(14), Padding = new(12, 10, 12, 10), Margin = new(0, 0, 0, 6), BorderThickness = new(1) };
            box.SetResourceReference(Border.BackgroundProperty, "Card"); box.SetResourceReference(Border.BorderBrushProperty, item.Read ? "Card" : "Accent");
            var layout = new DockPanel(); box.Child = layout;
            var icon = Glyph(item.Kind switch { "update" => "\uE896", "timer" => "\uE916", "warning" => "\uE7BA", _ => "\uE946" }, 15);
            icon.VerticalAlignment = VerticalAlignment.Top; icon.Margin = new(0, 2, 10, 0); icon.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
            DockPanel.SetDock(icon, Dock.Left); layout.Children.Add(icon);
            var text = new StackPanel(); layout.Children.Add(text);
            var top = new DockPanel();
            var when = new TextBlock { Text = When(item.Time), FontSize = 11, Margin = new(8, 0, 0, 0) }; when.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
            DockPanel.SetDock(when, Dock.Right); top.Children.Add(when);
            top.Children.Add(new TextBlock { Text = item.Title, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
            text.Children.Add(top);
            if (!string.IsNullOrWhiteSpace(item.Body))
            {
                var body = new TextBlock { Text = item.Body, TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new(0, 3, 0, 0) };
                body.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); text.Children.Add(body);
            }
            notes.Children.Add(box);
        }
    }
    private static string When(DateTimeOffset time)
    {
        var local = time.ToLocalTime(); var today = DateTime.Today;
        return local.Date == today ? local.ToString("HH:mm", CultureInfo.InvariantCulture) : local.Date == today.AddDays(-1) ? "dün " + local.ToString("HH:mm", CultureInfo.InvariantCulture) : local.ToString("d MMM", CultureInfo.GetCultureInfo("tr-TR"));
    }
    private async Task RefreshAsync()
    {
        if (polling || !IsVisible) return; polling = true;
        try
        {
            refreshing = true;
            try
            {
                var state = audio.Read();
                if (!volume.IsMouseCaptureWithin && !volume.IsKeyboardFocusWithin) volume.Value = Math.Round(state.Level * 100);
                volumeText.Text = state.Muted ? "Sessiz" : $"%{volume.Value:0}";
                volumeGlyph.Text = state.Muted ? "\uE74F" : "\uE767"; volume.IsEnabled = muteButton.IsEnabled = true;
            }
            catch (Exception ex) when (ex is COMException or InvalidCastException or UnauthorizedAccessException) { volume.IsEnabled = muteButton.IsEnabled = false; volumeText.Text = "Yok"; }
            finally { refreshing = false; }
            try { track = await media.ReadAsync(); } catch (Exception ex) when (ex is COMException or InvalidOperationException or UnauthorizedAccessException) { track = null; }
            if (track is null || (string.IsNullOrWhiteSpace(track.Title) && string.IsNullOrWhiteSpace(track.Artist))) { mediaCard.Visibility = Visibility.Collapsed; return; }
            mediaCard.Visibility = Visibility.Visible;
            mediaTitle.Text = string.IsNullOrWhiteSpace(track.Title) ? "Bilinmeyen parça" : track.Title; mediaArtist.Text = track.Artist;
            mediaArt.Background = track.Artwork is { } art ? new ImageBrush(art) { Stretch = Stretch.UniformToFill } : (Brush)FindResource("Line");
            playGlyph.Text = track.Playing ? "\uE769" : "\uE768";
            mediaPlay.IsEnabled = track.Playing ? track.CanPause : track.CanPlay; mediaPrevious.IsEnabled = track.CanPrevious; mediaNext.IsEnabled = track.CanNext;
        }
        finally { polling = false; }
    }
    private async Task MediaAsync(string action)
    {
        try { if (!await media.InvokeAsync(action)) controller.ViewModel.Status = "Medya uygulaması bu komutu kabul etmedi."; }
        catch (Exception ex) when (ex is COMException or InvalidOperationException) { controller.ViewModel.Status = "Medya oturumuna ulaşılamadı."; }
        await RefreshAsync();
    }
    private void ToggleMute() => TryAudio(() => { audio.SetMute(!audio.Read().Muted); _ = RefreshAsync(); });
    private void TryAudio(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException or UnauthorizedAccessException) { controller.ViewModel.Status = "Ses değiştirilemedi; çıkış aygıtını Windows ayarlarından kontrol et."; controller.Log.Write("audio-change-failed", ex); }
    }
    private void PowerMenu(Button anchor)
    {
        var menu = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Bottom };
        void Add(string text, Action action) { var item = new MenuItem { Header = text }; item.Click += (_, _) => { Leave(); action(); }; menu.Items.Add(item); }
        Add("Noxelyra masaüstü alanını aç", controller.ShowDesktopSpace);
        Add("Dock'u öne getir", controller.SummonDock);
        menu.Items.Add(new Separator());
        Add("Noxelyra'dan çık", controller.Exit);
        menu.IsOpen = true;
    }
    private static TextBlock Heading(string text) => new() { Text = text, FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new(4, 16, 0, 2) };
    private static Button IconButton(string glyph, string name, Action? action)
    {
        var button = new Button { Content = Glyph(glyph, 15), ToolTip = name, Padding = new(10, 7, 10, 7), Margin = new(2), Background = Brushes.Transparent, BorderThickness = new(0) };
        System.Windows.Automation.AutomationProperties.SetName(button, name);
        if (action is not null) button.Click += (_, _) => action();
        return button;
    }
    private static TextBlock Glyph(string code, double size) => new() { Text = code, FontFamily = new FontFamily(IconFont), FontSize = size, VerticalAlignment = VerticalAlignment.Center };
    private static TextBlock Line(double size) => new() { FontSize = size, TextTrimming = TextTrimming.CharacterEllipsis };
    private static TextBlock Figure() { var block = new TextBlock { VerticalAlignment = VerticalAlignment.Center }; System.Windows.Documents.Typography.SetNumeralAlignment(block, FontNumeralAlignment.Tabular); return block; }
}
