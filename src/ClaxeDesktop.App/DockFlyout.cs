using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

/// <summary>The small panel over a dock icon: live previews of an app's windows, or the contents of a pinned folder.</summary>
internal sealed class DockFlyout : Window
{
    private const string IconFont = "Segoe Fluent Icons, Segoe MDL2 Assets";
    private const double Box = 190, BoxHeight = 112;
    private readonly List<(AppWindow Window, Border Preview, nint Thumbnail)> previews = [];
    private readonly DispatcherTimer closeDelay = new() { Interval = TimeSpan.FromMilliseconds(240) };
    private readonly ShellIconService icons;
    private readonly Button anchor;
    private readonly bool bottomEdge;
    internal Button Anchor => anchor;
    /// <summary>True while the pointer is on the flyout, so the dock keeps itself open under it.</summary>
    internal bool Hovered => IsMouseOver;
    internal DockFlyout(Button anchor, ShellIconService icons, ThemePalette palette, bool bottomEdge)
    {
        this.anchor = anchor; this.icons = icons; this.bottomEdge = bottomEdge;
        AltTab.Hide(this); Title = "Noxelyra · Dock"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; ShowActivated = false; Topmost = true; SizeToContent = SizeToContent.WidthAndHeight;
        SetResourceReference(BackgroundProperty, "Surface"); SetResourceReference(ForegroundProperty, "Text");
        SourceInitialized += (_, _) => { var handle = new WindowInteropHelper(this).Handle; SystemBackdrop.Round(handle); SystemBackdrop.Border(handle, palette.Line); };
        MouseEnter += (_, _) => closeDelay.Stop();
        MouseLeave += (_, _) => CloseSoon();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        closeDelay.Tick += (_, _) => { closeDelay.Stop(); if (!IsMouseOver && !anchor.IsMouseOver) Close(); };
        Closed += (_, _) => { closeDelay.Stop(); foreach (var item in previews) DwmThumbnail.Unregister(item.Thumbnail); previews.Clear(); };
    }
    internal void CloseSoon() { closeDelay.Stop(); closeDelay.Start(); }
    internal void KeepOpen() => closeDelay.Stop();
    /// <summary>Above the icon on a bottom dock, below it on a top dock; always kept inside the monitor.</summary>
    private void Place()
    {
        var handle = new WindowInteropHelper(this).Handle; if (WindowOps.Read(handle) is not { } self) return;
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var corner = anchor.PointToScreen(new Point(0, 0)); var work = WindowOps.WorkArea(self.Monitor); var bounds = WindowOps.MonitorBounds(self.Monitor);
        var (width, height) = (self.Bounds.Width, self.Bounds.Height);
        var x = corner.X + anchor.ActualWidth * scale / 2 - width / 2; var y = bottomEdge ? corner.Y - height - 10 * scale : corner.Y + anchor.ActualHeight * scale + 10 * scale;
        x = Math.Clamp(x, bounds.X + 8, Math.Max(bounds.X + 8, bounds.X + bounds.Width - width - 8)); y = Math.Clamp(y, work.Y + 4, Math.Max(work.Y + 4, bounds.Y + bounds.Height - height - 4));
        WindowPlacement.Move(handle, (int)x, (int)y);
    }

