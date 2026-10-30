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
/// </summary>
internal sealed class LauncherWindow : Window
{
    private sealed record LauncherAction(string Name, string Glyph, Action Run);
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
    internal int ApplicationCount => apps.Count;
    internal IReadOnlyList<Shortcut> InstalledApplications => apps;
    internal TextBox Search => search;
    internal ListBox Results => results;
    public LauncherWindow(DesktopController controller)
    {
        this.controller = controller;
        Title = "Noxelyra · Uygulamalar"; WindowStyle = WindowStyle.None; AllowsTransparency = true; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; Topmost = true; Background = Brushes.Transparent; SetResourceReference(ForegroundProperty, "Text");
        builtins =
        [
            new("Hızlı ayarlar", "", controller.ShowControls), new("Kontrol merkezi", "", controller.ShowFullControls),
            new("Klavye ve kısayollar", "", controller.ShowKeyboard), new("Hafifletme", "", controller.ShowLighten),
            new("Widget atölyesi", "", controller.Widgets.ShowEditor), new("Ayarlar", "", controller.ShowHub), new("Kısayollar ve ipuçları", "", controller.ShowShortcuts),
            new("Noxelyra masaüstü", "", controller.ShowDesktopSpace), new("Windows uygulamaları", "", () => controller.OpenSystem("apps")),
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
        var hint = new TextBlock { Text = "Uygulama veya Noxelyra eylemi ara", FontSize = 20, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false, Margin = new(8, 0, 0, 0) };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        var input = new Grid(); input.Children.Add(hint); input.Children.Add(search); fieldRow.Children.Add(input);
        search.SetResourceReference(ForegroundProperty, "Text"); search.SetResourceReference(TextBox.CaretBrushProperty, "Accent");
        System.Windows.Automation.AutomationProperties.SetName(search, "Uygulama veya Noxelyra eylemi ara");
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
        grid = controller.Keyboard.LauncherStyle == "grid";
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
        if (!Motion.Enabled(controller.ViewModel)) { Hide(); return; }
        Motion.To(stage, OpacityProperty, 0, 120, () => { Hide(); stage.BeginAnimation(OpacityProperty, null); stage.Opacity = 1; });
    }
    private void Navigate(KeyEventArgs e)
    {
        var columns = grid ? Math.Max(1, (int)((card.ActualWidth - 36) / 150)) : 1;
        int step = e.Key switch { Key.Down => columns, Key.Up => -columns, Key.Right when grid && !search.IsKeyboardFocused => 1, Key.Left when grid && !search.IsKeyboardFocused => -1, Key.Tab => Keyboard.Modifiers == ModifierKeys.Shift ? -1 : 1, _ => 0 };
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
        var culture = CultureInfo.GetCultureInfo("tr-TR").CompareInfo; var query = search.Text.Trim();
        bool Match(string name) => query.Length == 0 || culture.IndexOf(name, query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
        // Pinned apps first, then names that start with the query, then the rest alphabetically.
        var all = controller.ViewModel.Shortcuts.Concat(apps).DistinctBy(a => a.Target, StringComparer.OrdinalIgnoreCase).Where(a => Match(a.Name))
            .OrderBy(a => controller.IsPinned(a.Target) ? 0 : 1).ThenBy(a => query.Length > 0 && culture.IsPrefix(a.Name, query, CompareOptions.IgnoreCase) ? 0 : 1).ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(grid ? 120 : 40).ToArray();
        var actions = query.Length == 0 && !grid ? [] : builtins.Where(b => Match(b.Name)).ToArray();
        foreach (var action in actions) results.Items.Add(Row(action.Name, "Noxelyra", action, null, current));
        foreach (var app in all) results.Items.Add(Row(app.Name, controller.IsPinned(app.Target) ? "Dock'ta" : "Uygulama", app, app.Target, current));
        if (results.Items.Count > 0) results.SelectedIndex = 0;
        status.Text = results.Items.Count == 0 ? "Eşleşme yok." : $"{apps.Count} uygulama · Enter aç · oklar gez · Esc kapat" + (controller.Keyboard.SuperLauncher ? " · Win tekrar basınca kapanır" : "");
    }
    private ListBoxItem Row(string name, string kind, object tag, string? iconPath, int current)
    {
        FrameworkElement visual;
        if (iconPath is null) { var glyph = new TextBlock { Text = ((LauncherAction)tag).Glyph, FontFamily = new FontFamily(IconFont), FontSize = grid ? 30 : 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; glyph.SetResourceReference(TextBlock.ForegroundProperty, "Accent"); visual = glyph; }
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
        }
        return item;
    }
    private async Task SetIconAsync(Image image, string path, int current) { var asset = await icons.GetAsync(path); if (!closed && generation == current) image.Source = asset.Icon; }
    internal void RunSelected()
    {
        if (results.SelectedItem is not ListBoxItem item) return;
        if (item.Tag is Shortcut app) controller.Launch(app);
        else if (item.Tag is LauncherAction action) action.Run();
        Leave();
    }
}
