using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;

namespace ClaxeDesktop.App;
/// <summary>
/// The bar's richer modules: open apps (Hyprland-workspace style), network speed, Android-style status icons and the
/// drop-down panels behind network and system load. Every figure is read from Windows; nothing is estimated.
/// </summary>
public sealed partial class PanelWindow
{
    private sealed class AppSlot { public required Button Button; public required Border Indicator; public required Image Icon; public List<AppWindow> Windows = []; }
    private readonly StackPanel appsRow = new() { Orientation = Orientation.Horizontal, Margin = new(3, 0, 3, 0) };
    private readonly Dictionary<string, AppSlot> appSlots = new();
    private readonly List<string> appOrder = [];
    private nint lastForeground;
    private readonly TextBlock netDown = Figure("0 KB/s"), netUp = Figure("0 KB/s");
    private readonly TextBlock netGlyph = Glyph("\uEB55"), btGlyph = Glyph("\uE702"), batteryGlyph = Glyph("\uE83F"), batteryText = Figure("");
    private readonly StackPanel batteryPart = new() { Orientation = Orientation.Horizontal, Margin = new(10, 0, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Grid bellPart = new() { Margin = new(10, 0, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Canvas cpuSpark = BarPopup.Graph(30, 14);
    private readonly NetworkMeter network = new();
    private readonly ProcessMeter processes = new();
    private readonly Queue<double> cpuHistory = new(), memoryHistory = new();
    private bool sampling;
    private BarPanel? openPanel;

    // ---- Open apps ----
    private Border BuildApps() { var part = MakeIsland(appsRow); part.Visibility = Visibility.Collapsed; return part; }
    private void ShowApps(IReadOnlyList<AppWindow> windows, nint foreground)
    {
        lastForeground = foreground;
        var groups = windows.GroupBy(w => w.GroupKey).ToDictionary(g => g.Key, g => g.ToList());
        // Icons keep the order the apps first appeared in, so focusing a window never shuffles the strip.
        appOrder.RemoveAll(key => !groups.ContainsKey(key));
        foreach (var key in groups.Keys) if (!appOrder.Contains(key)) appOrder.Add(key);
        var shown = appOrder.Take(10).ToList(); var changed = false;
        foreach (var key in appSlots.Keys.Where(k => !shown.Contains(k)).ToList()) { appsRow.Children.Remove(appSlots[key].Button); appSlots.Remove(key); changed = true; }
        for (int i = 0; i < shown.Count; i++)
        {
            var key = shown[i];
            if (!appSlots.TryGetValue(key, out var slot)) { slot = AppButton(key, groups[key][0]); appSlots[key] = slot; changed = true; Pop(slot.Button); }
            if (appsRow.Children.IndexOf(slot.Button) != i) { appsRow.Children.Remove(slot.Button); appsRow.Children.Insert(i, slot.Button); }
            slot.Windows = groups[key];
            var active = slot.Windows.Any(w => w.Handle == foreground);
            var indicator = active ? 14 : slot.Windows.Count > 1 ? 8 : 4;
            if (Math.Abs(slot.Indicator.Width - indicator) > .1) { if (Motion.Enabled(vm)) Motion.To(slot.Indicator, WidthProperty, indicator, 260); else slot.Indicator.Width = indicator; }
            slot.Indicator.SetResourceReference(Border.BackgroundProperty, active ? "Accent" : "Muted");
            slot.Indicator.Opacity = active ? 1 : .55;
            slot.Button.ToolTip = slot.Windows.Count == 1 ? slot.Windows[0].Title : $"{slot.Windows.Count} pencere · " + string.Join(" · ", slot.Windows.Take(3).Select(w => w.Title));
            System.Windows.Automation.AutomationProperties.SetName(slot.Button, (string)slot.Button.ToolTip);
        }
        var visible = shown.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (appsIsland.Visibility != visible) { appsIsland.Visibility = visible; changed = true; }
        if (changed) Fit();
    }
    private AppSlot AppButton(string key, AppWindow first)
    {
        var icon = new Image { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
        var letter = new TextBlock { Text = (first.Title.FirstOrDefault(char.IsLetterOrDigit) is var c && c != default ? c : '?').ToString().ToUpper(CultureInfo.GetCultureInfo("tr-TR")), FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var indicator = new Border { Width = 4, Height = 3, CornerRadius = new(1.5), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new(0, 0, 0, 2) };
        var face = new Grid { Width = 26 }; face.Children.Add(letter); face.Children.Add(icon); face.Children.Add(indicator);
        var button = IconButton(face, first.Title, null); button.Padding = new(4, 0, 4, 0); button.MinWidth = 30; button.Margin = new(1, 2, 1, 2);
        var slot = new AppSlot { Button = button, Indicator = indicator, Icon = icon };
        button.Click += (_, _) => ActivateApp(slot);
        button.MouseRightButtonUp += (_, e) => { e.Handled = true; AppMenu(slot); };
        if (first.Executable is { } path) _ = LoadAppIconAsync(path, icon, letter);
        return slot;
    }
    /// <summary>A new app icon grows in from its centre.</summary>
    private void Pop(FrameworkElement part)
    {
        if (!Motion.Enabled(vm) || !IsVisible) return;
        var scale = new ScaleTransform(.4, .4); part.RenderTransform = scale; part.RenderTransformOrigin = new(.5, .5); part.Opacity = 0;
        Motion.To(scale, ScaleTransform.ScaleXProperty, 1, 320); Motion.To(scale, ScaleTransform.ScaleYProperty, 1, 320); Motion.To(part, OpacityProperty, 1, 220);
    }
    private async Task LoadAppIconAsync(string path, Image icon, TextBlock letter)
    {
        try { if ((await icons.GetAsync(path)).Icon is { } source) { icon.Source = source; letter.Visibility = Visibility.Collapsed; } }
        catch { /* The first letter stays as the face. */ }
    }
    // Click: switch to the app; on the app already in front, minimise it (one window) or step to its next window.
    private void ActivateApp(AppSlot slot)
    {
        if (slot.Windows.Count == 0) return;
        var index = slot.Windows.FindIndex(w => w.Handle == lastForeground);
        if (index >= 0 && slot.Windows.Count == 1) { WindowOps.Minimize(slot.Windows[0].Handle); return; }
        var target = index >= 0 ? slot.Windows[(index + 1) % slot.Windows.Count] : slot.Windows.FirstOrDefault(w => !w.Minimized) ?? slot.Windows[0];
        if (!controller.ActivateWindow(target)) vm.Status = "Windows pencereye odak vermedi veya pencere kapandı.";
    }
    private void AppMenu(AppSlot slot)
    {
        var menu = new ContextMenu();
        foreach (var window in slot.Windows) { var item = new MenuItem { Header = window.Title }; var target = window; item.Click += (_, _) => controller.ActivateWindow(target); menu.Items.Add(item); }
        menu.Items.Add(new Separator());
        var close = new MenuItem { Header = slot.Windows.Count == 1 ? "Pencereyi kapat" : "Pencereleri kapat", ToolTip = "Uygulamaya normal kapatma isteği gönderir; kaydedilmemiş iş varsa uygulama sorar." };
        var windows = slot.Windows.ToList(); close.Click += (_, _) => { foreach (var window in windows) WindowOps.Close(window.Handle); };
        menu.Items.Add(close);
        menu.PlacementTarget = slot.Button; menu.Placement = PlacementMode.Bottom; menu.IsOpen = true;
    }

    // ---- Network speed ----
    private Button BuildNetwork()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var down = Glyph("\uE896"); down.FontSize = 12; down.Margin = new(0, 0, 5, 0); down.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
        var up = Glyph("\uE898"); up.FontSize = 12; up.Margin = new(12, 0, 5, 0); up.SetResourceReference(TextBlock.ForegroundProperty, "Secondary");
        netDown.MinWidth = netUp.MinWidth = 54;
        row.Children.Add(down); row.Children.Add(netDown); row.Children.Add(up); row.Children.Add(netUp);
        var button = IconButton(row, "Ağ: anlık indirme ve yükleme hızı · Tıkla: ayrıntılar", null); button.Padding = new(12, 0, 12, 0);
        button.Click += (_, _) => OpenNetwork(button);
        return button;
    }
    private async Task SampleAsync(bool connectivity)
    {
        if (sampling) return; sampling = true;
        try
        {
            await Task.Run(network.Sample);
            netDown.Text = NetworkMeter.Speed(network.Current.Down); netUp.Text = NetworkMeter.Speed(network.Current.Up);
            Push(cpuHistory, vm.CpuValue); Push(memoryHistory, vm.MemoryValue);
            BarPopup.Draw(cpuSpark, [cpuHistory.ToArray()], ["Accent"], NetworkMeter.HistoryLength, 100);
            if (connectivity) await ConnectivityService.ReadAsync();
        }
        catch (Exception ex) when (ex is System.Net.NetworkInformation.NetworkInformationException or InvalidOperationException) { }
        finally { sampling = false; }
    }
    private static void Push(Queue<double> history, double value) { history.Enqueue(value); while (history.Count > NetworkMeter.HistoryLength) history.Dequeue(); }

    // ---- Status icons ----
    private Button BuildStatus()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        netGlyph.Margin = new(0, 0, 10, 0); btGlyph.Margin = new(0, 0, 10, 0); btGlyph.Visibility = Visibility.Collapsed; volumeGlyph.Margin = new(0, 0, 6, 0);
        batteryGlyph.Margin = new(0, 0, 5, 0); batteryPart.Children.Add(batteryGlyph); batteryPart.Children.Add(batteryText);
        var bell = Glyph("\uEA8F"); bell.SetResourceReference(TextBlock.ForegroundProperty, "Accent"); bellPart.Children.Add(bell);
        row.Children.Add(netGlyph); row.Children.Add(btGlyph); row.Children.Add(volumeGlyph); row.Children.Add(volumeText); row.Children.Add(batteryPart); row.Children.Add(bellPart);
        var button = IconButton(row, "Hızlı ayarlar · Tekerlek: ses · Orta tık: sesi kapat/aç", controller.ShowQuickSettings); button.Padding = new(12, 0, 12, 0);
        button.PreviewMouseWheel += (_, e) => { ChangeVolume(e.Delta > 0 ? .02f : -.02f); e.Handled = true; };
        button.PreviewMouseDown += (_, e) => { if (e.ChangedButton == MouseButton.Middle) { ToggleMute(); e.Handled = true; } };
        button.PreviewKeyDown += (_, e) => { if (e.Key is Key.Up or Key.Right) { ChangeVolume(.02f); e.Handled = true; } else if (e.Key is Key.Down or Key.Left) { ChangeVolume(-.02f); e.Handled = true; } };
        return button;
    }
    private void ShowConnectivity()
    {
        var c = ConnectivityService.Last;
        netGlyph.Text = c.Kind switch
        {
            "wifi" or "cellular" => c.Bars switch { null or >= 4 => "\uE701", 3 => "\uE874", 2 => "\uE873", _ => "\uE872" },
            "ethernet" => "\uE839",
            _ => "\uEB55",
        };
        netGlyph.Opacity = c.Internet ? 1 : .5;
        btGlyph.Visibility = c.BluetoothOn == true ? Visibility.Visible : Visibility.Collapsed;
        btGlyph.SetResourceReference(TextBlock.ForegroundProperty, c.BluetoothDevices.Count > 0 ? "Accent" : "Text");
        if (c.Battery is { } percent)
        {
            var step = Math.Clamp(percent / 10, 0, 10);
            batteryGlyph.Text = c.Charging ? (step == 10 ? "\uE83E" : ((char)(0xE85A + step)).ToString()) : (step == 10 ? "\uE83F" : ((char)(0xE850 + step)).ToString());
            batteryText.Text = $"%{percent}"; batteryPart.Visibility = Visibility.Visible;
        }
        else batteryPart.Visibility = Visibility.Collapsed;
        volumeButton.ToolTip = StatusSummary(c) + "\nTıkla: hızlı ayarlar · Tekerlek: ses · Orta tık: sessiz";
        Fit();
    }
    private static string StatusSummary(Connectivity c)
    {
        var parts = new List<string>
        {
            c.Kind switch { "wifi" => $"Wi-Fi: {c.Name ?? "bağlı"}", "ethernet" => $"Ethernet: {c.Name ?? "bağlı"}", "cellular" => $"Mobil: {c.Name ?? "bağlı"}", _ => "Ağ bağlantısı yok" } + (c.Kind != "none" && !c.Internet ? " (internet yok)" : ""),
        };
        if (c.BluetoothOn == true) parts.Add(c.BluetoothDevices.Count > 0 ? "Bluetooth: " + string.Join(", ", c.BluetoothDevices) : "Bluetooth açık");
        if (c.Battery is { } battery) parts.Add($"Pil: %{battery}" + (c.Charging ? " (şarj oluyor)" : ""));
        return string.Join("\n", parts);
    }

    // ---- Drop-down panels ----
    /// <summary>Opens a panel under its island; the same island again closes it, another island swaps it.</summary>
    private bool Toggle(FrameworkElement anchor)
    {
        if (openPanel is not { } previous) return true;
        openPanel = null; previous.Close(); return previous.Anchor != anchor;
    }
    private void Open(BarPanel panel, DispatcherTimer? refresh)
    {
        openPanel = panel;
        panel.Closed += (_, _) => { refresh?.Stop(); if (openPanel == panel) openPanel = null; };
        panel.Show(); panel.Activate(); refresh?.Start();
    }
    private void OpenNetwork(FrameworkElement anchor)
    {
        if (!Toggle(anchor)) return;
        var body = new StackPanel(); var subtitle = new TextBlock();
        var c = ConnectivityService.Last;
        body.Children.Add(BarPopup.Header(c.Kind == "ethernet" ? "\uE839" : "\uE701", "Ağ", subtitle));
        var speeds = new UniformGrid { Columns = 2, Margin = new(0, 14, 0, 0) };
        TextBlock Speed(string label, string glyph, string brush)
        {
            var cell = new StackPanel(); var head = new StackPanel { Orientation = Orientation.Horizontal };
            var icon = BarPopup.Glyph(glyph, 11); icon.Margin = new(0, 0, 6, 0); icon.SetResourceReference(TextBlock.ForegroundProperty, brush);
            head.Children.Add(icon); head.Children.Add(BarPopup.Muted(label)); cell.Children.Add(head);
            var value = BarPopup.Figure("", 22, FontWeights.SemiBold); cell.Children.Add(value); speeds.Children.Add(cell); return value;
        }
        var down = Speed("İndirme", "\uE896", "Accent"); var up = Speed("Yükleme", "\uE898", "Secondary");
        body.Children.Add(speeds);
        var graph = BarPopup.Graph(316, 72); graph.Margin = new(0, 12, 0, 2); body.Children.Add(graph);
        body.Children.Add(BarPopup.Muted("Son 1 dakika · aktif bağlantı", 11));
        body.Children.Add(BarPopup.Heading("Bu oturumda"));
        body.Children.Add(BarPopup.Row("İndirilen", "", out var gotText)); body.Children.Add(BarPopup.Row("Yüklenen", "", out var sentText));
        body.Children.Add(BarPopup.Heading("Bağlantı"));
        body.Children.Add(BarPopup.Row("Tür", "", out var kindText)); body.Children.Add(BarPopup.Row("Ad", "", out var nameText));
        body.Children.Add(BarPopup.Row("Sinyal", "", out var signalText)); body.Children.Add(BarPopup.Row("İnternet", "", out var internetText));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 14, 0, 0) };
        actions.Children.Add(BarPopup.Action("\uE713", "Ağ ayarları", () => { openPanel?.Close(); controller.OpenSystem("network"); }));
        body.Children.Add(actions);
        void Render()
        {
            var now = ConnectivityService.Last; var history = network.History;
            down.Text = NetworkMeter.Speed(network.Current.Down); up.Text = NetworkMeter.Speed(network.Current.Up);
            BarPopup.Draw(graph, [history.Select(h => h.Down).ToArray(), history.Select(h => h.Up).ToArray()], ["Accent", "Secondary"], NetworkMeter.HistoryLength);
            gotText.Text = NetworkMeter.Size(network.SessionDown); sentText.Text = NetworkMeter.Size(network.SessionUp);
            kindText.Text = now.Kind switch { "wifi" => "Wi-Fi", "ethernet" => "Ethernet", "cellular" => "Mobil", _ => "Bağlı değil" };
            nameText.Text = now.Name ?? "—"; signalText.Text = now.Bars is { } bars ? $"{bars} / 5" : "—";
            internetText.Text = now.Kind == "none" ? "Yok" : now.Internet ? "Var" : "Yalnız yerel ağ";
            subtitle.Text = now.Kind == "none" ? "Bağlantı yok" : $"{kindText.Text} · {nameText.Text}";
        }
        Render();
        var refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; refresh.Tick += (_, _) => Render();
        Open(new BarPanel(anchor, 352, body, controller), refresh);
    }
    private void OpenSystem(FrameworkElement anchor)
    {
        if (!Toggle(anchor)) return;
        var body = new StackPanel(); var subtitle = new TextBlock();
        body.Children.Add(BarPopup.Header("\uE9D9", "Sistem", subtitle));
        var figures = new UniformGrid { Columns = 2, Margin = new(0, 14, 0, 0) };
        var cpuCell = new StackPanel { Margin = new(0, 0, 10, 0) }; cpuCell.Children.Add(BarPopup.Muted("İşlemci"));
        var cpuText = BarPopup.Figure("", 22, FontWeights.SemiBold); cpuCell.Children.Add(cpuText);
        var cpuGraph = BarPopup.Graph(148, 40); cpuGraph.Margin = new(0, 6, 0, 0); cpuCell.Children.Add(cpuGraph);
        var memoryCell = new StackPanel(); memoryCell.Children.Add(BarPopup.Muted("Bellek"));
        var memoryText = BarPopup.Figure("", 22, FontWeights.SemiBold); memoryCell.Children.Add(memoryText);
        var memoryGraph = BarPopup.Graph(148, 40); memoryGraph.Margin = new(0, 6, 0, 0); memoryCell.Children.Add(memoryGraph);
        figures.Children.Add(cpuCell); figures.Children.Add(memoryCell); body.Children.Add(figures);
        var memoryDetail = BarPopup.Muted("", 11); memoryDetail.Margin = new(0, 6, 0, 0); body.Children.Add(memoryDetail);
        TextBlock[] Rows(string heading, int count)
        {
            body.Children.Add(BarPopup.Heading(heading)); var values = new TextBlock[count * 2];
            for (int i = 0; i < count; i++) { var row = BarPopup.Row("", "", out var value); values[i * 2] = (TextBlock)row.Children[1]; values[i * 2 + 1] = value; row.Visibility = Visibility.Collapsed; body.Children.Add(row); }
            return values;
        }
        var cpuRows = Rows("En çok işlemci kullananlar", 5); var memoryRows = Rows("En çok bellek kullananlar", 5);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 14, 0, 0) };
        actions.Children.Add(BarPopup.Action("\uE9D9", "Hafifletme", () => { openPanel?.Close(); controller.ShowLighten(); }));
        actions.Children.Add(BarPopup.Action("\uE7EF", "Görev Yöneticisi", () =>
        {
            openPanel?.Close();
            try { Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { vm.Status = "Görev Yöneticisi açılamadı."; }
        }));
        body.Children.Add(actions);
        var first = true; var busy = false;
        static void Fill(TextBlock[] rows, IEnumerable<(string Name, string Value)> items)
        {
            var list = items.ToList();
            for (int i = 0; i < rows.Length / 2; i++)
            {
                var row = (FrameworkElement)rows[i * 2].Parent;
                if (i < list.Count) { rows[i * 2].Text = list[i].Name; rows[i * 2 + 1].Text = list[i].Value; row.Visibility = Visibility.Visible; } else row.Visibility = Visibility.Collapsed;
            }
        }
        static string Label(ProcessMeter.Usage usage) => usage.Count > 1 ? $"{usage.Name} ({usage.Count})" : usage.Name;
        async Task RenderAsync()
        {
            cpuText.Text = vm.CpuText; memoryText.Text = vm.MemoryText; memoryDetail.Text = vm.MemoryDetail;
            BarPopup.Draw(cpuGraph, [cpuHistory.ToArray()], ["Accent"], NetworkMeter.HistoryLength, 100);
            BarPopup.Draw(memoryGraph, [memoryHistory.ToArray()], ["Secondary"], NetworkMeter.HistoryLength, 100);
            var up = TimeSpan.FromMilliseconds(Environment.TickCount64); var days = (int)up.TotalDays;
            subtitle.Text = "Açık kalma süresi: " + (days > 0 ? $"{days} g " : "") + $"{up.Hours} sa {up.Minutes} dk";
            if (busy) return; busy = true;
            try
            {
                var usage = await Task.Run(processes.Sample);
                if (first) Fill(cpuRows, [("Ölçülüyor…", "")]);
                else Fill(cpuRows, usage.Where(u => u.Cpu >= .1).OrderByDescending(u => u.Cpu).Take(5).Select(u => (Label(u), $"%{u.Cpu.ToString("0.0", CultureInfo.GetCultureInfo("tr-TR"))}")));
                Fill(memoryRows, usage.OrderByDescending(u => u.Memory).Take(5).Select(u => (Label(u), NetworkMeter.Size(u.Memory))));
                first = false;
            }
            finally { busy = false; }
        }
        _ = RenderAsync();
        var refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) }; refresh.Tick += async (_, _) => await RenderAsync();
        Open(new BarPanel(anchor, 352, body, controller), refresh);
    }
}
