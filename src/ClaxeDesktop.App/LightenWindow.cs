using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

/// <summary>Shows what really uses memory and starts with Windows, and switches only per-user, restorable Windows values.</summary>
internal sealed class LightenWindow : Window
{
    private readonly DesktopController controller;
    private readonly StackPanel memory = new(), startup = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 4, 0, 0) };
    private readonly TextBlock memoryTotal = new() { Margin = new(0, 0, 0, 8) }, startupSummary = new() { Margin = new(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
    private readonly Button restore;
    private readonly bool editable;
    internal int StartupCount { get; private set; }
    public LightenWindow(DesktopController controller)
    {
        this.controller = controller; editable = !controller.IsSafeMode && !controller.ReadOnly;
        Title = "Noxelyra · Hafifletme"; Width = 640; Height = 860; MinWidth = 520; MinHeight = 520; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty, "Canvas"); SetResourceReference(ForegroundProperty, "Text"); Glass.PrepareFramed(this, controller.ViewModel.Settings); Motion.Attach(this, controller.ViewModel, () => controller.IsExiting);
        var root = new StackPanel { Margin = new(28) }; Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(new TextBlock { Text = "Hafifletme", FontSize = 24, FontWeight = FontWeights.SemiBold });
        status.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        status.Text = "Değişiklikler senin Windows tercihlerindir ve Noxelyra kapanınca da kalır. Hepsi aşağıdan geri alınabilir.";
        root.Children.Add(status);

        var mem = Section(root, "Bellek"); memoryTotal.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); mem.Children.Add(memoryTotal); mem.Children.Add(memory);
        var refresh = Button(mem, "Yenile", () => { RenderMemory(); RenderStartup(); }); refresh.HorizontalAlignment = HorizontalAlignment.Left;

        var boot = Section(root, "Windows açılınca başlayanlar"); startupSummary.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); boot.Children.Add(startupSummary); boot.Children.Add(startup);
        var taskManager = Button(boot, "Görev Yöneticisi'nde aç", () => Try(LightenService.OpenTaskManagerStartup)); taskManager.HorizontalAlignment = HorizontalAlignment.Left;

        var extras = Section(root, "Windows ekstraları");
        var phone = new CheckBox { Content = "Telefon Bağlantısı arka planda çalışsın", IsChecked = controller.PhoneInBackground, IsEnabled = editable, ToolTip = "Phone Link ve Cross Device hizmetinin arka plan iznini Ayarlar'daki gibi değiştirir." };
        phone.Click += (_, _) => { Say(controller.SetPhoneInBackground(phone.IsChecked == true)); phone.IsChecked = controller.PhoneInBackground; };
        var web = new CheckBox { Content = "Başlat aramasında web önerileri", IsChecked = controller.WebSearchInStart, IsEnabled = editable, ToolTip = "Kullanıcı ilkesi DisableSearchBoxSuggestions. Explorer yeniden başlayınca veya oturum yenilenince etkinleşir." };
        web.Click += (_, _) => { Say(controller.SetWebSearch(web.IsChecked == true)); web.IsChecked = controller.WebSearchInStart; };
        extras.Children.Add(phone); extras.Children.Add(web);

        var services = Section(root, "Windows servisleri · yönetici izni"); services.Children.Add(serviceList);
        var serviceActions = new WrapPanel(); services.Children.Add(serviceActions);
        disableServices = Button(serviceActions, "Seçilenleri kapat", async () => await RunServices(() => controller.DisableServicesAsync(selected.ToList())));
        restoreServices = Button(serviceActions, "Servisleri eski haline getir", async () => await RunServices(controller.RestoreServicesAsync));
        services.Children.Add(Muted("Windows her seferinde yönetici izni sorar. Yalnız bu üç servise dokunulur; Defender, güncelleme ve kabuk servisleri listede yoktur."));
        RenderServices();

        var updates = Section(root, "Güncellemeler");
        updates.Children.Add(Muted("Windows Update kapatılmaz; güvenlik açığı bırakır. Ayarlar'dan güncellemeleri en fazla 5 hafta duraklatabilirsin."));
        var update = Button(updates, "Windows Update'i aç", () => Try(LightenService.OpenWindowsUpdate)); update.HorizontalAlignment = HorizontalAlignment.Left;

        restore = Button(root, "Hepsini eski haline getir", () => { Say(controller.RestoreLighten()); phone.IsChecked = controller.PhoneInBackground; web.IsChecked = controller.WebSearchInStart; RenderStartup(); });
        restore.HorizontalAlignment = HorizontalAlignment.Left; restore.Margin = new(0, 24, 0, 0);
        RenderMemory(); RenderStartup(); UpdateRestore();
    }
    private readonly StackPanel serviceList = new();
    private readonly HashSet<string> selected = new(StringComparer.OrdinalIgnoreCase);
    private Button disableServices = null!, restoreServices = null!;
    private bool runningServices;
    private void RenderServices()
    {
        serviceList.Children.Clear(); selected.Clear();
        foreach (var tweak in ServiceTweaks.All)
        {
            var state = controller.ReadService(tweak.Name);
            var text = state is null ? "bu Windows'ta yok" : (state.Running ? "çalışıyor" : "durdu") + " · " + state.Start switch { ServiceStart.Disabled => "devre dışı", ServiceStart.Manual => "el ile", ServiceStart.AutomaticDelayed => "otomatik (gecikmeli)", _ => "otomatik" };
            var label = new TextBlock(); label.Inlines.Add(new Run(tweak.Title)); label.Inlines.Add(new Run("   " + text) { Foreground = (System.Windows.Media.Brush)FindResource("Muted"), FontSize = 12 });
            var box = new CheckBox { Content = label, IsEnabled = editable && state is { Start: not ServiceStart.Disabled }, Margin = new(0, 8, 0, 2), ToolTip = tweak.Effect };
            System.Windows.Automation.AutomationProperties.SetName(box, tweak.Title + " kapatılsın");
            var name = tweak.Name;
            box.Click += (_, _) => { if (box.IsChecked == true) selected.Add(name); else selected.Remove(name); UpdateServiceButtons(); };
            serviceList.Children.Add(box);
            var effect = Muted(tweak.Effect); effect.Margin = new(24, 0, 0, 4); effect.FontSize = 12; serviceList.Children.Add(effect);
        }
        UpdateServiceButtons();
    }
    private void UpdateServiceButtons()
    {
        if (disableServices is null) return;
        disableServices.IsEnabled = editable && !runningServices && selected.Count > 0;
        restoreServices.IsEnabled = editable && !runningServices && controller.ServicesChanged;
    }
    private async Task RunServices(Func<Task<string>> action)
    {
        runningServices = true; UpdateServiceButtons(); Say("Yönetici izni bekleniyor…");
        try { Say(await action()); } finally { runningServices = false; RenderServices(); }
    }
    private void Say(string message) { status.Text = message; UpdateRestore(); }
    private void UpdateRestore() => restore.IsEnabled = editable && controller.LightenChanged;
    private void Try(Action action) { try { action(); } catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { Say("Açılamadı: " + ex.Message); } }
    private void RenderMemory()
    {
        memory.Children.Clear();
        memoryTotal.Text = controller.ViewModel.MemoryDetail;
        try
        {
            foreach (var use in LightenService.Memory(10))
            {
                var row = Row(use.Processes > 1 ? $"{use.Name}  ·  {use.Processes} süreç" : use.Name);
                var value = new TextBlock { Text = use.Bytes >= 1L << 30 ? $"{use.Bytes / (double)(1L << 30):0.0} GB" : $"{use.Bytes >> 20} MB", HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                Typography.SetNumeralAlignment(value, FontNumeralAlignment.Tabular); Grid.SetColumn(value, 1); row.Children.Add(value);
                memory.Children.Add(row);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { memory.Children.Add(Muted("Süreç listesi okunamadı: " + ex.Message)); }
    }
    private void RenderStartup()
    {
        startup.Children.Clear();
        IReadOnlyList<StartupEntry> entries;
        try { entries = LightenService.Startup(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { startupSummary.Text = "Başlangıç listesi okunamadı: " + ex.Message; return; }
        StartupCount = entries.Count;
        startupSummary.Text = $"{entries.Count(e => e.Enabled)} açık, {entries.Count(e => !e.Enabled)} kapalı. Yönetici gerektirenler soluk; onları Görev Yöneticisi'nden değiştirebilirsin.";
        foreach (var entry in entries)
        {
            var own = entry.Name.StartsWith("Noxelyra", StringComparison.OrdinalIgnoreCase);
            var box = new CheckBox { IsChecked = entry.Enabled, IsEnabled = editable && entry.UserScope && !own, Margin = new(0, 6, 0, 6), ToolTip = entry.Command };
            var label = new TextBlock(); label.Inlines.Add(new Run(entry.Name));
            var where = own ? "Noxelyra ayarlarından" : entry.UserScope ? (entry.Source == StartupSource.UserFolder ? "Başlangıç klasörü" : "Kullanıcı") : "Yönetici gerekir";
            label.Inlines.Add(new Run("   " + where) { Foreground = (System.Windows.Media.Brush)FindResource("Muted"), FontSize = 12 });
            box.Content = label;
            System.Windows.Automation.AutomationProperties.SetName(box, entry.Name + " açılışta başlasın");
            var current = entry;
            box.Click += (_, _) => { Say(controller.SetStartup(current, box.IsChecked == true)); RenderStartup(); };
            startup.Children.Add(box);
        }
    }
    private static Grid Row(string name)
    {
        var row = new Grid { Margin = new(0, 3, 0, 3) };
        row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        row.Children.Add(new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }
    private static TextBlock Muted(string text) { var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 8) }; block.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); return block; }
    private static StackPanel Section(Panel parent, string title)
    {
        parent.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new(0, 24, 0, 8) });
        var body = new StackPanel(); parent.Children.Add(body); return body;
    }
    private static Button Button(Panel parent, string label, Action action) { var button = new Button { Content = label, Padding = new(12, 7, 12, 7) }; button.Click += (_, _) => action(); parent.Children.Add(button); return button; }
}
