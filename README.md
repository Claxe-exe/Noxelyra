# Noxelyra · v0.15

Windows 11 üzerinde panel, dock, widget ve kişisel masaüstü alanı. Önceki adı Claxe Desktop idi; mevcut ayarlar korunur.

## Hızlı kullanım

- **Noxelyra.cmd** veya `artifacts/publish-v0.15/Noxelyra.exe` aç.
- **Alt+K** her basışta dock'u mevcut uygulamanın önüne getirir. **Esc**, dock'tan uygulama seçimi veya etkileşimsiz 15 saniye normal davranışı geri getirir. Genel Ctrl+Alt kısayol ayarından bağımsızdır. Çakışmada bildirim alanı menüsündeki **Dock'u öne getir** kullanılabilir.
- Kontrol merkezinden veya bildirim alanından **Noxelyra masaüstü alanını aç**. Gerçek Desktop dosyalarını ara, çift tık/Enter ile aç, sürükle veya Alt+yön ile yerleştir. **Esc** Windows masaüstüne döner. Alanın oturumla açılması kapalı varsayılandır.
- **Windows masaüstüne dönünce Noxelyra alanını göster** seçeneği, alanı görev çubuğunda ayrı uygulama yapmadan masaüstünde tutar. Diğer uygulamalar önde kalır; otomatik gösterim odağını çalmaz. Esc/Alt+F4 geçici olarak Windows'a döner. Bu mod yeni kurulumda kapalıdır.
- Alanın üstünden yerel MP4/M4V/WMV seç. Sessiz döngü; gizleme, minimize, başka uygulama, kilit, pil ve fullscreen durumunda pause. Windows codec desteği gerekir.
- **Özgün Nox maskotu** ve **Özgün gece görselini Windows'a uygula** kontrol merkezindedir. Kendi yerel görsellerini kullanmaya devam edebilirsin.

## Windows ve Wallpaper Engine

Panel için isteğe bağlı gerçek AppBar çalışma alanı, sahiplikli Windows görev çubuğu otomatik gizleme ve statik wallpaper kurtarması vardır. Çıkışta eski tercihler geri alınır; görev çubuğu değişikliği gerekiyorsa ayrı yerel crash yardımcısı kullanılır. Explorer kapatılmaz/değiştirilmez. Windows ses düzeyi, medya oturumları ve ilgili Windows Ayarları bağlantıları gerçek API'lere bağlıdır.

