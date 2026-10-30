using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;

/// <summary>Every shortcut with the keys actually in use, plus the small features that are easy to miss.</summary>
internal sealed class ShortcutsWindow : Window
{
    public ShortcutsWindow(DesktopController controller)
    {
        Title = "Noxelyra · Kısayollar ve ipuçları"; Width = 640; Height = 760; MinWidth = 480; MinHeight = 420; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty, "Canvas"); SetResourceReference(ForegroundProperty, "Text");
        Glass.PrepareFramed(this, controller.ViewModel.Settings); Motion.Attach(this, controller.ViewModel, () => controller.IsExiting);
        var root = new StackPanel { Margin = new(28, 24, 28, 24) }; Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(new TextBlock { Text = "Kısayollar ve ipuçları", FontSize = 24, FontWeight = FontWeights.SemiBold });
        var options = controller.Keyboard;

        Section(root, "Kısayollar");
        Key(root, controller.GestureText("emergency"), "Acil çıkış: Noxelyra'yı hemen kapatır, Windows'u eski haline getirir");
        Key(root, controller.GestureText("island"), "Dinamik ada kartı her yerde (oyunda da); tekrar basınca kapanır");
        Key(root, controller.GestureText("dock"), "Dock'u öne getir");
        Key(root, controller.GestureText("launcher"), "Başlatıcı");
        Key(root, controller.GestureText("controls"), "Hızlı ayarlar");
        Key(root, controller.GestureText("layout"), "Pencere düzenleri");
        if (options.SuperLauncher) Key(root, "Win", "Tek basış: Noxelyra başlatıcısı (Win+E, Win+L gibi kısayollar Windows'ta kalır)");
        if (options.AltTab) Key(root, "Alt+Tab", "Canlı önizlemeli pencere değiştirici · Alt'ı bırakınca geçer, Esc vazgeçer");
        if (controller.ViewModel.Settings.DesktopSpace.CaptureShowDesktop) Key(root, "Win+D", "Noxelyra masaüstü; tekrar basınca pencereler geri gelir");
        if (options.Tiling)
            foreach (var action in KeyboardOptions.TilingActions) Key(root, controller.GestureText(action), KeyboardOptions.Label(action));
        var change = Muted("Kısayolları Ayarlar → Klavye ve kısayollar'dan değiştirebilirsin."); change.Margin = new(0, 8, 0, 0); root.Children.Add(change);

        Section(root, "Ada kartı açıkken");
        Key(root, "← / →", "10 saniye geri / ileri sar");
        Key(root, "↑ / ↓", "Ses");
        Key(root, "Boşluk", "Oynat / duraklat");
        Key(root, "Tab", "Sayfa değiştir (müzik ↔ PC kontrolleri)");
        Key(root, "Esc", "Kapat");

        Section(root, "İpuçları");
        foreach (var tip in new[]
        {
            "Dinamik adaya sağ tıkla: zamanlayıcı ve kronometre.",
            "Ada kartında kapağa tıkla: arka plandaki videoyu aç / kapat. Video, Noxelyra Music'te veya ekranda görünen bir tarayıcı penceresinde oynarken gelir.",
            "Bardaki ˄ düğmesi Windows'un gizli simgeler panelini açar; görev çubuğu gizliyken de çalışır.",
            "Bardaki ⚙ hızlı ayarlar ve bildirimler; dişlinin üstündeki nokta okunmamış bildirim var demek.",
            "Widget atölyesinde önizlemedeki kartları sürükleyerek widget'ları taşı; Kaydet'e basmayı unutma.",
            "Pencereyi elle taşırsan ya da Win+oklarla yerleştirirsen döşemeden çıkar; Windows Snap bozulmaz.",
            "Noxelyra masaüstünde Ctrl+F dosya arar, Esc Windows masaüstüne döner.",
            "Görünümünü tek dosyayla paylaş: Ayarlar → Masaüstü ve Windows → Rice.",
        })
        {
            var line = new TextBlock { Text = "•  " + tip, TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 8), FontSize = 13 };
            root.Children.Add(line);
        }
    }
    private static void Section(Panel parent, string title) => parent.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new(0, 22, 0, 8) });
    private static void Key(Panel parent, string keys, string what)
    {
        var row = new DockPanel { Margin = new(0, 3, 0, 3) };
        var chip = new Border { CornerRadius = new(8), Padding = new(10, 4, 10, 4), MinWidth = 150, Margin = new(0, 0, 14, 0), Child = new TextBlock { Text = keys, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center } };
        chip.SetResourceReference(Border.BackgroundProperty, "Card"); DockPanel.SetDock(chip, Dock.Left); row.Children.Add(chip);
        row.Children.Add(new TextBlock { Text = what, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, FontSize = 13 });
        parent.Children.Add(row);
    }
    private static TextBlock Muted(string text) { var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12 }; block.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); return block; }
}
