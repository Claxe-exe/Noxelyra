using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;

/// <summary>Keyboard takeovers (Win key, Alt+Tab, tiling) and a capture-based editor for every Noxelyra shortcut.</summary>
internal sealed class KeyboardWindow : Window
{
    private readonly DesktopController controller;
    private readonly StackPanel list = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 6, 0, 0) };
    private string? capturing;
    private Button? captureButton;
    private readonly bool editable;
    public KeyboardWindow(DesktopController controller)
    {
        this.controller = controller; editable = !controller.IsSafeMode && !controller.ReadOnly;
        Title = "Noxelyra · Klavye ve kısayollar"; Width = 680; Height = 860; MinWidth = 560; MinHeight = 520; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty, "Canvas"); SetResourceReference(ForegroundProperty, "Text");
        Glass.PrepareFramed(this, controller.ViewModel.Settings); Motion.Attach(this, controller.ViewModel, () => controller.IsExiting);
        var root = new StackPanel { Margin = new(28) }; Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(new TextBlock { Text = "Klavye", FontSize = 24, FontWeight = FontWeights.SemiBold });
        status.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        status.Text = $"Acil çıkış her zaman çalışır: {controller.GestureText("emergency")}. Noxelyra'yı hemen kapatır ve Windows'u eski haline getirir.";
        root.Children.Add(status);

        var linux = Section(root, "Linux hissi");
        var options = controller.Keyboard;
        Toggle(linux, "Win tuşu Noxelyra başlatıcısını açsın", "Win'e tek basış Başlat yerine başlatıcıyı açar; Win+E, Win+L gibi diğer Windows kısayolları çalışmaya devam eder.", options.SuperLauncher, on => o => o with { SuperLauncher = on });
        Toggle(linux, "Alt+Tab Noxelyra değiştiricisi olsun", "Canlı önizlemeli pencere değiştirici. Alt'ı bırakınca geçer, Esc vazgeçer.", options.AltTab, on => o => o with { AltTab = on });
        Toggle(linux, "Otomatik döşeme (Hyprland dwindle)", "Yeni pencereler kendiliğinden yerleşir; diyaloglar, oyunlar ve büyütülmüş pencereler serbest kalır. Win+T pencereyi döşemeden çıkarır.", options.Tiling, on => o => o with { Tiling = on });
        var gapLabel = new TextBlock { Margin = new(0, 10, 0, 0) }; var gap = new Slider { Minimum = 0, Maximum = 40, Value = options.TilingGap, TickFrequency = 2, IsSnapToTickEnabled = true, IsEnabled = editable };
        void GapText() => gapLabel.Text = $"Pencere boşluğu: {gap.Value:0} px";
        GapText(); linux.Children.Add(gapLabel); linux.Children.Add(gap);
        gap.ValueChanged += (_, _) => GapText();
        gap.PreviewMouseLeftButtonUp += async (_, _) => Say(await controller.SetKeyboardAsync(o => o with { TilingGap = (int)gap.Value }));
        System.Windows.Automation.AutomationProperties.SetName(gap, "Döşemede pencere boşluğu");
        linux.Children.Add(new TextBlock { Text = "Başlatıcı stili", Margin = new(0, 12, 0, 4) });
        var style = new ComboBox { ItemsSource = new[] { "Ortada arama kutusu", "Tam ekran ızgara" }, SelectedIndex = options.LauncherStyle == "grid" ? 1 : 0, IsEnabled = editable };
        style.SelectionChanged += async (_, _) => Say(await controller.SetKeyboardAsync(o => o with { LauncherStyle = style.SelectedIndex == 1 ? "grid" : "center" }));
        linux.Children.Add(style);

        var keys = Section(root, "Kısayollar");
        keys.Children.Add(Muted("Değiştir'e bas, sonra yeni tuş birleşimine bas (Esc vazgeçer). Döşeme kısayolları Win ile başlar ve döşeme açıkken çalışır."));
        keys.Children.Add(list);
        var reset = new Button { Content = "Hepsini varsayılana döndür", Padding = new(12, 7, 12, 7), HorizontalAlignment = HorizontalAlignment.Left, Margin = new(0, 12, 0, 0), IsEnabled = editable };
        reset.Click += async (_, _) => { Say(await controller.SetKeyboardAsync(o => o with { Bindings = [] })); Render(); };
        keys.Children.Add(reset);
        Render();
        PreviewKeyDown += Capture;
    }
    private void Render()
    {
        list.Children.Clear();
        foreach (var action in KeyboardOptions.Actions)
        {
            var row = new DockPanel { Margin = new(0, 4, 0, 4) };
            var change = new Button { Content = "Değiştir", Padding = new(10, 5, 10, 5), Margin = new(8, 0, 0, 0), IsEnabled = editable };
            var key = new Border { CornerRadius = new(8), Padding = new(10, 4, 10, 4), MinWidth = 150, Child = new TextBlock { Text = controller.GestureText(action), HorizontalAlignment = HorizontalAlignment.Center } };
            key.SetResourceReference(Border.BackgroundProperty, "Card");
            DockPanel.SetDock(change, Dock.Right); DockPanel.SetDock(key, Dock.Right);
            row.Children.Add(change); row.Children.Add(key);
            row.Children.Add(new TextBlock { Text = KeyboardOptions.Label(action) + (controller.Keyboard.IsCustom(action) ? "  ·  özel" : ""), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            var target = action;
            change.Click += (_, _) => { capturing = target; captureButton = change; change.Content = "Tuşlara bas…"; Say("Yeni kısayol için tuşlara bas. Esc vazgeçer."); };
            System.Windows.Automation.AutomationProperties.SetName(change, KeyboardOptions.Label(action) + " kısayolunu değiştir");
            list.Children.Add(row);
        }
    }
    private async void Capture(object sender, KeyEventArgs e)
    {
        if (capturing is null) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape) { capturing = null; Render(); Say("Değişiklik yapılmadı."); return; }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        var modifiers = Keyboard.Modifiers; var win = Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin);
        var gesture = new HotkeyGesture(modifiers.HasFlag(ModifierKeys.Control), modifiers.HasFlag(ModifierKeys.Alt), modifiers.HasFlag(ModifierKeys.Shift), win, KeyInterop.VirtualKeyFromKey(key));
        var action = capturing; capturing = null;
        try { gesture.Validate(); } catch (InvalidDataException ex) { Say(ex.Message); Render(); return; }
        Say(await controller.SetKeyboardAsync(o => o.With(action, gesture)));
        Render();
    }
    private void Toggle(Panel parent, string label, string help, bool value, Func<bool, Func<KeyboardOptions, KeyboardOptions>> change)
    {
        var box = new CheckBox { Content = label, IsChecked = value, IsEnabled = editable };
        box.Click += async (_, _) => { Say(await controller.SetKeyboardAsync(change(box.IsChecked == true))); box.IsChecked = label.StartsWith("Win") ? controller.Keyboard.SuperLauncher : label.StartsWith("Alt") ? controller.Keyboard.AltTab : controller.Keyboard.Tiling; };
        parent.Children.Add(box);
        var note = Muted(help); note.Margin = new(26, -6, 0, 8); note.FontSize = 12; parent.Children.Add(note);
    }
    private void Say(string message) => status.Text = message;
    private static TextBlock Muted(string text) { var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 8) }; block.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); return block; }
    private static StackPanel Section(Panel parent, string title)
    {
        parent.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new(0, 24, 0, 8) });
        var body = new StackPanel(); parent.Children.Add(body); return body;
    }
}
