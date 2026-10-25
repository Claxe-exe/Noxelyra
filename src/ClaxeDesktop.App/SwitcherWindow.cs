using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

/// <summary>
/// Alt+Tab replacement: live DWM previews of open windows in a themed card. Keys arrive through the keyboard hook,
/// so the window never takes focus; releasing Alt switches, Esc cancels, a click picks a window.
/// Not layered (DWM thumbnails need a normal window); Windows rounds its corners.
/// </summary>
internal sealed class SwitcherWindow : Window
{
    private const double Box = 220, BoxHeight = 138;
    private readonly DesktopController controller;
    private readonly WrapPanel cards = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock title = new() { FontSize = 15, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new(0, 14, 0, 0) };
    private readonly List<(AppWindow Window, Border Card, Border Preview, nint Thumbnail)> items = [];
    private readonly ShellIconService icons;
    private int selected;
    internal bool Open => IsVisible;
    public SwitcherWindow(DesktopController controller, ShellIconService icons)
    {
        this.controller = controller; this.icons = icons;
        Title = "Noxelyra · Pencereler"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight; SetResourceReference(BackgroundProperty, "Surface"); SetResourceReference(ForegroundProperty, "Text");
        var root = new StackPanel { Margin = new(22) }; root.Children.Add(cards); root.Children.Add(title); Content = root;
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle; SystemBackdrop.Round(handle);
            SystemBackdrop.Border(handle, ThemePresets.Resolve(controller.ViewModel.Settings).Line);
        };
        Deactivated += (_, _) => Cancel();
    }
    /// <summary>Shows previews of <paramref name="windows"/> (most recent first) with the next or previous one selected.</summary>
    internal void Present(IReadOnlyList<AppWindow> windows, bool previous)
    {
        Clear();
        if (windows.Count == 0) { controller.SwitcherClosed(); return; }
        var monitor = controller.MonitorUnderPointer();
        cards.MaxWidth = monitor.Width / monitor.Scale * .86;
        foreach (var window in windows.Take(18))
        {
            var preview = new Border { Width = Box, Height = BoxHeight, CornerRadius = new(10) };
            preview.SetResourceReference(Border.BackgroundProperty, "Canvas");
            var icon = new Image { Width = 40, Height = 40, Opacity = .55 }; preview.Child = icon;
            if (window.Executable is { } exe) _ = LoadIconAsync(icon, exe);
            var label = new DockPanel { Margin = new(2, 8, 2, 0), Width = Box };
            var small = new Image { Width = 16, Height = 16, Margin = new(0, 0, 6, 0) }; DockPanel.SetDock(small, Dock.Left); label.Children.Add(small);
            if (window.Executable is { } path) _ = LoadIconAsync(small, path);
            label.Children.Add(new TextBlock { Text = window.Title, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 12 });
            var stack = new StackPanel(); stack.Children.Add(preview); stack.Children.Add(label);
            var card = new Border { Child = stack, Padding = new(10), Margin = new(6), CornerRadius = new(14), BorderThickness = new(2), BorderBrush = Brushes.Transparent, Cursor = Cursors.Hand };
            var target = window; card.MouseLeftButtonUp += (_, _) => { selected = items.FindIndex(i => i.Window == target); Commit(); };
            cards.Children.Add(card); items.Add((window, card, preview, 0));
        }
        selected = windows.Count == 1 ? 0 : previous ? items.Count - 1 : 1;
        Show();
        var dpi = VisualTreeHelper.GetDpi(this);
        Left = (monitor.X / monitor.Scale) + (monitor.Width / monitor.Scale - ActualWidth) / 2; Top = (monitor.Y / monitor.Scale) + (monitor.Height / monitor.Scale - ActualHeight) / 2;
        UpdateLayout();
        var handle = new WindowInteropHelper(this).Handle;
        for (int i = 0; i < items.Count; i++)
        {
            var (window, card, preview, _) = items[i];
            var thumbnail = DwmThumbnail.Register(handle, window.Handle);
            if (thumbnail != 0)
            {
                // Fit the window's aspect ratio inside the preview box, centred.
                var (w, h) = DwmThumbnail.SourceSize(thumbnail); var origin = preview.TranslatePoint(new Point(0, 0), this);
                double scale = w > 0 && h > 0 ? Math.Min(Box / w, BoxHeight / h) : 1, fw = w > 0 ? w * scale : Box, fh = h > 0 ? h * scale : BoxHeight;
                double x = origin.X + (Box - fw) / 2, y = origin.Y + (BoxHeight - fh) / 2;
                DwmThumbnail.Show(thumbnail, (int)(x * dpi.DpiScaleX), (int)(y * dpi.DpiScaleY), (int)((x + fw) * dpi.DpiScaleX), (int)((y + fh) * dpi.DpiScaleY));
            }
            items[i] = (window, card, preview, thumbnail);
        }
        Highlight();
    }
    internal void Move(int step) { if (items.Count == 0) return; selected = (selected + step + items.Count) % items.Count; Highlight(); }
    internal void Commit()
    {
        if (items.Count == 0 || !IsVisible) { Cancel(); return; }
        var window = items[Math.Clamp(selected, 0, items.Count - 1)].Window;
        Cancel();
        if (!controller.ActivateWindow(window)) controller.ViewModel.Status = "Windows bu pencereye geçişe izin vermedi.";
    }
    internal void Cancel() { if (!IsVisible) return; Clear(); Hide(); controller.SwitcherClosed(); }
    private void Highlight()
    {
        for (int i = 0; i < items.Count; i++)
        {
            var on = i == selected;
            if (on) { items[i].Card.SetResourceReference(Border.BorderBrushProperty, "Accent"); items[i].Card.SetResourceReference(Border.BackgroundProperty, "Card"); }
            else { items[i].Card.BorderBrush = Brushes.Transparent; items[i].Card.Background = Brushes.Transparent; }
        }
        title.Text = items.Count > 0 ? items[selected].Window.Title : "";
    }
    private void Clear()
    {
        foreach (var item in items) DwmThumbnail.Unregister(item.Thumbnail);
        items.Clear(); cards.Children.Clear();
    }
    private async Task LoadIconAsync(Image image, string path) { try { var asset = await icons.GetAsync(path); image.Source = asset.Icon; } catch { /* Title stays as the identifier. */ } }
}
