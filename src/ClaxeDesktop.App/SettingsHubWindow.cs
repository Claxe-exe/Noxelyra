using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace ClaxeDesktop.App;

/// <summary>
/// The one "Ayarlar" place: cards for each Noxelyra area and a search box that finds any area, action or catalog setting
/// and opens the right window for it. The detailed windows stay as they are; this is the map to them.
/// </summary>
internal sealed class SettingsHubWindow : Window
{
    private const string IconFont = "Segoe Fluent Icons, Segoe MDL2 Assets";
    private readonly DesktopController controller;
    private readonly IReadOnlyList<HubEntry> entries;
    private readonly TextBox search = new() { FontSize = 16, Padding = new(10, 8, 10, 8) };
    private readonly UniformGrid cards = new() { Columns = 3 };
    private readonly ListBox results = new() { Visibility = Visibility.Collapsed, MaxHeight = 520 };
    private readonly DispatcherTimer debounce = new() { Interval = TimeSpan.FromMilliseconds(120) };
    public SettingsHubWindow(DesktopController controller)
    {
        this.controller = controller; entries = HubEntries.All(controller);
        Title = "Noxelyra · Ayarlar"; Width = 900; Height = 720; MinWidth = 640; MinHeight = 480; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty, "Canvas"); SetResourceReference(ForegroundProperty, "Text");
        Glass.PrepareFramed(this, controller.ViewModel.Settings); Motion.Attach(this, controller.ViewModel, () => controller.IsExiting);
        var root = new DockPanel { Margin = new(28, 24, 28, 24) }; Content = root;
        var header = new StackPanel(); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        header.Children.Add(new TextBlock { Text = "Ayarlar", FontSize = 26, FontWeight = FontWeights.SemiBold });
        var hint = new TextBlock { Text = "Bir alan seç ya da ne aradığını yaz: \"alt tab\", \"duvar kağıdı\", \"saat\"…", FontSize = 13, Margin = new(0, 4, 0, 14) };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); header.Children.Add(hint);
        var field = new Grid(); var placeholder = new TextBlock { Text = "Ayarlarda ara", FontSize = 16, Margin = new(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        placeholder.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); field.Children.Add(search); field.Children.Add(placeholder);
        System.Windows.Automation.AutomationProperties.SetName(search, "Ayarlarda ara");
        header.Children.Add(field);
        var footer = new TextBlock { Text = "Acil çıkış her zaman çalışır: " + controller.GestureText("emergency"), FontSize = 11, Margin = new(0, 12, 0, 0) };
        footer.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var body = new Grid { Margin = new(0, 16, 0, 0) }; root.Children.Add(body);
        body.Children.Add(new ScrollViewer { Content = cards, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        body.Children.Add(results);
        foreach (var entry in entries.Where(e => e.Card)) cards.Children.Add(Card(entry));
        search.TextChanged += (_, _) => { placeholder.Visibility = search.Text.Length == 0 ? Visibility.Visible : Visibility.Hidden; debounce.Stop(); debounce.Start(); };
        debounce.Tick += (_, _) => { debounce.Stop(); Filter(); };
        search.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Down && results.Items.Count > 0) { results.SelectedIndex = 0; (results.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus(); e.Handled = true; }
            else if (e.Key == Key.Enter && results.Items.Count > 0) { Run((HubEntry)((ListBoxItem)results.Items[0]).Tag); e.Handled = true; }
        };
        results.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter && results.SelectedItem is ListBoxItem item) { Run((HubEntry)item.Tag); e.Handled = true; } };
        results.PreviewMouseLeftButtonUp += (_, e) => { if (ItemsControl.ContainerFromElement(results, (DependencyObject)e.OriginalSource) is ListBoxItem item) Run((HubEntry)item.Tag); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { if (search.Text.Length > 0) search.Clear(); else Close(); e.Handled = true; } };
        Loaded += (_, _) => search.Focus();
    }
    private void Filter()
    {
        var query = search.Text.Trim();
        results.Items.Clear();
        if (query.Length == 0) { results.Visibility = Visibility.Collapsed; cards.Visibility = Visibility.Visible; return; }
        foreach (var entry in entries.Where(e => e.Matches(query)).Take(40)) results.Items.Add(Row(entry));
        if (results.Items.Count == 0) results.Items.Add(new ListBoxItem { Content = new TextBlock { Text = "Eşleşen bir ayar yok. Başka bir kelime dene." }, IsEnabled = false });
        results.Visibility = Visibility.Visible; cards.Visibility = Visibility.Collapsed;
    }
    private void Run(HubEntry entry) { try { entry.Open(controller); } catch (Exception ex) when (ex is InvalidOperationException or IOException) { controller.ViewModel.Status = entry.Title + " açılamadı: " + ex.Message; } }
    private Button Card(HubEntry entry)
    {
        var content = new StackPanel();
        var icon = new TextBlock { Text = entry.Glyph, FontFamily = new FontFamily(IconFont), FontSize = 22 }; icon.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
        content.Children.Add(icon);
        content.Children.Add(new TextBlock { Text = entry.Title, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new(0, 10, 0, 4), TextWrapping = TextWrapping.Wrap });
        var description = new TextBlock { Text = entry.Description, FontSize = 12, TextWrapping = TextWrapping.Wrap }; description.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        content.Children.Add(description);
        var button = new Button { Content = content, Padding = new(16, 14, 16, 14), Margin = new(5), HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Top, MinHeight = 128 };
        System.Windows.Automation.AutomationProperties.SetName(button, entry.Title);
        button.Click += (_, _) => Run(entry);
        return button;
    }
    private static ListBoxItem Row(HubEntry entry)
    {
        var row = new DockPanel();
        var icon = new TextBlock { Text = entry.Glyph, FontFamily = new FontFamily(IconFont), FontSize = 16, Margin = new(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        icon.SetResourceReference(TextBlock.ForegroundProperty, "Accent"); DockPanel.SetDock(icon, Dock.Left); row.Children.Add(icon);
        var text = new StackPanel(); text.Children.Add(new TextBlock { Text = entry.Title, FontWeight = FontWeights.SemiBold });
        var detail = new TextBlock { Text = entry.Description, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis }; detail.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        text.Children.Add(detail); row.Children.Add(text);
        return new ListBoxItem { Content = row, Tag = entry };
    }
}
