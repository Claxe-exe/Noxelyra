using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Windows.Devices.WiFi;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

/// <summary>Full-card panels inside the control center: sound outputs and the app mixer, Wi-Fi networks and Bluetooth devices.</summary>
internal sealed partial class IslandCard
{
    private readonly Grid detail = new() { Visibility = Visibility.Collapsed };
    private readonly TextBlock detailTitle = new() { FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel detailActions = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly ContentControl detailBody = new();
    private string? detailKind, mixerSignature;
    private readonly Dictionary<string, (Slider Volume, Button Mute, TextBlock Glyph)> mixerRows = new(StringComparer.OrdinalIgnoreCase);
    private Slider? masterSlider;
    private long lastVolumeSet;
    private int detailGeneration;

    private void BuildDetail(Grid host)
    {
        detail.SetResourceReference(BackgroundProperty, "Surface");
        var layout = new DockPanel { Margin = new(18, 14, 18, 14) }; detail.Children.Add(layout);
        var top = new DockPanel { Margin = new(0, 0, 0, 12) }; DockPanel.SetDock(top, Dock.Top); layout.Children.Add(top);
        var back = new Button { Content = Glyph("\uE72B", 13), Padding = new(10, 6, 10, 6), Margin = new(0, 0, 10, 0), ToolTip = "Kontrol merkezine dön" };
        System.Windows.Automation.AutomationProperties.SetName(back, "Geri"); back.Click += (_, _) => { Touch(); CloseDetail(); };
        DockPanel.SetDock(back, Dock.Left); top.Children.Add(back);
        DockPanel.SetDock(detailActions, Dock.Right); top.Children.Add(detailActions); top.Children.Add(detailTitle);
        layout.Children.Add(new ScrollViewer { Content = detailBody, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        host.Children.Add(detail);
    }
    private void OpenDetail(string kind, string title)
    {
        Touch(); detailKind = kind; detailGeneration++; mixerSignature = null; detailTitle.Text = title; detailActions.Children.Clear(); detailBody.Content = null; detail.Visibility = Visibility.Visible;
    }
    private void CloseDetail() { detailKind = null; detailGeneration++; mixerRows.Clear(); masterSlider = null; detailBody.Content = null; detail.Visibility = Visibility.Collapsed; }
    private void DetailLink(string text, string settingsKey) => detailActions.Children.Add(Small(BarPopup.Action("\uE713", text, () => { Leave(); controller.OpenSystem(settingsKey); })));
    private Button RowButton(string text, Action click)
    {
        var button = new Button { Content = text, Padding = new(10, 3, 10, 3), FontSize = 11, Margin = new(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        button.Click += (_, _) => { Touch(); click(); }; return button;
    }
    private static Border ListRow(UIElement content)
    {
        var row = new Border { Child = content, CornerRadius = new(10), Padding = new(10, 7, 10, 7), Margin = new(0, 0, 0, 4), BorderThickness = new(1) };
        row.SetResourceReference(Border.BackgroundProperty, "Card"); row.SetResourceReference(Border.BorderBrushProperty, "Line"); return row;
    }
    private void TickDetail() { if (detailKind == "sound") RenderSound(); }

    // ---- Sound ----
    private void ShowSound() { OpenDetail("sound", "Ses"); DetailLink("Windows ses ayarları", "sound"); RenderSound(); }
    private void RenderSound()
    {
        IReadOnlyList<AudioOutput> outputs; IReadOnlyList<AudioApp> apps;
        try { outputs = AppAudio.Outputs(); apps = AppAudio.Apps(); }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        { if (mixerSignature != "failed") { mixerSignature = "failed"; detailBody.Content = BarPopup.Muted("Windows ses aygıtlarını okuyamadı. Bağlı bir ses çıkışı olmayabilir.", 12); } return; }
        var signature = string.Join("|", outputs.Select(o => o.Id + o.Default)) + "#" + string.Join("|", apps.Select(a => a.Key));
        if (signature == mixerSignature)
        {
            foreach (var app in apps) if (mixerRows.TryGetValue(app.Key, out var row)) { if (!row.Volume.IsMouseCaptureWithin) row.Volume.Value = app.Level * 100; row.Glyph.Text = app.Muted ? "\uE74F" : "\uE767"; }
            return;
        }
        mixerSignature = signature; mixerRows.Clear();
        var grid = new Grid(); grid.ColumnDefinitions.Add(new() { Width = new(300) }); grid.ColumnDefinitions.Add(new() { Width = new(18) }); grid.ColumnDefinitions.Add(new());

        var left = new StackPanel();
        left.Children.Add(Section("\uE995", "Genel ses"));
        masterSlider = new Slider { Minimum = 0, Maximum = 100, Margin = new(0, 0, 0, 12) }; System.Windows.Automation.AutomationProperties.SetName(masterSlider, "Genel ses");
        try { masterSlider.Value = audio.Read().Level * 100; } catch (Exception ex) when (ex is COMException or InvalidCastException) { masterSlider.IsEnabled = false; }
        masterSlider.ValueChanged += (_, _) => { Touch(); if (Throttled()) return; TryAudio(() => audio.SetLevel((float)(masterSlider.Value / 100))); };
        masterSlider.PreviewMouseLeftButtonUp += (_, _) => TryAudio(() => audio.SetLevel((float)(masterSlider.Value / 100)));
        left.Children.Add(masterSlider);
        left.Children.Add(Section("\uE7F6", "Çıkış aygıtı"));
        if (outputs.Count == 0) left.Children.Add(BarPopup.Muted("Etkin ses çıkışı yok.", 12));
        foreach (var output in outputs)
        {
            var face = new DockPanel { Width = 272 };
            var check = Glyph(output.Default ? "\uE73E" : "", 12); check.Width = 18; check.SetResourceReference(TextBlock.ForegroundProperty, "Accent"); DockPanel.SetDock(check, Dock.Left); face.Children.Add(check);
            face.Children.Add(new TextBlock { Text = output.Name, FontSize = 12, TextWrapping = TextWrapping.Wrap, FontWeight = output.Default ? FontWeights.SemiBold : FontWeights.Normal });
            var item = new Button { Content = face, Padding = new(10, 8, 10, 8), Margin = new(0, 0, 0, 4), HorizontalContentAlignment = HorizontalAlignment.Left };
            // The active device stays bright and outlined; a disabled look would hide the one that matters most.
            if (output.Default) item.SetResourceReference(BorderBrushProperty, "Accent");
            System.Windows.Automation.AutomationProperties.SetName(item, output.Name + (output.Default ? ", seçili" : ""));
            var id = output.Id; var name = output.Name;
            item.Click += (_, _) =>
            {
                Touch(); if (output.Default) return;
                if (AppAudio.SetDefaultOutput(id)) { controller.ViewModel.Status = name + " varsayılan çıkış oldu."; mixerSignature = null; RenderSound(); }
                else { controller.ViewModel.Status = "Windows çıkış aygıtını buradan değiştirmeye izin vermedi; ses ayarları açılıyor."; Leave(); controller.OpenSystem("sound"); }
            };
            left.Children.Add(item);
        }
        grid.Children.Add(left);

        var right = new StackPanel(); Grid.SetColumn(right, 2); grid.Children.Add(right);
        right.Children.Add(Section("\uE9E9", "Uygulamalar"));
        if (apps.Count == 0) right.Children.Add(BarPopup.Muted("Şu anda ses çıkaran uygulama yok. Uygulama ses çalınca burada görünür.", 12));
        foreach (var app in apps)
        {
            var key = app.Key; var line = new DockPanel();
            var glyph = Glyph(app.Muted ? "\uE74F" : "\uE767", 13);
            var mute = new Button { Content = glyph, Padding = new(7, 5, 7, 5), Margin = new(8, 0, 0, 0), Background = Brushes.Transparent, BorderThickness = new(0), ToolTip = "Sesi kapat / aç" };
            System.Windows.Automation.AutomationProperties.SetName(mute, app.Label + " sesini kapat veya aç");
            mute.Click += (_, _) => { Touch(); TryAudio(() => { var current = AppAudio.Apps().FirstOrDefault(a => a.Key == key); if (current is not null) AppAudio.SetApp(key, null, !current.Muted); }); RenderSound(); };
            DockPanel.SetDock(mute, Dock.Right); line.Children.Add(mute);
            var head = new StackPanel { Orientation = Orientation.Horizontal, Width = 118 };
            var icon = new Image { Width = 16, Height = 16, Margin = new(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center }; if (app.Path is { } path) _ = LoadIconAsync(path, icon); head.Children.Add(icon);
            head.Children.Add(new TextBlock { Text = app.Label, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 94, VerticalAlignment = VerticalAlignment.Center, ToolTip = app.Label });
            DockPanel.SetDock(head, Dock.Left); line.Children.Add(head);
            var volume = new Slider { Minimum = 0, Maximum = 100, Value = app.Level * 100, Margin = new(0), VerticalAlignment = VerticalAlignment.Center }; System.Windows.Automation.AutomationProperties.SetName(volume, app.Label + " ses düzeyi");
            volume.ValueChanged += (_, _) => { if (!volume.IsMouseCaptureWithin && !volume.IsKeyboardFocused) return; Touch(); if (Throttled()) return; TryAudio(() => AppAudio.SetApp(key, (float)(volume.Value / 100), null)); };
            volume.PreviewMouseLeftButtonUp += (_, _) => TryAudio(() => AppAudio.SetApp(key, (float)(volume.Value / 100), null));
            line.Children.Add(volume);
            right.Children.Add(ListRow(line)); mixerRows[key] = (volume, mute, glyph);
        }
        detailBody.Content = grid;
    }
    // A slider fires many times a second; each change walks the audio sessions, so at most 25 a second reach Windows. The release always does.
    private bool Throttled() { var now = Environment.TickCount64; if (now - lastVolumeSet < 40) return true; lastVolumeSet = now; return false; }

    // ---- Wi-Fi ----
    private void ShowWifi()
    {
        OpenDetail("wifi", "Wi-Fi ağları");
        detailActions.Children.Add(Small(BarPopup.Action("\uE72C", "Yenile", () => { Touch(); _ = RenderWifiAsync(); })));
        DetailLink("Windows ağ ayarları", "network");
        _ = RenderWifiAsync();
    }
    private async Task RenderWifiAsync()
    {
        var generation = ++detailGeneration;
        var body = new StackPanel(); detailBody.Content = body; body.Children.Add(BarPopup.Muted("Ağlar aranıyor…", 12));
        var (items, problem, location) = await WifiService.ScanAsync();
        if (generation != detailGeneration || detailKind != "wifi") return;
        body.Children.Clear();
        if (problem is not null)
        {
            body.Children.Add(BarPopup.Muted(problem, 12));
            if (location) { var open = Small(BarPopup.Action("\uE713", "Konum ayarlarını aç", () => OpenUri("ms-settings:privacy-location"))); open.Margin = new(0, 10, 0, 0); open.HorizontalAlignment = HorizontalAlignment.Left; body.Children.Add(open); }
            return;
        }
        foreach (var network in items)
        {
            var line = new DockPanel();
            var action = network.Connected ? RowButton("Bağlantıyı kes", () => { network.Adapter.Disconnect(); controller.ViewModel.Status = network.Name + " bağlantısı kesildi."; _ = RenderWifiAsync(); }) : RowButton("Bağlan", () => _ = ConnectWifiAsync(network));
            DockPanel.SetDock(action, Dock.Right); line.Children.Add(action);
            var signal = Glyph(network.Bars switch { >= 4 => "\uE701", 3 => "\uEC3F", 2 => "\uEC3E", _ => "\uEC3D" }, 15); signal.Margin = new(0, 0, 10, 0); signal.SetResourceReference(TextBlock.ForegroundProperty, network.Connected ? "Accent" : "Text"); DockPanel.SetDock(signal, Dock.Left); line.Children.Add(signal);
            if (network.Secured) { var lockGlyph = Glyph("\uE72E", 11); lockGlyph.Margin = new(0, 0, 8, 0); lockGlyph.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); DockPanel.SetDock(lockGlyph, Dock.Left); line.Children.Add(lockGlyph); }
            line.Children.Add(new TextBlock { Text = network.Connected ? network.Name + "  ·  bağlı" : network.Name, FontSize = 12, FontWeight = network.Connected ? FontWeights.SemiBold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            body.Children.Add(ListRow(line));
        }
    }
    private async Task ConnectWifiAsync(WifiService.Network network)
    {
        if (network.Enterprise) { controller.ViewModel.Status = "Kurumsal ağlar Windows ayarlarından bağlanır."; Leave(); controller.OpenSystem("network"); return; }
        controller.ViewModel.Status = network.Name + " ağına bağlanılıyor…";
        var result = await WifiService.ConnectAsync(network, null);
        // A secured network without a saved profile asks for its password in a real window.
        if (result == WiFiConnectionStatus.InvalidCredential && network.Secured) { new WifiPasswordWindow(controller, network, message => { controller.ViewModel.Status = message; _ = ConnectivityService.ReadAsync(); }).Show(); return; }
        controller.ViewModel.Status = WifiService.Message(result, network.Name); _ = ConnectivityService.ReadAsync();
        if (detailKind == "wifi") await RenderWifiAsync();
    }

    // ---- Bluetooth ----
    private void ShowBluetooth() { OpenDetail("bluetooth", "Bluetooth cihazları"); detailActions.Children.Add(Small(BarPopup.Action("\uE710", "Cihaz ekle veya bağlan", () => { Leave(); controller.OpenSystem("bluetooth"); }))); _ = RenderBluetoothAsync(); }
    private async Task RenderBluetoothAsync()
    {
        var generation = ++detailGeneration;
        var body = new StackPanel(); detailBody.Content = body;
        if (ConnectivityService.Last.BluetoothOn != true) { body.Children.Add(BarPopup.Muted(ConnectivityService.Last.BluetoothOn is null ? "Bu bilgisayarda Bluetooth bulunamadı." : "Bluetooth kapalı. Kontrol merkezindeki kutucuktan açabilirsin.", 12)); return; }
        body.Children.Add(BarPopup.Muted("Cihazlar okunuyor…", 12));
        IReadOnlyList<BluetoothList.Device> devices;
        try { devices = await BluetoothList.PairedAsync(); }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or InvalidOperationException) { if (generation == detailGeneration) { body.Children.Clear(); body.Children.Add(BarPopup.Muted("Bluetooth cihazları okunamadı.", 12)); } return; }
        if (generation != detailGeneration || detailKind != "bluetooth") return;
        body.Children.Clear();
        var note = BarPopup.Muted("Windows, eşleşmiş bir cihazı başka bir uygulamanın bağlamasına izin veren belgelenmiş bir yol sunmuyor; bağlanmak için Windows ayarları açılır.", 11); note.Margin = new(0, 0, 0, 10); body.Children.Add(note);
        if (devices.Count == 0) body.Children.Add(BarPopup.Muted("Eşleşmiş cihaz yok.", 12));
        foreach (var device in devices)
        {
            var line = new DockPanel();
            if (!device.Connected) { var connect = RowButton("Windows'ta bağlan", () => { Leave(); controller.OpenSystem("bluetooth"); }); DockPanel.SetDock(connect, Dock.Right); line.Children.Add(connect); }
            var glyph = Glyph("\uE702", 14); glyph.Margin = new(0, 0, 10, 0); glyph.SetResourceReference(TextBlock.ForegroundProperty, device.Connected ? "Accent" : "Muted"); DockPanel.SetDock(glyph, Dock.Left); line.Children.Add(glyph);
            line.Children.Add(new TextBlock { Text = device.Connected ? device.Name + "  ·  bağlı" : device.Name, FontSize = 12, FontWeight = device.Connected ? FontWeights.SemiBold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            body.Children.Add(ListRow(line));
        }
    }
}