    // ---- Window previews ----
    internal void ShowWindows(IReadOnlyList<AppWindow> windows, Action<AppWindow> activate, Action<AppWindow> close)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(10) }; Content = row;
        foreach (var window in windows.Take(5))
        {
            var preview = new Border { Width = Box, Height = BoxHeight, CornerRadius = new(8) }; preview.SetResourceReference(Border.BackgroundProperty, "Canvas");
            var title = new TextBlock { Text = window.Title, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            var x = new Button { Content = new TextBlock { Text = "\uE711", FontFamily = new FontFamily(IconFont), FontSize = 10 }, Width = 24, Height = 22, Padding = new(0), Margin = new(4, 0, 0, 0), Background = Brushes.Transparent, BorderThickness = new(0), ToolTip = "Pencereyi kapat" };
            System.Windows.Automation.AutomationProperties.SetName(x, window.Title + " penceresini kapat");
            var target = window; x.Click += (_, _) => { close(target); Close(); };
            var head = new DockPanel { Width = Box, Margin = new(0, 0, 0, 6) }; DockPanel.SetDock(x, Dock.Right); head.Children.Add(x); head.Children.Add(title);
            var stack = new StackPanel(); stack.Children.Add(head); stack.Children.Add(preview);
            var card = new Border { Child = stack, Padding = new(8), Margin = new(2), CornerRadius = new(10), Cursor = Cursors.Hand, Background = Brushes.Transparent };
            card.MouseEnter += (_, _) => card.SetResourceReference(Border.BackgroundProperty, "Hover"); card.MouseLeave += (_, _) => card.Background = Brushes.Transparent;
            card.MouseLeftButtonUp += (_, e) => { if (e.OriginalSource is DependencyObject source && IsInside(source, x)) return; activate(target); Close(); };
            row.Children.Add(card); previews.Add((window, preview, 0));
        }
        Show();
        UpdateLayout(); Place();
        var handle = new WindowInteropHelper(this).Handle; var dpi = VisualTreeHelper.GetDpi(this);
        for (var i = 0; i < previews.Count; i++)
        {
            var (window, preview, _) = previews[i];
            var thumbnail = DwmThumbnail.Register(handle, window.Handle);
            if (thumbnail != 0)
            {
                // Fit the window's aspect ratio inside the box, centred.
                var (w, h) = DwmThumbnail.SourceSize(thumbnail); var origin = preview.TranslatePoint(new Point(0, 0), this);
                double scale = w > 0 && h > 0 ? Math.Min(Box / w, BoxHeight / h) : 1, fw = w > 0 ? w * scale : Box, fh = h > 0 ? h * scale : BoxHeight;
                double left = origin.X + (Box - fw) / 2, top = origin.Y + (BoxHeight - fh) / 2;
                DwmThumbnail.Show(thumbnail, (int)(left * dpi.DpiScaleX), (int)(top * dpi.DpiScaleY), (int)((left + fw) * dpi.DpiScaleX), (int)((top + fh) * dpi.DpiScaleY));
            }
            previews[i] = (window, preview, thumbnail);
        }
    }
    private static bool IsInside(DependencyObject source, DependencyObject parent)
    {
        for (DependencyObject? at = source; at is not null; at = at is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(at) : LogicalTreeHelper.GetParent(at)) if (at == parent) return true;
        return false;
    }

    // ---- Pinned folder ----
    internal void ShowFolder(string name, string folder, Action<string> open)
    {
        ShowActivated = true;
        var root = new StackPanel { Margin = new(14), MaxWidth = 460 }; Content = root;
        var head = new DockPanel { Margin = new(2, 0, 2, 10) };
        var whole = new Button { Content = "Klasörü aç", Padding = new(10, 4, 10, 4), FontSize = 12 }; whole.Click += (_, _) => { open(folder); Close(); };
        DockPanel.SetDock(whole, Dock.Right); head.Children.Add(whole);
        head.Children.Add(new TextBlock { Text = name, FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        root.Children.Add(head);
        var grid = new WrapPanel(); var body = new ScrollViewer { Content = grid, MaxHeight = 330, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; root.Children.Add(body);
        string[] entries;
        try { entries = Directory.EnumerateFileSystemEntries(folder, "*", new EnumerationOptions { AttributesToSkip = FileAttributes.Hidden | FileAttributes.System, IgnoreInaccessible = true }).OrderBy(e => !Directory.Exists(e)).ThenBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase).Take(40).ToArray(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { entries = []; root.Children.Add(new TextBlock { Text = "Klasör okunamadı: " + ex.Message, TextWrapping = TextWrapping.Wrap, FontSize = 12 }); }
        if (entries.Length == 0 && root.Children.Count == 2) root.Children.Add(new TextBlock { Text = "Bu klasör boş.", FontSize = 12, Margin = new(2, 0, 2, 4) });
        foreach (var entry in entries)
        {
            var image = new Image { Width = 36, Height = 36, HorizontalAlignment = HorizontalAlignment.Center }; _ = LoadIconAsync(image, entry);
            var stack = new StackPanel { Width = 78 }; stack.Children.Add(image);
            stack.Children.Add(new TextBlock { Text = Path.GetFileNameWithoutExtension(entry), FontSize = 11, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = 30, Margin = new(0, 4, 0, 0) });
            var item = new Button { Content = stack, Padding = new(4, 8, 4, 6), Margin = new(2), ToolTip = entry, Style = (Style)FindResource("DockTile") };
            System.Windows.Automation.AutomationProperties.SetName(item, Path.GetFileName(entry));
            var target = entry; item.Click += (_, _) => { open(target); Close(); };
            grid.Children.Add(item);
        }
        Deactivated += (_, _) => { if (IsVisible) Close(); };
        Show(); UpdateLayout(); Place(); Activate();
    }
    private async Task LoadIconAsync(Image image, string path) { var asset = await icons.GetAsync(path); if (asset.Icon is not null) image.Source = asset.Icon; }
}
