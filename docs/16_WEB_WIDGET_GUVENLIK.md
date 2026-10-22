# Yerel web widget güvenliği · v0.4

## Desteklenen kapsam

Kullanıcı `widget.json` seçerek güvendiği yerel HTML/CSS/JS paketini ekler. Otomatik indirme, marketplace, ZIP açma veya uzaktan güncelleme yoktur. Paket kendi GUID dizinine kopyalanır; her widget örneği ayrı WebView2 kullanıcı verisi dizini alır. Bu dizinler `%LOCALAPPDATA%/ClaxeDesktop` altındadır; testte `CLAXE_DATA_DIR` kullanılır.

Manifest v1 camelCase alanları: `manifestVersion`, `id`, `name`, `entry`, `defaultSizeDip`, `minSizeDip`, `permissions`, `networkOrigins`, `refreshHintSeconds`. Son alan metadata bilgisidir; host JS zamanlayıcılarını bu aralığa zorlamaz. İzin ve origin listeleri bu sürümde boş olmalıdır. Sadece `theme.getTokens` ve `time.getLocal` JSON mesajları desteklenir. Yanıt `{id,result}` şeklindedir. Geniş native nesne veya dosya API'si yoktur.

## Uygulanan sınırlar

- Kaynak yalnız yerel disk; UNC/ağ sürücüsü reddedilir. Reparse/junction/symlink, kök dışı yol, izin verilmeyen uzantılar reddedilir. En fazla 200 dosya, 200 klasör, 12 seviye; dosya başına 4 MiB, toplam 16 MiB. Manifest 16 KiB ile sınırlıdır.
- Statik içerik yalnız `https://widget.claxe.invalid/` origininden, hostun doğruladığı paket dosyalarından sunulur. Sanal klasör eşlemesi kullanılmaz. GET dışında istek, root dışı yol veya harici kaynak 403 alır.
- CSP: harici bağlantı, fetch/WebSocket, çerçeve, worker, object, form ve base engelleri. Yerel JS ve inline script/style desteklenir. İçerik kullanıcı kodudur.
- Ana navigasyon yalnız manifest giriş dosyası; yeni pencere, indirme ve alt çerçeve navigasyonu engellenir. Host nesneleri, DevTools, varsayılan script diyaloğu, otomatik doldurma/parola kaydı kapalıdır. WebView2 izin talepleri reddedilir.
- Mesajlarda origin, boyut, alanlar ve yöntem doğrulanır. Bilinmeyen komutun yan etkisi/yanıtı yoktur. Tema yanıtı renk tokenları içerir, kişisel dosya yolu içermez.
- Gizleme ve raf kapatma WebView2 denetimini dispose eder; JS/polling bağlamı sona erer. Göstermek yeni bağlam açar, geçici JS durumu korunmaz. Windows kilidi ve ilgili monitörde tam ekran algısı görünürlüğü yönetir. Güvenli modda WebView2 hiç başlatılmaz.
- Başlatma/işlem hatası widget içinde görünür; yeniden başlatma veya gizleme sunulur. Eksik paket kaldırılıp yeniden eklenebilir.

## İddia edilmeyenler

WebView2 bir tarayıcı güvenlik modeli sağlar; bu host işletim sistemi çapında güvenlik duvarı, kötü amaçlı kod analizi veya tam güvenlik sandbox'u değildir. Tarayıcı altyapısının bütün ağ kanalları (örneğin WebRTC ve Runtime'ın kendi hizmetleri) için işletim sistemi seviyesinde ağ yokluğu garantisi verilmez. Dosya/HTTP navigasyonu ve sayfa kaynak politikası gerçek testte doğrulandı; bu, bütün Chromium ağ yollarının denetlendiği anlamına gelmez. Yalnız güvendiğin yerel paketleri ekle. İnternet widget desteği bu sürümün özelliği değildir.

Sert CPU/GPU/RAM kotası ve otomatik aşırı tüketim tespiti yoktur; ağır JS kullanıcı oturumunu zorlayabilir. Gizle/kaldır ve güvenli mod geri dönüş yollarıdır. Aynı Windows kullanıcısının paket dosyalarını sonradan değiştirmesine karşı kriptografik imza veya ayrı düşük yetkili broker uygulanmadı. Uygulamaya telemetri veya gizli içerik indirmesi eklenmedi.

İçe alınmış/kaldırılmış paket ve kullanıcı veri dizinleri otomatik silinmez; sonradan veri temizleme aracı gereklidir. Bu sürüm sadece güvenilen kişisel yerel içerik için ilk hosttur.

## Bağımlılık ve lisans

Microsoft.Web.WebView2 **1.0.4258.31**, Microsoft resmi NuGet SDK. Orijinal `WebView2-LICENSE.txt` ve `WebView2-NOTICE.txt` kaynakta ve çalıştırılabilir paket yanında korunur. WebView2 Runtime gerekir; yoksa widget hata ekranı görünür, otomatik kurulum yapılmaz.

- [Microsoft güvenlik rehberi](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security)
- [WPF WebView2 belgeleri](https://learn.microsoft.com/en-us/microsoft-edge/webview2/platforms/wpf)
- [Sürümlü Windows hedefi](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/tutorials/wpf?view=aspnetcore-10.0)
- [SDK paketi ve lisansı](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4258.31)
