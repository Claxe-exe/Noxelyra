using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;

namespace ClaxeDesktop.App;
/// <summary>Waybar-style top bar: separate glass islands (apps + active window, dynamic island, media/volume/system/tools) on a click-through strip.</summary>
public sealed class PanelWindow : FloatingWindow
{
    private const string IconFont = "Segoe Fluent Icons, Segoe MDL2 Assets";
    private readonly DesktopViewModel vm;
    private readonly DesktopController controller;
    private readonly ShellIconService icons;
    private readonly WindowsAudioService audio = new();
    private readonly MediaService media = new();
    private readonly List<Border> islands = [];
    private readonly StackPanel left = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly StackPanel center = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly StackPanel right = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly Border clockIsland, mediaIsland, volumeIsland;
    private readonly DynamicIsland island;
    private readonly Image windowIcon = new() { Width = 16, Height = 16, Margin = new(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    private readonly DockPanel row = new() { LastChildFill = true };
    private readonly TextBlock windowTitle = new() { MaxWidth = 320, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Text = "Masaüstü" };
    private readonly TextBlock mediaText = new() { MaxWidth = 220, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 6, 0) };
    private readonly Button playPause, previous, next, volumeButton;
    private readonly TextBlock volumeGlyph = Glyph("\uE767"), volumeText = Figure("");
    private readonly Ellipse unread = new() { Width = 7, Height = 7, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, -2, -4, 0), Visibility = Visibility.Collapsed, IsHitTestVisible = false };
    // Volume is polled faster than the clock so the island reacts to keyboard volume keys almost at once.
    private readonly DispatcherTimer volumeTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private (int Level, bool Muted)? lastVolume;
    private string? lastExecutable;
    private int ticks;
    private bool reading, playing;
    internal bool Integrated { get; set; }
    protected override bool CanMove => !Integrated;
    internal DynamicIsland Island => island;
    public PanelWindow(DesktopViewModel vm, DesktopController controller, ShellIconService icons) : base("Panel")
    {
        this.vm = vm; this.controller = controller; this.icons = icons;
        DataContext = vm; Width = vm.PanelWidth; Height = vm.PanelHeight; Frame.Padding = new(6, 4, 6, 4);
        // Side groups keep their size; the island centres in what is left, so modules never overlap on a narrow floating bar.
        DockPanel.SetDock(left, Dock.Left); DockPanel.SetDock(right, Dock.Right);
        row.Children.Add(left); row.Children.Add(right); row.Children.Add(center); Frame.Child = row;

        // Left: launcher mark and the focused window, like Waybar's distro logo + hyprland/window modules.
        var mark = new TextBlock { Text = "N", FontWeight = FontWeights.Bold, FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
        mark.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
        var launcher = IconButton(mark, "Uygulamalar", controller.ShowLauncher);
        left.Children.Add(MakeIsland(launcher));
        var focused = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(12, 0, 14, 0) }; focused.Children.Add(windowIcon); focused.Children.Add(windowTitle);
        left.Children.Add(MakeIsland(focused));

        // Center: the dynamic island. It doubles as the drag handle while the bar floats; a click without movement acts on the island.
        island = new(vm, controller) { Enabled = vm.Settings.DynamicIsland };
        clockIsland = MakeIsland(island); center.Children.Add(clockIsland);
        clockIsland.ToolTip = "Tıkla: zamanlayıcı veya çalan parça · Sağ tık: menü · Bar serbestken sürükleyerek taşı";
        clockIsland.MouseLeftButtonDown += (_, e) => { if (e.ClickCount != 1 || !CanMove) return; e.Handled = true; if (!TryDrag()) island.Click(); };
        clockIsland.MouseLeftButtonUp += (_, e) => { if (CanMove) return; e.Handled = true; island.Click(); };
        clockIsland.MouseRightButtonUp += (_, e) => { e.Handled = true; island.OpenMenu(); };
        island.KeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { island.Click(); e.Handled = true; } else if (e.Key == Key.Apps || (e.Key == Key.F10 && Keyboard.Modifiers == ModifierKeys.Shift)) { island.OpenMenu(); e.Handled = true; } };

