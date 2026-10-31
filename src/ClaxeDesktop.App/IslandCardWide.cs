using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

/// <summary>
/// The island card's expanded view (the button under page 1): the card grows into a control center with Wi-Fi, Bluetooth,
/// sound, microphone, do-not-disturb and dark-mode switches, the network graph, system load with the heaviest apps, the open
/// windows, Windows shortcuts and power. Every value is read from Windows; a switch Windows refuses says why.
/// </summary>
internal sealed partial class IslandCard
{
    private const double WideWidth = 760, WideHeight = 480, WideColumn = 226;
    private readonly Grid wide = new() { Visibility = Visibility.Collapsed };
    private readonly TextBlock expandGlyph = Glyph("", 12), expandText = new() { FontSize = 11, Margin = new(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Text = "Genişlet" };
    private readonly List<(ToggleButton Tile, TextBlock State, Func<bool?> Read)> wideTiles = [];
    private readonly TextBlock connectionLine = new() { FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new(3, 5, 3, 0) };
    private readonly TextBlock netDownText = BarPopup.Figure("", 15, FontWeights.SemiBold), netUpText = BarPopup.Figure("", 15, FontWeights.SemiBold), netTotals = BarPopup.Muted("", 11);
    private readonly Canvas netGraph = BarPopup.Graph(WideColumn, 50), loadGraph = BarPopup.Graph(WideColumn, 26);
    private readonly TextBlock uptimeText = BarPopup.Muted("", 11);
    private readonly Dictionary<string, TextBlock> loads = [];
    private readonly StackPanel processRows = new(), windowRows = new();
    private readonly ProcessMeter processes = new();
    private PerformanceSample? lastPerformance;
    private Button? expandButton;
    private bool expanded, sampling;
    private int wideTicks;
    private string windowsSignature = "";
    private string? powerArmed;
    private DateTime powerArmedUntil;

    private Button BuildExpandButton()
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal }; content.Children.Add(expandGlyph); content.Children.Add(expandText);
        expandText.Visibility = Visibility.Collapsed; expandGlyph.FontSize = 13;
        var button = new Button { Content = content, Padding = new(8, 3, 8, 3), Margin = new(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Genişlet: kontrol merkezi (bağlantılar, ağ, sistem, açık uygulamalar, Windows kısayolları)" };
        System.Windows.Automation.AutomationProperties.SetName(button, "Kartı genişlet");
        button.Click += (_, _) => { Touch(); SetExpanded(!expanded); };
        return expandButton = button;
    }
    private void BuildWide(Grid host)
    {
        host.Children.Add(wide);
        // Top right, beside the pages: the switches and what is connected.
        var quick = new StackPanel { Width = WideWidth - CardWidth - 16, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new(CardWidth - 4, 14, 0, 0) };
        var tiles = new UniformGrid { Columns = 3 }; quick.Children.Add(tiles);
        WideTile(tiles, "", "Wi-Fi", () => ConnectivityService.Last.WifiOn, async on => controller.ViewModel.Status = await ConnectivityService.SetRadioAsync(Windows.Devices.Radios.RadioKind.WiFi, on));
        WideTile(tiles, "", "Bluetooth", () => ConnectivityService.Last.BluetoothOn, async on => controller.ViewModel.Status = await ConnectivityService.SetRadioAsync(Windows.Devices.Radios.RadioKind.Bluetooth, on));
        WideTile(tiles, "", "Sessiz", () => { try { return audio.Read().Muted; } catch (Exception ex) when (ex is COMException or InvalidCastException) { return null; } }, on => { TryAudio(() => audio.SetMute(on)); return Task.CompletedTask; });
        WideTile(tiles, "", "Mikrofon", () => { try { return !audio.ReadMicrophone().Muted; } catch (Exception ex) when (ex is COMException or InvalidCastException) { return null; } }, on => { TryAudio(() => audio.SetMicrophoneMute(!on)); return Task.CompletedTask; });
        WideTile(tiles, "", "Rahatsız etme", () => controller.Notifications.DoNotDisturb, on => { controller.SetDoNotDisturb(on); return Task.CompletedTask; });
        WideTile(tiles, "", "Koyu Windows", () => controller.WindowsDark, on => { controller.ViewModel.Status = controller.SetWindowsDark(on); return Task.CompletedTask; });
        connectionLine.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); quick.Children.Add(connectionLine);
        var status = BarPopup.Muted("", 11); status.Margin = new(3, 3, 3, 0); status.FontSize = 10; status.DataContext = controller.ViewModel; status.SetBinding(TextBlock.TextProperty, new Binding(nameof(DesktopViewModel.Status)));
        status.TextTrimming = TextTrimming.CharacterEllipsis; status.TextWrapping = TextWrapping.NoWrap; quick.Children.Add(status);
        wide.Children.Add(quick);

        var rule = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Top, Margin = new(16, CardHeight - 6, 16, 0), Opacity = .7 }; rule.SetResourceReference(Border.BackgroundProperty, "Line"); wide.Children.Add(rule);

