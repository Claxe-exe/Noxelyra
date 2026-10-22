# Bağımlılıklar ve dağıtım envanteri

Sürüm: 0.11.0, Windows x64, framework-dependent. Yeni üçüncü taraf bağımlılığı eklenmedi.

| Bileşen | Kullanım / dağıtım | Lisans / kaynak |
| --- | --- | --- |
| .NET 10 Desktop Runtime / WPF / WinForms | Kullanıcı makinesinde gerekir; pakete runtime gömülmez | Microsoft .NET projeleri MIT; runtime içindeki üçüncü taraf bildirimleri geçerlidir. https://github.com/dotnet/windowsdesktop |
| Microsoft.Web.WebView2 SDK 1.0.4258.31 | Yerel web widget host'u; loader ve managed SDK pakette | SDK'nın özgün WebView2-LICENSE.txt ve WebView2-NOTICE.txt dosyaları binary yanında korunur; Microsoft dağıtım koşulları geçerlidir. |
| Microsoft Edge WebView2 Runtime | İsteğe bağlı web widget çalıştırma; bu paket yüklemez veya dağıtmaz | Microsoft Edge WebView2 Runtime dağıtım koşulları; https://developer.microsoft.com/microsoft-edge/webview2/ |
| Windows Win32 / COM / WScript.Shell | Kurulu işletim sistemi API'leri; ayrı dağıtım yok | Windows bileşenleri |
| Claxe C#/XAML ve vektör arayüzü | Proje kaynağı | Kaynak sahibi henüz açık kaynak lisansı seçmedi; MIT/Apache lisansı varmış gibi sunulmaz. |
| assets/references | Yalnız tasarım referansı | Kurulum/ZIP payload'una dahil edilmez; yeniden dağıtım hakkı varsayılmaz. |
| Kullanıcının wallpaper/maskot/web paketleri | LocalAppData altında kişisel içe aktarma | Kurulum paketine katılmaz; haklar kaynağa bağlıdır. |
| Test görselleri | Yerel testte programatik mor elips / ekran görüntüsü | Son ürün maskotu veya dağıtılan karakter varlığı değildir. |

SDK lisanslarının özgün kopyaları `src/ClaxeDesktop.WebWidgets` içindedir.
Sistem fontları ve shell ikonları işletim sisteminden çalışma anında alınır; ayrı font/ikon paketi dağıtılmaz.
GitHub yayımlama öncesi kaynak lisansı seçimi, kod imzalama ve temiz Windows VM kabulü tamamlanmalıdır.

## Noxelyra 0.10 ekleri
WPF MediaElement ve Windows yerel codec desteği video oynatır; yeni NuGet/runtime bağımlılığı eklenmedi. Windows.Media.Editing yalnız test MP4 fixture üretiminde kullanılır. Wallpaper Engine ayrı kurulu üründür; yalnız resmi CLI üzerinden isteğe bağlı çağrılır, pakete dahil edilmez. OriginalArt.cs içindeki ay/dağ ve Nox çizimleri ile Noxelyra.ico özgün proje varlıklarıdır, üçüncü taraf karakter veya font dağıtılmaz. Kaynak/varlık dağıtım lisansı sahibi tarafından kesinleştirilecektir.

0.11: foreground izleme ve özel konsol sorgusu yalnız Windows sistem API'lerini kullanır. Yeni NuGet/SDK veya lisans bağımlılığı eklenmedi. Gerçek WE 2.8 kurulu sistemde uygulama/geri dönüş ve guardian testi yapıldı; WE hâlâ ayrı kurulu isteğe bağlı üründür.
