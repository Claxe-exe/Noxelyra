using System.Globalization;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;

/// <summary>One place Noxelyra can take you: shown as a card in the hub, or found by search through its keywords.</summary>
internal sealed record HubEntry(string Glyph, string Title, string Description, string Keywords, Action<DesktopController> Open, bool Card = false)
{
    public bool Matches(string query)
    {
        var compare = CultureInfo.GetCultureInfo("tr-TR").CompareInfo; var text = $"{Title} {Description} {Keywords}";
        return query.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(part => compare.IndexOf(text, part, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);
    }
}

internal static class HubEntries
{
    /// <summary>Cards first (the main areas), then search-only shortcuts into the right window, then every catalog setting.</summary>
    internal static IReadOnlyList<HubEntry> All(DesktopController controller)
    {
        var list = new List<HubEntry>
        {
            new("\uE771", "Görünüm ve bar", "Tema, panel stili, köşeler, bar ve dock boyutları, animasyonlar", "renk palet saydamlık cam neon köşe kenarlık saat 12 24 saniye dock simge büyüme gizle", c => c.ShowSettings(), true),
            new("\uE713", "Hızlı ayarlar", "Rahatsız etme, ses, çalan parça, paletler ve bildirimler", "bildirim ses medya wifi bluetooth", c => c.ShowQuickSettings(), true),
            new("\uE765", "Klavye ve kısayollar", "Win tuşu, Alt+Tab, döşeme ve tüm kısayolları değiştir", "win tuşu alt tab döşeme tiling kısayol tuş acil çıkış alt 0 ada", c => c.ShowKeyboard(), true),
            new("\uE7F4", "Masaüstü ve Windows", "Noxelyra masaüstü, görev çubuğu, duvar kâğıdı, açılış, güncellemeler", "masaüstü alanı görev çubuğu taskbar duvar kağıdı wallpaper açılış animasyon klip güncelleme windows koyu mod", c => c.ShowFullControls(), true),
            new("\uE8A9", "Widget atölyesi", "Widget ekle, taşı, boyutlandır", "widget saat takvim not hava durumu müzik grafik büyük saat taşı", c => c.Widgets.ShowEditor(), true),
            new("\uE9D9", "Hafifletme", "Bellek, açılışta başlayan programlar, Windows ekstraları", "ram bellek başlangıç startup servis telemetri performans", c => c.ShowLighten(), true),
            new("\uE8F1", "Profiller ve düzenler", "Uygulamaya göre görünüm profilleri ve pencere düzenleri", "profil kural pencere düzeni maskot", c => c.ShowAdvanced(), true),
            new("\uE8D6", "Noxelyra Music", MusicApp.Installed ? "Eşlikçi müzik ve video oynatıcıyı aç" : "Eşlikçi müzik ve video oynatıcıyı kur", "müzik video oynatıcı youtube spotify ada", c => { if (MusicApp.Installed) c.ViewModel.Status = MusicApp.Open(); else c.ShowFullControls(); }, true),
            new("\uE897", "Kısayollar ve ipuçları", "Tüm kısayollar ve gizli kalan küçük özellikler tek sayfada", "yardım ipucu kısayol rehber nasıl", c => c.ShowShortcuts(), true),
            new("\uE8A1", "Kurulum rehberi", "İlk açılıştaki rehberi yeniden göster", "rehber kurulum hoş geldin başlangıç", c => c.ShowFirstRun(force: true), true),
            // Search-only shortcuts.
            new("\uE7E8", "Windows ile başlat", "Noxelyra oturum açınca başlasın", "otomatik başlat başlangıç oturum startup", c => c.ShowStartupSettings()),
            new("\uE790", "Duvar kâğıdından renk al", "Duvar kâğıdından okunaklı bir palet üret", "pywal renk tema duvar kağıdı rice", c => _ = c.UseWallpaperPaletteAsync()),
            new("\uE8C8", "Rice dışa / içe aktar", "Görünümün tamamını tek dosyayla paylaş", "rice noxrice paylaş yedek dışa aktar içe aktar", c => c.ShowFullControls()),
            new("\uE7BA", "Acil çıkış", "Ctrl+Shift+Ü Noxelyra'yı hemen kapatır, Windows'u eski haline getirir", "acil kapat panik çıkış", c => c.ShowShortcuts()),
            new("\uE8D6", "Dinamik ada kartı", "Adaya tıkla ya da Alt+0: parça, ±10 sn, hava durumu, ses, parlaklık", "ada alt 0 kart müzik hava durumu şehir parlaklık mikrofon", c => c.ShowShortcuts()),
            new("\uE70E", "Gizli simgeler (tepsi)", "Bardaki ˄ düğmesi Windows'un gizli simgeler panelini açar", "tepsi tray gizli simgeler bildirim alanı", c => c.ShowTray()),
        };
        foreach (var setting in SettingsCatalog.Supported)
        {
            var label = setting.Label;
            list.Add(new("\uE713", setting.Label, setting.Category + " · " + setting.Description, setting.Key, c => c.ShowSetting(label)));
        }
        return list;
    }
}
