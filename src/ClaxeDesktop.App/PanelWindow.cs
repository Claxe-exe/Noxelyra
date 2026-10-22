using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;

namespace ClaxeDesktop.App;
/// <summary>Waybar-style top bar: separate glass islands (apps + active window, clock, media/volume/system/theme/power) on a click-through strip.</summary>
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
    private readonly Image windowIcon = new() { Width = 16, Height = 16, Margin = new(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    private readonly TextBlock date = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 10, 0) };
    private readonly DockPanel row = new() { LastChildFill = true };
    private readonly TextBlock windowTitle = new() { MaxWidth = 320, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Text = "Masaüstü" };
    private readonly TextBlock mediaText = new() { MaxWidth = 220, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 6, 0) };
    private readonly Button playPause, previous, next, volumeButton;
    private readonly TextBlock volumeGlyph = Glyph(""), volumeText = Figure("");
    private string? lastExecutable;
    private int ticks;
    private bool reading;
    internal bool Integrated { get; set; }
    protected override bool CanMove => !Integrated;
    public PanelWindow(DesktopViewModel vm, DesktopController controller, ShellIconService icons) : base("Panel")
    {
        this.vm = vm; this.controller = controller; this.icons = icons;
        DataContext = vm; Width = vm.PanelWidth; Height = vm.PanelHeight; Frame.Padding = new(6, 4, 6, 4);
        // Side groups keep their size; the clock centres in what is left, so modules never overlap on a narrow floating bar.
        DockPanel.SetDock(left, Dock.Left); DockPanel.SetDock(right, Dock.Right);
        row.Children.Add(left); row.Children.Add(right); row.Children.Add(center); Frame.Child = row;

        // Left: launcher mark and the focused window, like Waybar's distro logo + hyprland/window modules.
        var mark = new TextBlock { Text = "N", FontWeight = FontWeights.Bold, FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
        mark.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
        var launcher = IconButton(mark, "Uygulamalar", controller.ShowLauncher);
        left.Children.Add(Island(launcher));
        var focused = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(12, 0, 14, 0) }; focused.Children.Add(windowIcon); focused.Children.Add(windowTitle);
        left.Children.Add(Island(focused));

        // Center: clock island doubles as the drag handle when the bar floats.
        date.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        date.SetBinding(TextBlock.TextProperty, new Binding(nameof(DesktopViewModel.ShortDate)));
        var time = Figure(""); time.FontWeight = FontWeights.SemiBold; time.SetBinding(TextBlock.TextProperty, new Binding(nameof(DesktopViewModel.Time)));
        var clock = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(14, 0, 14, 0) }; clock.Children.Add(date); clock.Children.Add(time);
        clockIsland = Island(clock); center.Children.Add(clockIsland); MakeDragHandle(clockIsland);
        clockIsland.ToolTip = "Sürükle veya odaklanınca Alt + yön tuşlarıyla taşı";

        // Right: media (only while a session exists), volume, system load, theme and power.
        var mediaRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(12, 0, 4, 0) };
        var note = Glyph(""); note.Margin = new(0, 0, 8, 0); note.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
        mediaRow.Children.Add(note); mediaRow.Children.Add(mediaText);
        previous = IconButton(Glyph(""), "Önceki parça", () => _ = MediaAsync("previous"));
        playPause = IconButton(Glyph(""), "Oynat", () => _ = MediaAsync(playing ? "pause" : "play"));
        next = IconButton(Glyph(""), "Sonraki parça", () => _ = MediaAsync("next"));
        mediaRow.Children.Add(previous); mediaRow.Children.Add(playPause); mediaRow.Children.Add(next);
        mediaIsland = Island(mediaRow); mediaIsland.Visibility = Visibility.Collapsed; right.Children.Add(mediaIsland);

        var volumeRow = new StackPanel { Orientation = Orientation.Horizontal }; volumeGlyph.Margin = new(0, 0, 8, 0); volumeRow.Children.Add(volumeGlyph); volumeRow.Children.Add(volumeText);
        volumeButton = IconButton(volumeRow, "Sesi kapat / aç · Tekerlekle ayarla", ToggleMute); volumeButton.Padding = new(12, 0, 12, 0);
        volumeButton.PreviewMouseWheel += (_, e) => { ChangeVolume(e.Delta > 0 ? .02f : -.02f); e.Handled = true; };
        volumeButton.PreviewKeyDown += (_, e) => { if (e.Key is Key.Up or Key.Right) { ChangeVolume(.02f); e.Handled = true; } else if (e.Key is Key.Down or Key.Left) { ChangeVolume(-.02f); e.Handled = true; } };
        volumeIsland = Island(volumeButton); right.Children.Add(volumeIsland);

        var system = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(14, 0, 14, 0) };
        system.Children.Add(Label("CPU")); var cpu = Figure(""); cpu.Margin = new(6, 0, 14, 0); cpu.SetBinding(TextBlock.TextProperty, new Binding(nameof(DesktopViewModel.CpuText))); system.Children.Add(cpu);
        system.Children.Add(Label("RAM")); var ram = Figure(""); ram.Margin = new(6, 0, 0, 0); ram.SetBinding(TextBlock.TextProperty, new Binding(nameof(DesktopViewModel.MemoryText))); system.Children.Add(ram);
        var systemIsland = Island(system); systemIsland.SetBinding(ToolTipProperty, new Binding(nameof(DesktopViewModel.MemoryDetail))); right.Children.Add(systemIsland);

        var themeButton = IconButton(Glyph(""), "Renk paleti", null);
        themeButton.Click += (_, _) => { var menu = ThemeMenu(); menu.PlacementTarget = themeButton; menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom; menu.IsOpen = true; };
        var controls = IconButton(Glyph(""), "Kontrol merkezi: ses, medya, masaüstü", controller.ShowControls);
        var power = IconButton(Glyph(""), "Noxelyra menüsü", null);
        power.Click += (_, _) => { var menu = PowerMenu(); menu.PlacementTarget = power; menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom; menu.IsOpen = true; };
        var tools = new StackPanel { Orientation = Orientation.Horizontal }; tools.Children.Add(themeButton); tools.Children.Add(controls); tools.Children.Add(power);
        right.Children.Add(Island(tools));

        ContextMenu = PowerMenu();
        row.SizeChanged += (_, _) => Fit();
        vm.PropertyChanged += Ticked;
        Closed += (_, _) => vm.PropertyChanged -= Ticked;
    }
    private bool playing;
    private static TextBlock Glyph(string code) => new() { Text = code, FontFamily = new FontFamily(IconFont), FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
    private static TextBlock Figure(string text) { var block = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center }; System.Windows.Documents.Typography.SetNumeralAlignment(block, FontNumeralAlignment.Tabular); return block; }
    private static TextBlock Label(string text) { var block = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center }; block.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); return block; }
    private Border Island(UIElement content)
    {
        var island = new Border { Child = content, Margin = new(4, 0, 4, 0), BorderThickness = new(1), VerticalAlignment = VerticalAlignment.Stretch, MinWidth = 36 };
        islands.Add(island); return island;
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
        return menu;
    }
    private ContextMenu PowerMenu()
    {
        var menu = new ContextMenu();
        void Add(string text, Action action) { var item = new MenuItem { Header = text }; item.Click += (_, _) => action(); menu.Items.Add(item); }
        Add("Ayarlar", controller.ShowSettings);
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
        foreach (var island in islands)
        {
            island.Background = fill; island.BorderBrush = border; island.Effect = effect;
            island.BorderThickness = new(model.Settings.BorderWidth);
            island.CornerRadius = new(Math.Min(model.Settings.CornerRadius, height / 2));
            island.SetResourceReference(Border.BorderBrushProperty, model.Settings.Material == "neon" ? "Accent" : "Line");
        }
    }
    // On a narrow floating bar the date gives way first so the time never clips.
    private void Fit()
    {
        if (row.ActualWidth <= 0) return;
        date.Visibility = Visibility.Visible;
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        left.Measure(infinite); right.Measure(infinite); center.Measure(infinite);
        var free = row.ActualWidth - (clockIsland.Parent == center ? left.DesiredSize.Width : 0) - right.DesiredSize.Width;
        if (clockIsland.Parent == center && free < center.DesiredSize.Width) date.Visibility = Visibility.Collapsed;
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
        ReadVolume();
        if (ticks++ % 2 == 0) _ = ReadMediaAsync();
    }
    private void ReadVolume()
    {
        try
        {
            var state = audio.Read(); var level = (int)Math.Round(state.Level * 100);
            volumeText.Text = state.Muted ? "Sessiz" : $"%{level}";
            volumeGlyph.Text = state.Muted ? "" : level == 0 ? "" : level < 34 ? "" : level < 67 ? "" : "";
            volumeIsland.Visibility = Visibility.Visible;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidCastException or UnauthorizedAccessException)
        { Hide(volumeIsland); }
    }
    private void Hide(Border island) { if (island.Visibility == Visibility.Collapsed) return; island.Visibility = Visibility.Collapsed; Fit(); }
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
            if (state is null || (string.IsNullOrWhiteSpace(state.Title) && string.IsNullOrWhiteSpace(state.Artist))) { Hide(mediaIsland); return; }
            mediaText.Text = string.IsNullOrWhiteSpace(state.Artist) ? state.Title : $"{state.Title} — {state.Artist}";
            mediaIsland.ToolTip = mediaText.Text;
            playing = state.Playing;
            ((TextBlock)playPause.Content).Text = playing ? "" : "";
            playPause.ToolTip = playing ? "Duraklat" : "Oynat"; System.Windows.Automation.AutomationProperties.SetName(playPause, (string)playPause.ToolTip);
            playPause.IsEnabled = playing ? state.CanPause : state.CanPlay; previous.IsEnabled = state.CanPrevious; next.IsEnabled = state.CanNext;
            if (mediaIsland.Visibility != Visibility.Visible) { mediaIsland.Visibility = Visibility.Visible; Fit(); }
        }
        catch (Exception) { Hide(mediaIsland); }
        finally { reading = false; }
    }
    private async Task MediaAsync(string action)
    {
        try { if (!await media.InvokeAsync(action)) vm.Status = "Medya uygulaması bu komutu kabul etmedi."; }
        catch (Exception) { vm.Status = "Medya oturumuna ulaşılamadı."; }
        await ReadMediaAsync();
    }
}