        // Right: media (only while a session exists and the island is off), volume, system load and tools.
        var mediaRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(12, 0, 4, 0) };
        var note = Glyph("\uE8D6"); note.Margin = new(0, 0, 8, 0); note.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
        mediaRow.Children.Add(note); mediaRow.Children.Add(mediaText);
        previous = IconButton(Glyph("\uE892"), "Önceki parça", () => _ = MediaAsync("previous"));
        playPause = IconButton(Glyph("\uE768"), "Oynat", () => _ = MediaAsync(playing ? "pause" : "play"));
        next = IconButton(Glyph("\uE893"), "Sonraki parça", () => _ = MediaAsync("next"));
        mediaRow.Children.Add(previous); mediaRow.Children.Add(playPause); mediaRow.Children.Add(next);
        mediaIsland = MakeIsland(mediaRow); mediaIsland.Visibility = Visibility.Collapsed; right.Children.Add(mediaIsland);

        var volumeRow = new StackPanel { Orientation = Orientation.Horizontal }; volumeGlyph.Margin = new(0, 0, 8, 0); volumeRow.Children.Add(volumeGlyph); volumeRow.Children.Add(volumeText);
        volumeButton = IconButton(volumeRow, "Sesi kapat / aç · Tekerlekle ayarla", ToggleMute); volumeButton.Padding = new(12, 0, 12, 0);
        volumeButton.PreviewMouseWheel += (_, e) => { ChangeVolume(e.Delta > 0 ? .02f : -.02f); e.Handled = true; };
        volumeButton.PreviewKeyDown += (_, e) => { if (e.Key is Key.Up or Key.Right) { ChangeVolume(.02f); e.Handled = true; } else if (e.Key is Key.Down or Key.Left) { ChangeVolume(-.02f); e.Handled = true; } };
        volumeIsland = MakeIsland(volumeButton); right.Children.Add(volumeIsland);

        var system = new StackPanel { Orientation = Orientation.Horizontal };
        system.Children.Add(Label("CPU")); var cpu = Figure(""); cpu.Margin = new(6, 0, 14, 0); cpu.SetBinding(TextBlock.TextProperty, new Binding(nameof(DesktopViewModel.CpuText))); system.Children.Add(cpu);
        system.Children.Add(Label("RAM")); var ram = Figure(""); ram.Margin = new(6, 0, 0, 0); ram.SetBinding(TextBlock.TextProperty, new Binding(nameof(DesktopViewModel.MemoryText))); system.Children.Add(ram);
        var systemButton = IconButton(system, "Hafifletme: bellek ve açılışta başlayanlar", controller.ShowLighten); systemButton.Padding = new(12, 0, 12, 0);
        right.Children.Add(MakeIsland(systemButton));

        var themeButton = IconButton(Glyph("\uE790"), "Renk paleti", null);
        themeButton.Click += (_, _) => { var menu = ThemeMenu(); menu.PlacementTarget = themeButton; menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom; menu.IsOpen = true; };
        unread.SetResourceReference(Shape.FillProperty, "Accent");
        var quickGlyph = new Grid(); quickGlyph.Children.Add(Glyph("\uE713")); quickGlyph.Children.Add(unread);
        var quick = IconButton(quickGlyph, "Hızlı ayarlar ve bildirimler", controller.ShowQuickSettings);
        var power = IconButton(Glyph("\uE7E8"), "Noxelyra menüsü", null);
        power.Click += (_, _) => { var menu = PowerMenu(); menu.PlacementTarget = power; menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom; menu.IsOpen = true; };
        var tray = IconButton(Glyph("\uE70E"), "Sistem tepsisi: gizli simgeler (görev çubuğu gizliyken de)", controller.ShowTray);
        var tools = new StackPanel { Orientation = Orientation.Horizontal }; tools.Children.Add(tray); tools.Children.Add(themeButton); tools.Children.Add(quick); tools.Children.Add(power);
        right.Children.Add(MakeIsland(tools));

        ContextMenu = PowerMenu();
        row.SizeChanged += (_, _) => Fit();
        vm.PropertyChanged += Ticked;
        volumeTimer.Tick += (_, _) => { if (IsVisible && !controller.IsSuspended && !controller.IsLocked) ReadVolume(); };
        controller.NotificationPosted += island.ShowNotification;
        controller.NotificationsChanged += UpdateUnread;
        controller.TimerChanged += island.Tick;
        Loaded += (_, _) => { ReadVolume(); UpdateUnread(); volumeTimer.Start(); };
        Closed += (_, _) =>
        {
            vm.PropertyChanged -= Ticked; volumeTimer.Stop();
            controller.NotificationPosted -= island.ShowNotification; controller.NotificationsChanged -= UpdateUnread; controller.TimerChanged -= island.Tick;
        };
    }
    private static TextBlock Glyph(string code) => new() { Text = code, FontFamily = new FontFamily(IconFont), FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
    private static TextBlock Figure(string text) { var block = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center }; System.Windows.Documents.Typography.SetNumeralAlignment(block, FontNumeralAlignment.Tabular); return block; }
    private static TextBlock Label(string text) { var block = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center }; block.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); return block; }
    private Border MakeIsland(UIElement content)
    {
        var border = new Border { Child = content, Margin = new(4, 0, 4, 0), BorderThickness = new(1), VerticalAlignment = VerticalAlignment.Stretch, MinWidth = 36 };
        islands.Add(border); return border;
    }
    private static Button IconButton(object content, string name, Action? action)
    {
        var button = new Button { Content = content, ToolTip = name, Padding = new(10, 0, 10, 0), Margin = new(2), Background = Brushes.Transparent, BorderThickness = new(0), MinWidth = 34 };
        System.Windows.Automation.AutomationProperties.SetName(button, name);
        if (action is not null) button.Click += (_, _) => action();
        return button;
    }
    private ContextMenu ThemeMenu()
    {
        var menu = new ContextMenu();
        foreach (var palette in ThemePresets.All)
        {
            var swatch = new Border { Width = 12, Height = 12, CornerRadius = new(6), Margin = new(0, 0, 10, 0), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(palette.Accent)) };
            var header = new StackPanel { Orientation = Orientation.Horizontal }; header.Children.Add(swatch); header.Children.Add(new TextBlock { Text = palette.Name });
            var item = new MenuItem { Header = header, IsChecked = palette.Id == vm.Settings.ThemeId };
            var id = palette.Id; item.Click += async (_, _) => await controller.SetThemeAsync(id);
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        var wallpaper = new MenuItem { Header = "Duvar kâğıdından renk al", IsChecked = vm.Settings.ThemeId == ThemePresets.WallpaperId, ToolTip = "Noxelyra masaüstü görselinden ya da Windows duvar kâğıdından okunaklı bir palet üretir." };
        wallpaper.Click += async (_, _) => await controller.UseWallpaperPaletteAsync();
        menu.Items.Add(wallpaper);
        return menu;
    }
    private ContextMenu PowerMenu()
    {
        var menu = new ContextMenu();
        void Add(string text, Action action) { var item = new MenuItem { Header = text }; item.Click += (_, _) => action(); menu.Items.Add(item); }
        Add("Ayarlar", controller.ShowSettings);
        Add("Kontrol merkezi", controller.ShowFullControls);
        Add("Klavye ve kısayollar", controller.ShowKeyboard);
        Add("Masaüstü alanını aç", controller.ShowDesktopSpace);
        menu.Items.Add(new Separator());
        Add("Noxelyra'dan çık", controller.Exit);
        return menu;
    }
    public override void ApplyAppearance(DesktopViewModel model)
    {
        base.ApplyAppearance(model);
        // The strip itself stays transparent so gaps between islands click through, Hyprland-style.
        var fill = Frame.Background; var border = Frame.BorderBrush; var effect = Frame.Effect;
        Frame.Background = Brushes.Transparent; Frame.BorderThickness = new(0); Frame.Effect = null;
        var height = Math.Max(24, model.PanelHeight - 8);
        foreach (var part in islands)
        {
            part.Background = fill; part.BorderBrush = border; part.Effect = effect;
            part.BorderThickness = new(model.Settings.BorderWidth);
            part.CornerRadius = new(Math.Min(model.Settings.CornerRadius, height / 2));
            part.SetResourceReference(Border.BorderBrushProperty, model.Settings.Material == "neon" ? "Accent" : "Line");
        }
        if (island.Enabled != model.Settings.DynamicIsland) { island.Enabled = model.Settings.DynamicIsland; if (island.Enabled) Hide(mediaIsland); else _ = ReadMediaAsync(); }
    }
    // The island gets what the side groups leave; on a narrow floating bar its date gives way first so the time never clips.
    private void Fit()
    {
        if (row.ActualWidth <= 0) return;
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        left.Measure(infinite); right.Measure(infinite);
        var centred = clockIsland.Parent == center;
        island.Available = Math.Max(80, (centred ? row.ActualWidth - left.DesiredSize.Width - right.DesiredSize.Width : 380) - 12);
    }
    public void UpdateOrder(bool clockLeft)
    {
        var parent = (Panel)clockIsland.Parent; var target = clockLeft ? left : center;
        if (parent == target) return;
        parent.Children.Remove(clockIsland); if (clockLeft) target.Children.Insert(1, clockIsland); else target.Children.Add(clockIsland);
        Fit();
    }
    internal void UpdateWindows(IReadOnlyList<AppWindow> windows, nint foreground)
    {
        var active = windows.FirstOrDefault(w => w.Handle == foreground);
        // Noxelyra's own windows are not in the snapshot; keep the last app title while the bar itself has focus.
        if (active is null && WindowsDesktopMonitor.Presence() != DesktopPresence.Desktop) return;
        var title = active is null ? "Masaüstü" : active.Title;
        if (title != windowTitle.Text) { windowTitle.Text = title; Fit(); }
        if (active?.Executable != lastExecutable)
        {
            lastExecutable = active?.Executable; windowIcon.Source = null; windowIcon.Visibility = Visibility.Collapsed;
            if (lastExecutable is { } path) _ = LoadIconAsync(path);
        }
    }
    private async Task LoadIconAsync(string path)
    {
        try { var asset = await icons.GetAsync(path); if (path == lastExecutable && asset.Icon is not null) { windowIcon.Source = asset.Icon; windowIcon.Visibility = Visibility.Visible; } }
        catch { /* The title alone still identifies the window. */ }
    }
    private void Ticked(object? sender, PropertyChangedEventArgs e)
    {
        // The controller ticks once a second and already stops while locked, suspended or hidden.
        if (e.PropertyName != nameof(DesktopViewModel.Time) || !IsVisible) return;
        island.Tick();
        if (ticks++ % 2 == 0) _ = ReadMediaAsync();
    }
    private void UpdateUnread() => unread.Visibility = controller.Notifications.Unread > 0 ? Visibility.Visible : Visibility.Collapsed;
    private static string VolumeGlyph(int level, bool muted) => muted ? "\uE74F" : level == 0 ? "\uE992" : level < 34 ? "\uE993" : level < 67 ? "\uE994" : "\uE995";
    private void ReadVolume()
    {
        try
        {
            var state = audio.Read(); var level = (int)Math.Round(state.Level * 100);
            volumeText.Text = state.Muted ? "Sessiz" : $"%{level}";
            volumeGlyph.Text = VolumeGlyph(level, state.Muted);
            if (volumeIsland.Visibility != Visibility.Visible) { volumeIsland.Visibility = Visibility.Visible; Fit(); }
            // The first reading is the starting point, not a change worth announcing.
            if (lastVolume is { } before && before != (level, state.Muted)) island.ShowVolume(level, state.Muted, VolumeGlyph(level, state.Muted));
            lastVolume = (level, state.Muted);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidCastException or UnauthorizedAccessException)
        { Hide(volumeIsland); lastVolume = null; }
    }
    private void Hide(Border part) { if (part.Visibility == Visibility.Collapsed) return; part.Visibility = Visibility.Collapsed; Fit(); }
    private void ChangeVolume(float delta)
    {
        try { var state = audio.Read(); audio.SetLevel(Math.Clamp(state.Level + delta, 0, 1)); if (state.Muted && delta > 0) audio.SetMute(false); ReadVolume(); }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidCastException or UnauthorizedAccessException) { vm.Status = "Ses düzeyi değiştirilemedi."; }
    }
    private void ToggleMute()
    {
        try { audio.SetMute(!audio.Read().Muted); ReadVolume(); }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidCastException or UnauthorizedAccessException) { vm.Status = "Ses kapatılamadı."; }
    }
    private async Task ReadMediaAsync()
    {
        if (reading) return; reading = true;
        try
        {
            var state = await media.ReadAsync();
            island.ShowMedia(state);
            if (island.Enabled || state is null || (string.IsNullOrWhiteSpace(state.Title) && string.IsNullOrWhiteSpace(state.Artist))) { Hide(mediaIsland); return; }
            mediaText.Text = string.IsNullOrWhiteSpace(state.Artist) ? state.Title : $"{state.Title} — {state.Artist}";
            mediaIsland.ToolTip = mediaText.Text;
            playing = state.Playing;
            ((TextBlock)playPause.Content).Text = playing ? "\uE769" : "\uE768";
            playPause.ToolTip = playing ? "Duraklat" : "Oynat"; System.Windows.Automation.AutomationProperties.SetName(playPause, (string)playPause.ToolTip);
            playPause.IsEnabled = playing ? state.CanPause : state.CanPlay; previous.IsEnabled = state.CanPrevious; next.IsEnabled = state.CanNext;
            if (mediaIsland.Visibility != Visibility.Visible) { mediaIsland.Visibility = Visibility.Visible; Fit(); }
        }
        catch (Exception) { island.ShowMedia(null); Hide(mediaIsland); }
        finally { reading = false; }
    }
    private async Task MediaAsync(string action)
    {
        try { if (!await media.InvokeAsync(action)) vm.Status = "Medya uygulaması bu komutu kabul etmedi."; }
        catch (Exception) { vm.Status = "Medya oturumuna ulaşılamadı."; }
        await ReadMediaAsync();
    }
}
