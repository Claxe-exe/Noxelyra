using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;

/// <summary>
/// First-run guide: theme, the Linux-style takeovers (each one explained and switchable; all but auto tiling pre-ticked), start with Windows and the
/// shortcuts worth knowing. Nothing changes until "Bitir"; "Atla" leaves every takeover off.
/// </summary>
internal sealed class FirstRunWindow : Window
{
    private readonly DesktopController controller;
    private readonly ContentControl page = new();
    private readonly StackPanel dots = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button back = new() { Content = "Geri", Padding = new(16, 8, 16, 8) }, next = new() { Content = "İleri", Padding = new(20, 8, 20, 8) }, skip = new() { Content = "Atla", Padding = new(14, 8, 14, 8) };
    private readonly CheckBox super = Option("Win tuşu Noxelyra başlatıcısını açsın", true), altTab = Option("Alt+Tab Noxelyra'nın canlı önizlemeli değiştiricisi olsun", true),
        tiling = Option("Yeni pencereler otomatik döşensin (Hyprland gibi)", false), taskbar = Option("Windows görev çubuğu Noxelyra açıkken gizlensin", true),
        startup = Option("Noxelyra Windows ile birlikte başlasın", true);
    private readonly List<Func<FrameworkElement>> pages;
    private int index;
    private bool finishing;
    public FirstRunWindow(DesktopController controller)
    {
        this.controller = controller;
        Title = "Noxelyra'ya hoş geldin"; Width = 720; Height = 560; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty, "Canvas"); SetResourceReference(ForegroundProperty, "Text");
        Glass.PrepareFramed(this, controller.ViewModel.Settings); Motion.Attach(this, controller.ViewModel, () => controller.IsExiting);
        try { startup.IsChecked = true; } catch (InvalidOperationException) { }
        pages = [Welcome, Look, Takeovers, Shortcuts, Done];
        var root = new DockPanel { Margin = new(36, 30, 36, 24) }; Content = root;
        var bar = new DockPanel { Margin = new(0, 18, 0, 0) }; DockPanel.SetDock(bar, Dock.Bottom); root.Children.Add(bar);
        DockPanel.SetDock(skip, Dock.Left); bar.Children.Add(skip);
        var right = new StackPanel { Orientation = Orientation.Horizontal }; right.Children.Add(back); right.Children.Add(next); DockPanel.SetDock(right, Dock.Right); bar.Children.Add(right);
        bar.Children.Add(dots);
        root.Children.Add(page);
        back.Click += (_, _) => Go(index - 1);
        next.Click += async (_, _) => { if (index == pages.Count - 1) await FinishAsync(true); else Go(index + 1); };
        skip.Click += async (_, _) => await FinishAsync(false);
        Closing += (_, _) => { if (!finishing) _ = controller.CompleteFirstRunAsync(); };
        Go(0);
    }
    private void Go(int target)
    {
        index = Math.Clamp(target, 0, pages.Count - 1); page.Content = pages[index]();
        back.Visibility = index == 0 ? Visibility.Hidden : Visibility.Visible; next.Content = index == pages.Count - 1 ? "Bitir" : "İleri";
        skip.Visibility = index == pages.Count - 1 ? Visibility.Hidden : Visibility.Visible;
        dots.Children.Clear();
        for (int i = 0; i < pages.Count; i++) { var dot = new Ellipse { Width = 7, Height = 7, Margin = new(4) }; dot.SetResourceReference(Shape.FillProperty, i == index ? "Accent" : "Line"); dots.Children.Add(dot); }
    }
    private async Task FinishAsync(bool apply)
    {
        if (finishing) return; finishing = true; next.IsEnabled = skip.IsEnabled = back.IsEnabled = false;
        if (apply)
        {
            controller.ViewModel.Status = await controller.SetKeyboardAsync(o => o with { SuperLauncher = super.IsChecked == true, AltTab = altTab.IsChecked == true, Tiling = tiling.IsChecked == true });
            if (taskbar.IsChecked == true) await controller.ConfigureWindowsTaskbarAsync(true);
            try { controller.Startup.SetEnabled(startup.IsChecked == true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException) { controller.Log.Write("first-run-startup-failed", ex); }
        }
        await controller.CompleteFirstRunAsync();
        Close();
    }
    private FrameworkElement Welcome() => Page("Noxelyra'ya hoş geldin",
        "Windows'un üstüne yumuşak, Linux tadında bir masaüstü: üstte bar ve dinamik ada, altta dock, kendi masaüstün ve widget'lar. Explorer'a dokunmaz; Noxelyra kapanınca Windows olduğu gibi geri gelir.",
        Note("Bir şey ters giderse " + controller.GestureText("emergency") + " Noxelyra'yı hemen kapatır ve Windows'u eski haline getirir."));
    private FrameworkElement Look()
    {
        var themes = new WrapPanel { Margin = new(0, 6, 0, 0) };
        foreach (var palette in ThemePresets.All)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new Ellipse { Width = 14, Height = 14, Margin = new(0, 0, 8, 0), Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(palette.Accent)) });
            content.Children.Add(new TextBlock { Text = palette.Name });
            var button = new Button { Content = content, Padding = new(12, 8, 12, 8), Margin = new(0, 0, 8, 8) };
            var id = palette.Id; button.Click += async (_, _) => await controller.SetThemeAsync(id);
            themes.Children.Add(button);
        }
        return Page("Renklerini seç", "Tıklayınca hemen uygulanır. Sonra Ayarlar'dan değiştirebilir, duvar kâğıdından renk de alabilirsin.", themes);
    }
    private FrameworkElement Takeovers()
    {
        var list = new StackPanel();
        foreach (var (box, help) in new[]
        {
            (super, "Win'e tek basınca Başlat yerine Noxelyra başlatıcısı açılır. Win+E, Win+L gibi kısayollar Windows'ta kalır."),
            (altTab, "Alt+Tab canlı pencere önizlemeleri gösterir; Alt'ı bırakınca geçer, Esc vazgeçer."),
            (tiling, "Yeni pencereler ekranı paylaşarak yerleşir. Bir pencereyi elle taşırsan ya da Win+oklarla yerleştirirsen döşemeden çıkar."),
            (taskbar, "Görev çubuğu otomatik gizlenir; bardaki ˄ düğmesi tepsi simgelerini açar."),
        })
        {
            list.Children.Add(box);
            var line = new TextBlock { Text = help, TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new(26, -8, 0, 10) }; line.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
            list.Children.Add(line);
        }
        list.Children.Add(startup);
        return Page("Windows'u Noxelyra'ya bırak", "Otomatik döşeme dışında hepsi önerilen ayar olarak işaretli; istemediğini kaldır. Bitir'e basana kadar hiçbir şey değişmez, sonra Ayarlar → Klavye ve kısayollar'dan da değiştirebilirsin.", list);
    }
    private FrameworkElement Shortcuts()
    {
        var list = new StackPanel();
        void Row(string keys, string what)
        {
            var row = new DockPanel { Margin = new(0, 3, 0, 3) };
            var chip = new Border { CornerRadius = new(8), Padding = new(10, 4, 10, 4), MinWidth = 140, Margin = new(0, 0, 14, 0), Child = new TextBlock { Text = keys, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center } };
            chip.SetResourceReference(Border.BackgroundProperty, "Card"); DockPanel.SetDock(chip, Dock.Left); row.Children.Add(chip);
            row.Children.Add(new TextBlock { Text = what, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap }); list.Children.Add(row);
        }
        Row(controller.GestureText("island"), "Dinamik ada kartı: müzik, ±10 sn, hava, ses, parlaklık (oyunda da)");
        Row(controller.GestureText("dock"), "Dock'u öne getir");
        Row("Win", "Başlatıcı (açtıysan)");
        Row("Alt+Tab", "Pencere değiştirici (açtıysan)");
        Row(controller.GestureText("emergency"), "Acil çıkış");
        return Page("Bilmen gereken birkaç tuş", "Hepsi Ayarlar → Kısayollar ve ipuçları sayfasında da var.", list);
    }
    private FrameworkElement Done() => Page("Hazırsın",
        "Bitir'e basınca seçtiklerin uygulanır. Her şeyi tek yerden bulmak için bardaki menüden ya da başlatıcıdan \"Ayarlar\"ı aç; üstteki arama kutusu doğru pencereyi gösterir.",
        Note("Bu rehberi Ayarlar → Kurulum rehberi'nden yeniden açabilirsin."));
    private static FrameworkElement Page(string title, string text, UIElement body)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = title, FontSize = 26, FontWeight = FontWeights.SemiBold, Margin = new(0, 0, 0, 10) });
        var line = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 14, Margin = new(0, 0, 0, 18) }; line.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        panel.Children.Add(line); panel.Children.Add(body);
        return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
    private static Border Note(string text)
    {
        var note = new Border { CornerRadius = new(12), Padding = new(14, 10, 14, 10), Child = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap } };
        note.SetResourceReference(Border.BackgroundProperty, "Card"); return note;
    }
    private static CheckBox Option(string text, bool on) => new() { Content = text, IsChecked = on, FontSize = 14 };
}
