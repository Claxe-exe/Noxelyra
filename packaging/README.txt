Noxelyra 0.11.0 — Windows x64 / Faz 9 adayı

Gereksinimler: Windows 11, .NET 10 Desktop Runtime x64.
Resmi runtime: https://dotnet.microsoft.com/download/dotnet/10.0
Yerel web widget'ları için ayrıca Microsoft Edge WebView2 Runtime gerekir:
https://developer.microsoft.com/microsoft-edge/webview2/
Kurulum ağdan hiçbir şey indirmez. Web widget'ları isteğe bağlıdır.

ZIP dosyasının tamamını bir klasöre çıkar. PowerShell'de:
  powershell -NoProfile -File .\Install.ps1
Kullanıcı kurulum hedefi: %LOCALAPPDATA%\Programs/Noxelyra
Başlat menüsündeki Noxelyra kısayolunu aç.
Taşınabilir kullanım: app\Noxelyra.exe

Alt+K: dock'u mevcut uygulamanın önüne çağır.
Esc / dock ile uygulama seçimi: normal dock davranışına dön.
Alt+K her basışta yeniden öne getirir; etkileşimsiz 15 saniyede normal davranış döner.
Kısayol doluysa Noxelyra bildirim alanı menüsünde Dock'u öne getir seçeneği var.
Güvenli masaüstü veya özel tam ekran oyun üzerinde görünme garantisi yok.

Kaldırma (PowerShell, Noxelyra kurulum klasöründe):
  powershell -NoProfile -File .\Uninstall.ps1
Önce Noxelyra'ya temiz çıkış gönderilir; görev çubuğu ve arka plan kurtarma
kayıtları işlenir. Kurtarma başarısızsa uygulama dosyaları silinmez.
Kişisel veriler %LOCALAPPDATA%\ClaxeDesktop altında korunur.
Yeni sürüm kurulumu: önce kaldır, ardından yeni paketi kur; veriler korunur.
Otomatik güncelleme, Windows Uygulamalar listesi kaydı ve imzalı setup.exe yok.

Betikler ve uygulama şu an dijital imzalı değil. Manifest SHA256 dosya
bütünlüğü içindir; yayıncı imzası yerine geçmez. Windows'un yerel betik
politikası kurulumu engellerse politika değiştirilmez; taşınabilir EXE kullanılabilir.
Kaynak kodun dağıtım lisansı henüz sahibi tarafından seçilmedi.
Bağımlılıklar ve varlık durumu DEPENDENCIES.md içinde.
Temiz Windows 11 VM kabul testi henüz yapılmadı; v1 olarak sunulmaz.

Yeni masaüstü alanı: kontrol merkezinde veya bildirim alanı menüsünde
Noxelyra masaüstü alanını aç. Gerçek masaüstü dosyaların görüntülenir;
çift tık/Enter ile aç, sürükle veya Alt+yön ile yerleştir, Esc ile dön.
Yerel MP4/M4V/WMV videoyu alanın üstündeki düğmeden seç. Video ses kapalı
çalışır; görünmezken, başka uygulamadayken, kilit/pil/tam ekranda durur.
Windows codec desteği gerekir; alan Explorer içine gömülen video değildir.

Özgün Nox maskotu ve gece görseli kontrol merkezinden açılabilir.
Wallpaper Engine isteğe bağlıdır ve ayrıca satın alınıp çalıştırılmalıdır.
Kendi wallpaper32.exe / wallpaper64.exe dosyanı kontrol merkezinden seç.
Seçili videoyu WE ile Windows arka planına uygularken önceki dosya kaydedilir.
Yalnız hâlâ Noxelyra'nın dosyası seçiliyse çıkışta geri yüklenir. Çökmeden
sonra sonraki açılış/kaldırma kurtarmayı dener; WE çalışmıyorsa kayıt korunur.
Önceki WE oynatma/pause durumunu okuma veya geri getirme iddiası yoktur.

0.11 masaüstü modu: kontrol merkezinde Windows masaüstüne dönünce
Noxelyra alanını göster seçeneği var. Görev çubuğunda ayrı uygulama olarak
görünmez; normal uygulamalar önde kalır. Esc/Alt+F4 geçici Windows dönüşü.
WE video değişiminde guardian hazır olmadan mutasyon yapılmaz. Çökmede
yerel guardian önceki WE dosyasını geri getirmeyi dener; harici seçim korunur.
WE 2.8 konsol sorgusu, gerçek video/restore ve izole crash testi doğrulandı.