Wallpaper Engine ayrı ve isteğe bağlıdır. Kurulu/çalışan `wallpaper32.exe` veya `wallpaper64.exe` dosyanı kontrol merkezinden seç. Geçerli dosyayı oku, açık seçiminle tüm WE arka planlarını pause/play yap veya seçili yerel videoyu WE'nin varsayılan ekranına uygula. Dosya değişimi doğrulanır; önceki dosya kaydedilir ve yalnız hâlâ Noxelyra'nın seçimi kullanılıyorsa geri yüklenir. Kurtarma başarısızsa kayıt korunur. Önceki WE oynatma durumu veya çok monitör eşlemesi geri getirildiği iddia edilmez. [Resmi WE komutları](https://help.wallpaperengine.io/en/functionality/cli.html).

Noxelyra'nın video/ikon alanı bağımsız bir penceredir. Windows masaüstü ikonlarını silmez/gizlemez, Explorer içine gömülmez; Masaüstü izleme açıksa alan masaüstünde kalır; Esc ile geçici olarak Windows masaüstüne dönebilirsin. Explorer arkasında yerleşik video istenirse WE adaptörü gerekir. Secure desktop/exclusive fullscreen üzerine görünme garantisi yoktur. [Microsoft wallpaper API](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-idesktopwallpaper-setwallpaper).

## Mevcut özellikler

- Masaüstü alanında cam başlık, widget/dock etrafından dizilen simgeler, isteğe bağlı simge gizleme ve kendi yerel arka plan görseli.
- Hafifletme: bellek kullanımı, açılışta başlayan programlar, Windows ekstraları; geri alınabilir.
- Waybar tarzı adacıklı üst bar: odaktaki pencere, saat, medya, ses, CPU/RAM, palet menüsü.
- Midnight Noxelyra / Tempest / Sakura / Catppuccin Mocha / Tokyo Night / Nord / Rosé Pine, görünüm düzenleme, canlı önizleme, undo/redo, atomik JSON ayarlar.
- Taşınabilir panel/dock, gerçek shell simgeleri, açık pencere gruplama/aktivasyon ve kullanıcı kısayolları.
- Saat, takvim, not, gerçek sistem verisi widget'ları; yerleşim atölyesi ve izinleri sınırlı yerel web paketleri.
- Portatif görsel/yerel widget profilleri; uygulama/saat/ekran sayısıyla isteğe bağlı otomasyon, manuel koruma.
- Kullanıcının seçtiği pencereleri ikili/üçlü/2×2 düzenleme ve son yerleşimi geri alma; isteğe bağlı Ctrl+Alt+L.
- Yerel iki ifadeli maskot; fullscreen/kilit gizleme ve azaltılmış hareket/pil davranışı.

## Kurulum ve geliştirme

`artifacts/Noxelyra-0.15.0-win-x64.zip` dosyasının tamamını çıkar. PowerShell'de `Install.ps1`; kaldırmak için kurulum kökündeki `Uninstall.ps1`. Yönetici, ağdan indirme veya gizli otomatik başlatma yoktur. Varsayılan kurulum `%LOCALAPPDATA%/Programs/Noxelyra`; kişisel veriler uyumluluk için `%LOCALAPPDATA%/ClaxeDesktop` altında kalır. Kaldırma önce Windows durumunu kurtarır, kişisel verileri korur. Başlangıç zaten açıksa eski sahip olunan Claxe kısayolu yeni Noxelyra EXE'ye güncellenir. Yeni kurulum isteğe bağlı olarak ilk açılışta sorulur.

.NET 10 Desktop Runtime x64 gerekir; yerel web widget'ları için WebView2 Runtime gerekir. Windows betik politikası değiştirilmez; taşınabilir `app/Noxelyra.exe` alternatifi vardır.

```powershell
dotnet build ClaxeDesktop.sln -c Release
dotnet run --project src/ClaxeDesktop.App -c Release
./scripts/Verify-Noxelyra.ps1
./scripts/Package-Release.ps1
```

Namespace, çözüm ve IPC adlarının ClaxeDesktop kalması geriye uyumluluk içindir; ayrı Claxe Browser uygulaması değiştirilmez. Eski `Claxe Desktop.cmd` yeni sürüme yönlenir.

## Doğrulama ve sınırlar

Günlük kontrol yaklaşık **12 kritik kontrol** ile sınırlıdır. MP4 gerçekten Windows'ta üretilip decode edilir; fullscreen sinyali kontrollü test adapter'ıdır. Native hotkey kaydı ve WM_HOTKEY yolu sınanır; fiziksel klavye/oyun testi bununla eş tutulmaz. WE bulunmayan ortamda eksik kurulum fallback'i ve fake adaptörde journal sahipliği sınanır.

[Son masaüstü kabul raporu](docs/22_MASAUSTU_KATMANI_KABUL.md), [önceki özellik raporu](docs/21_NOXELYRA_RAPOR.md), [bağımlılık/lisans envanteri](docs/DEPENDENCIES.md), [geçmiş faz raporu](docs/20_FAZ_9_RAPOR.md).

Temiz Windows VM, fiziksel Alt+K ve exclusive fullscreen oyun, karma DPI, fiziksel uyku–uyanma, uzun yük ve ekran okuyucu turu tamamlandı sayılmaz. Gerçek WE dosya uygulama/geri dönüş ve crash guardian kabulü geçti; oyun turu son kullanıcı yönlendirmesiyle kapsam dışıdır. Kaynak dağıtım lisansı henüz sahibi tarafından seçilmedi. Kod imzası ve imzalı setup.exe / Windows Uygulamalar listesi kaydı yoktur. Bütün Faz 9 kabul kapıları veya v1 hazır denmez.

Gerçek WE kabulü: scripts/Verify-DesktopAcceptance.ps1. Bu özel test videoyu geçici uygular ve önceki WE sahnesini geri getirir; yalnız kendi izole fixture sürecini crash testi için durdurur. Sonuç artifacts/desktop-acceptance.json. WE 2.8 için kendi gizli konsolunu kullanan geçici sorgu yardımcısı vardır; kullanıcının konsolu okunmaz.