        // Bottom: network, system and open windows side by side.
        var columns = new Grid { Margin = new(16, CardHeight + 4, 16, 0), VerticalAlignment = VerticalAlignment.Top, Height = WideHeight - CardHeight - 70 };
        for (int i = 0; i < 3; i++) { columns.ColumnDefinitions.Add(new()); if (i < 2) columns.ColumnDefinitions.Add(new() { Width = new(14) }); }
        var network = new StackPanel(); network.Children.Add(Section("", "Ağ"));
        var speeds = new UniformGrid { Columns = 2, Margin = new(0, 2, 0, 0) };
        speeds.Children.Add(Speed("", "Accent", netDownText)); speeds.Children.Add(Speed("", "Secondary", netUpText)); network.Children.Add(speeds);
        netGraph.Margin = new(0, 5, 0, 3); network.Children.Add(netGraph); network.Children.Add(netTotals);
        var netActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 8, 0, 0) };
        netActions.Children.Add(Small(BarPopup.Action("", "Ağ ayarları", () => { Leave(); controller.OpenSystem("network"); })));
        netActions.Children.Add(Small(BarPopup.Action("", "Bluetooth", () => { Leave(); controller.OpenSystem("bluetooth"); })));
        network.Children.Add(netActions);
        columns.Children.Add(network);

        var system = new StackPanel(); system.Children.Add(Section("", "Sistem", uptimeText));
        var loadGrid = new UniformGrid { Columns = 2 };
        foreach (var (key, label) in new[] { ("cpu", "CPU"), ("gpu", "GPU"), ("ram", "RAM"), ("disk", "Disk") })
        {
            var row = BarPopup.Row(label, "…", out var value); row.Margin = new(0, 1, 12, 1); loads[key] = value; loadGrid.Children.Add(row);
        }
        system.Children.Add(loadGrid);
        loadGraph.Margin = new(0, 4, 0, 0); system.Children.Add(loadGraph);
        var heavy = BarPopup.Heading("En çok kaynak kullananlar"); heavy.Margin = new(0, 6, 0, 2); system.Children.Add(heavy);
        system.Children.Add(processRows);
        Grid.SetColumn(system, 2); columns.Children.Add(system);

        var open = new StackPanel(); open.Children.Add(Section("", "Açık uygulamalar")); open.Children.Add(windowRows);
        Grid.SetColumn(open, 4); columns.Children.Add(open);
        wide.Children.Add(columns);

        // Windows shortcuts and power along the bottom edge.
        var dock = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new(16, 0, 16, 12) };
        dock.Children.Add(Shortcut("", "Ayarlar", () => OpenUri("ms-settings:")));
        dock.Children.Add(Shortcut("", "Dosyalar", () => OpenUri("explorer.exe")));
        dock.Children.Add(Shortcut("", "Görev Yön.", () => OpenUri("taskmgr.exe")));
        dock.Children.Add(Shortcut("", "Alıntı", TakeScreenshot));
        dock.Children.Add(Shortcut("", "Ses", () => { Leave(); controller.OpenSystem("sound"); }));
        dock.Children.Add(Shortcut("", "Ekran", () => { Leave(); controller.OpenSystem("display"); }));
        dock.Children.Add(Shortcut("", "Bildirimler", () => { Leave(); controller.ShowQuickSettings(); }));
        var gap = new Border { Width = 1, Margin = new(6, 10, 12, 10) }; gap.SetResourceReference(Border.BackgroundProperty, "Line"); dock.Children.Add(gap);
        dock.Children.Add(Shortcut("", "Kilitle", () => Power("lock", null)));
        dock.Children.Add(Shortcut("", "Uyku", () => Power("sleep", null)));
        Button? restart = null, shutdown = null;
        restart = Shortcut("", "Y. başlat", () => Power("restart", restart)); dock.Children.Add(restart);
        shutdown = Shortcut("", "Kapat", () => Power("shutdown", shutdown)); dock.Children.Add(shutdown);
        wide.Children.Add(dock);
    }

    // ---- Growing and shrinking ----
    private void SetExpanded(bool on, bool animate = true)
    {
        if (expanded == on && animate) return;
        expanded = on; wideTicks = 0; windowsSignature = "";
        expandGlyph.Text = on ? "" : ""; expandText.Text = on ? "Küçült" : "Genişlet"; if (expandButton is not null) expandButton.ToolTip = on ? "Küçült" : "Genişlet: kontrol merkezi";
        idle.Interval = TimeSpan.FromSeconds(on ? 45 : 12); Touch();
        double width = on ? WideWidth : CardWidth, height = on ? WideHeight : CardHeight;
        if (on) { ResizeWindow(width, height); _ = RenderWideAsync(); _ = ConnectivityService.ReadAsync(); }
        if (!animate || !IsVisible || !Motion.Enabled(controller.ViewModel))
        {
            card.BeginAnimation(WidthProperty, null); card.BeginAnimation(HeightProperty, null); wide.BeginAnimation(OpacityProperty, null);
            card.Width = width; card.Height = height; wide.Opacity = 1; wide.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            if (!on) ResizeWindow(width, height);
            ApplyBackground(); return;
        }
        if (on)
        {
            wide.Visibility = Visibility.Visible; wide.Opacity = 0;
            Motion.To(card, WidthProperty, width, 380); Motion.To(card, HeightProperty, height, 440);
            Motion.To(wide, OpacityProperty, 1, 300, delay: 160);
        }
        else
        {
            Motion.To(wide, OpacityProperty, 0, 120);
            Motion.To(card, WidthProperty, width, 320);
            Motion.To(card, HeightProperty, height, 340, () => { if (!expanded) { wide.Visibility = Visibility.Collapsed; ResizeWindow(width, height); } });
        }
        ApplyBackground();
    }
    /// <summary>Resizes the window around its horizontal centre, keeping the top edge and the card inside the monitor.</summary>
    private void ResizeWindow(double cardWidth, double cardHeight)
    {
        var before = hwnd == 0 ? null : WindowOps.Read(hwnd)?.Bounds;
        Width = cardWidth + Shadow * 2; Height = cardHeight + Shadow * 2;
        if (before is not { } b || !IsVisible) return;
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX; var monitor = MonitorAt((int)(b.X + b.Width / 2), (int)(b.Y + 1));
        var x = b.X + b.Width / 2 - Width * scale / 2;
        x = Math.Clamp(x, monitor.X, Math.Max(monitor.X, monitor.X + monitor.Width - Width * scale));
        WindowPlacement.Move(hwnd, (int)x, (int)b.Y);
    }

    // ---- Live values while expanded ----
    private async Task RenderWideAsync()
    {
        if (!expanded || !IsVisible) return;
        foreach (var (tile, state, read) in wideTiles)
        {
            bool? on; try { on = read(); } catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException) { on = null; }
            tile.IsEnabled = on is not null; tile.IsChecked = on == true; state.Text = on is null ? "Yok" : on == true ? "Açık" : "Kapalı";
        }
        RenderConnection();
        var meter = NetworkMeter.Shared; var history = meter.History;
        netDownText.Text = NetworkMeter.Speed(meter.Current.Down); netUpText.Text = NetworkMeter.Speed(meter.Current.Up);
        BarPopup.Draw(netGraph, [history.Select(h => h.Down).ToArray(), history.Select(h => h.Up).ToArray()], ["Accent", "Secondary"], NetworkMeter.HistoryLength);
        netTotals.Text = $"Son 1 dakika · bu oturumda ↓ {NetworkMeter.Size(meter.SessionDown)}  ↑ {NetworkMeter.Size(meter.SessionUp)}";
        var vm = controller.ViewModel;
        loads["cpu"].Text = vm.CpuText; loads["ram"].Text = vm.MemoryText;
        loads["gpu"].Text = lastPerformance?.Gpu is { } gpu ? $"%{gpu:0}" : "Yok"; loads["disk"].Text = lastPerformance?.Disk is { } disk ? $"%{Math.Min(100, disk):0}" : "Yok";
        BarPopup.Draw(loadGraph, [SystemHistory.Cpu, SystemHistory.Memory], ["Accent", "Secondary"], NetworkMeter.HistoryLength, 100);
        var up = TimeSpan.FromMilliseconds(Environment.TickCount64); var days = (int)up.TotalDays;
        uptimeText.Text = "açık: " + (days > 0 ? $"{days} g " : "") + $"{up.Hours} sa {up.Minutes} dk";
        RenderWindows();
        if (wideTicks++ % 5 == 4) _ = ConnectivityService.ReadAsync();
        if (wideTicks % 2 == 1 && !sampling)
        {
            sampling = true;
            try { RenderProcesses(await Task.Run(processes.Sample), wideTicks <= 1); }
            finally { sampling = false; }
        }
    }
    private void RenderConnection()
    {
        var c = ConnectivityService.Last;
        var text = c.Kind switch { "wifi" => "Wi-Fi", "ethernet" => "Ethernet", "cellular" => "Mobil", _ => "Ağ bağlantısı yok" };
        if (c.Kind != "none") text += (c.Name is { } name ? " · " + name : "") + (c.Bars is { } bars && c.Kind != "ethernet" ? $" · sinyal {bars}/5" : "") + (c.Internet ? " · internet var" : " · internet yok");
        if (c.BluetoothOn == true) text += c.BluetoothDevices.Count > 0 ? "\nBluetooth: " + string.Join(", ", c.BluetoothDevices) : "\nBluetooth açık, bağlı cihaz yok";
        if (c.Battery is { } battery) text += $"\nPil %{battery}" + (c.Charging ? " · şarj oluyor" : "");
        connectionLine.Text = text;
    }
    private void RenderProcesses(IReadOnlyList<ProcessMeter.Usage> usage, bool first)
    {
        processRows.Children.Clear();
        var top = (first ? usage.OrderByDescending(u => u.Memory) : usage.OrderByDescending(u => u.Cpu).ThenByDescending(u => u.Memory)).Take(3);
        foreach (var item in top)
        {
            var name = item.Count > 1 ? $"{item.Name} ({item.Count})" : item.Name;
            var cpu = first ? "…" : "%" + item.Cpu.ToString("0.0", CultureInfo.GetCultureInfo("tr-TR"));
            var row = BarPopup.Row(name, $"{cpu} · {NetworkMeter.Size(item.Memory)}", out _); row.Margin = new(0, 1, 0, 1); processRows.Children.Add(row);
        }
    }
    private void RenderWindows()
    {
        var windows = controller.LastSnapshot.Take(5).ToList();
        var signature = string.Join("|", windows.Select(w => $"{w.Handle}:{w.Title}"));
        if (signature == windowsSignature) return; windowsSignature = signature;
        windowRows.Children.Clear();
        if (windows.Count == 0) { windowRows.Children.Add(BarPopup.Muted("Açık pencere yok", 11)); return; }
        foreach (var window in windows)
        {
            var row = new DockPanel { Margin = new(0, 1, 0, 1) };
            var close = new Button { Content = Glyph("", 10), Padding = new(6, 4, 6, 4), Background = Brushes.Transparent, BorderThickness = new(0), ToolTip = "Kapat (uygulama kaydetmek isterse sorar)" };
            System.Windows.Automation.AutomationProperties.SetName(close, window.Title + " kapat");
            var target = window; close.Click += (_, _) => { Touch(); WindowOps.Close(target.Handle); windowsSignature = ""; };
            DockPanel.SetDock(close, Dock.Right); row.Children.Add(close);
            // A fixed width keeps short titles at the left edge; the button template centres its content.
            var face = new StackPanel { Orientation = Orientation.Horizontal, Width = WideColumn - 40 };
            var image = new Image { Width = 16, Height = 16, Margin = new(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center }; face.Children.Add(image);
            face.Children.Add(new TextBlock { Text = window.Title, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = WideColumn - 70, VerticalAlignment = VerticalAlignment.Center });
            var open = new Button { Content = face, Padding = new(4, 3, 4, 3), Background = Brushes.Transparent, BorderThickness = new(0), HorizontalContentAlignment = HorizontalAlignment.Left, ToolTip = window.Title };
            System.Windows.Automation.AutomationProperties.SetName(open, window.Title + " öne getir");
            open.Click += (_, _) => { if (controller.ActivateWindow(target)) Leave(); else controller.ViewModel.Status = "Windows pencereye odak vermedi."; };
            row.Children.Add(open); windowRows.Children.Add(row);
            if (window.Executable is { } path) _ = LoadIconAsync(path, image);
        }
    }

    // ---- Actions ----
    private void OpenUri(string target)
    {
        Leave();
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { controller.ViewModel.Status = "Açılamadı: " + target; controller.Log.Write("island-open-failed", ex); }
    }
    // Lock and sleep act at once (both are undone by signing in / waking); restart and shut down need a second press within 4 s.
    private void Power(string action, Button? button)
    {
        Touch();
        if (action is "restart" or "shutdown" && (powerArmed != action || DateTime.UtcNow > powerArmedUntil))
        {
            powerArmed = action; powerArmedUntil = DateTime.UtcNow.AddSeconds(4);
            if (button?.Content is StackPanel { Children: [_, TextBlock label] }) { var original = label.Text; label.Text = "Emin misin?"; _ = ResetLabelAsync(label, original); }
            return;
        }
        powerArmed = null;
        try
        {
            switch (action)
            {
                case "lock": Leave(); if (!LockWorkStation()) controller.ViewModel.Status = "Ekran kilitlenemedi."; break;
                case "sleep": Leave(); System.Windows.Forms.Application.SetSuspendState(System.Windows.Forms.PowerState.Suspend, false, false); break;
                case "restart": controller.Log.Write("island-restart"); Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0") { UseShellExecute = false, CreateNoWindow = true }); break;
                case "shutdown": controller.Log.Write("island-shutdown"); Process.Start(new ProcessStartInfo("shutdown.exe", "/s /t 0") { UseShellExecute = false, CreateNoWindow = true }); break;
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { controller.ViewModel.Status = "Windows bu işlemi yapmadı."; controller.Log.Write("island-power-failed", ex); }
    }
    private static async Task ResetLabelAsync(TextBlock label, string original) { await Task.Delay(4100); label.Text = original; }

    // ---- Building blocks ----
    private void WideTile(Panel parent, string glyph, string label, Func<bool?> read, Func<bool, Task> change)
    {
        var tile = new ToggleButton { Style = (Style)FindResource("QuickTile"), Margin = new(2), Padding = new(9, 6, 9, 6) };
        var content = new StackPanel(); content.Children.Add(Glyph(glyph, 13));
        content.Children.Add(new TextBlock { Text = label, FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new(0, 3, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
        var state = new TextBlock { FontSize = 10, Opacity = .75 }; content.Children.Add(state);
        foreach (var text in content.Children.OfType<TextBlock>()) text.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(Foreground)) { Source = tile });
        tile.Content = content; System.Windows.Automation.AutomationProperties.SetName(tile, label);
        tile.Click += async (_, _) =>
        {
            Touch(); var wanted = tile.IsChecked == true; tile.IsEnabled = false;
            try { await change(wanted); }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException) { controller.ViewModel.Status = label + " değiştirilemedi."; }
            finally { tile.IsEnabled = true; await RenderWideAsync(); }
        };
        wideTiles.Add((tile, state, read)); parent.Children.Add(tile);
    }
    private static FrameworkElement Section(string glyph, string title, TextBlock? right = null)
    {
        var row = new DockPanel { Margin = new(0, 0, 0, 5) };
        if (right is not null) { right.VerticalAlignment = VerticalAlignment.Center; DockPanel.SetDock(right, Dock.Right); row.Children.Add(right); }
        var icon = Glyph(glyph, 13); icon.Margin = new(0, 0, 8, 0); icon.SetResourceReference(TextBlock.ForegroundProperty, "Accent"); row.Children.Add(icon);
        row.Children.Add(new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }
    private static FrameworkElement Speed(string glyph, string brush, TextBlock value)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = Glyph(glyph, 12); icon.Margin = new(0, 0, 6, 0); icon.SetResourceReference(TextBlock.ForegroundProperty, brush);
        row.Children.Add(icon); row.Children.Add(value); return row;
    }
    private static Button Small(Button button) { button.Padding = new(8, 4, 8, 4); button.FontSize = 10; return button; }
    private Button Shortcut(string glyph, string label, Action action)
    {
        var content = new StackPanel(); var icon = Glyph(glyph, 14); icon.HorizontalAlignment = HorizontalAlignment.Center; content.Children.Add(icon);
        content.Children.Add(new TextBlock { Text = label, FontSize = 9, Margin = new(0, 3, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 54 });
        var button = new Button { Content = content, Width = 58, Height = 46, Padding = new(2), Margin = new(0, 0, 4, 0), ToolTip = label };
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        button.Click += (_, _) => { Touch(); action(); };
        return button;
    }
    [DllImport("user32.dll")] private static extern bool LockWorkStation();
}
