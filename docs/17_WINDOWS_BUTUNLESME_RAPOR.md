# Windows ile bütünleşme teslimi · v0.5.0

Tarih: **22 Ekim 2026**, Europe/Istanbul. Kullanıcının “hâla pc ile tamamen iç içe hissettirmiyor, bitirelim artık” yönlendirmesiyle Faz 5 ve masaüstü hissi için gerekli statik arka plan entegrasyonu bir arada ele alındı. Bu sürüm bütün yol haritasının bittiği veya Windows shell'inin değiştirildiği anlamına gelmez.

## Sonuç

Claxe paneli isteğe bağlı olarak Windows'un gerçek çalışma alanında yer ayırır. Uygulamalar büyütüldüğünde panelin altında kalan alanı kullanır; panel sıradan bir overlay olmaktan çıkar. Başlatıcı ve sistem kontrolleri panelde, tepside, ayarlarda ve isteğe bağlı global kısayollarda birleşir. Dock bütünleşik modda alt kenara yerleşir. Windows görev çubuğunun otomatik gizlenmesi kullanıcının Windows ayarından seçilir.

## Çalışan özellikler

- **AppBar:** belgelenmiş SHAppBarMessage NEW/QUERYPOS/SETPOS/REMOVE. Yalnız Claxe panel HWND'si kaydedilir. Tam ekran bildiriminde panelin topmost durumu geri çekilir. Kapatmada kayıt kaldırılır; Explorer veya taskbar ayarı değiştirilmez.
- **Launcher:** kullanıcı/ortak Başlat menüsü `.lnk` dizini, gerçek simgeler, sabitlenmiş uygulamalar, Türkçe arama, liste/ızgara, Enter/Esc ve yenileme. Bu PC'de 160 Başlat menüsü kısayolu okundu. Salt Store/AUMID uygulama dizini ayrı indexlenmedi; Windows uygulamaları düğmesi AppsFolder'ı açar.
- **Kontrol merkezi:** gerçek Core Audio çıkış ses düzeyi ve mute. Windows Media Control üzerinden gerçek etkin medya oturumu, metadata ve desteklenen Oynat/Duraklat/Önceki/Sonraki komutları. Erişim yoksa durum açık, kontroller pasiftir. Sistem ses tercihi kullanıcı eylemidir; Claxe çıkınca otomatik olarak geri çevrilmez.
- **Windows ayarları:** Wi-Fi, Bluetooth, ses aygıtları, ekranlar, bildirimler ve görev çubuğu için sabit izinli ms-settings hedefleri. Donanım aç/kapat veya üçüncü taraf bildirim erişimi iddiası yoktur.
- **Kısayollar:** isteğe bağlı Ctrl+Alt+Space launcher, Ctrl+Alt+Q kontrol merkezi. RegisterHotKey/MOD_NOREPEAT; çakışma açık hata, çıkışta UnregisterHotKey. Varsayılan kapalı, güvenli modda kapalı.
- **Arka plan:** gerçek IDesktopWallpaper üzerinden ekran başına statik resim. Kullanıcı seçtiği resmi uygulama verisine PNG olarak kopyalar. En fazla 32 MiB giriş ve 20 megapiksel. Dosya taşınsa bile kopya korunur. Kaynak görsel silinmez.
- **Kurtarma:** Windows görseli değiştirilmeden önce eski/yeni yol atomik ve Flush(true) ile kaydedilir. Temiz çıkışta önceki görsel geri gelir; çökme halinde bir sonraki Claxe açılışında journal okunur. Kullanıcı başka bir arka plana geçmişse onun yeni tercihi korunur. Bağlı olmayan ekranın kaydı saklanır.
- **İlk açılış:** bütünleşme kapalıysa kontrol merkezi açılır. Ayarlar, tepsi ve panelden ulaşılabilir. İkinci kez başlatmak mevcut Claxe'ın kontrol merkezini açar; kullanıcıya özel yerel named pipe yalnız iki dar eylem taşır. Windows ile başlatma düğmesi ilgili ayar kategorisine gider.
- **Etkinlik:** Claxe'ın kendi son sekiz durum mesajı yerel bellekte gösterilir. Sistem bildirimlerini okuduğu veya kalıcı bildirim arşivi tuttuğu iddia edilmez.

## Kullanım

1. `Claxe Desktop.cmd` veya `artifacts/publish/ClaxeDesktop.App.exe` aç.
2. Kontrol merkezinde **Panel için Windows çalışma alanında yer ayır** seç. İstersen global kısayolları da aç.
3. Panelde **C CLAXE** uygulama aramasını, sağdaki **☷** kontrol merkezini açar.
4. Arka plan için Windows arka planını önce **Resim** yap; kontrol merkezinde bu tercihi doğrula, ekranı seç ve **Yerel görsel seç ve uygula** düğmesini kullan.
5. Windows ile başlatma düğmesi oturum ayarlarına götürür; başlangıç tercihi kullanıcıya aittir.

## Doğrulama

`scripts/Verify-Integration.ps1` yalnız **12 kritik kontrol** çalıştırdı: **12/12 başarılı**, temiz çıkış ve marker temizliği doğrulandı. Tam eski test takımı tekrar çalıştırılmadı.

Gerçek API kontrolleri: Core Audio native değer, iki fiziksel monitörün wallpaper API'den okunması, AppBar'ın çalışma alanını daraltması ve kaldırılınca aynı alana dönülmesi, ayarın tema taslağından ayrı saklanması, global kısayol kaydı/serbest bırakılması veya açık çakışma durumu, gerçek Başlat menüsü araması, launcher seçimiyle gerçek probe executable açılması ve kontrol merkezindeki ses değerinin Windows ile eşleşmesi.

Kurtarmanın üç senaryosu **fake wallpaper adapter ile** doğrulandı: yeni oturumdan eski görsele dönüş, harici kullanıcı değişikliğini koruma, bağlı olmayan ekranı bekletip bağlantıda geri alma. Test kullanıcının gerçek duvar kâğıdını, ses düzeyini veya medya oynatmasını değiştirmedi. Gerçek görüntü Set/Restore, ses yazma ve medya komutu kabul testi kullanıcı kullanımında ayrıca gereklidir.

Kanıt `artifacts/phase5-integration-results.json`. Yerel WPF görselleri `artifacts/integration-runtime/verification` içinde; gerçek uygulama/medya isimleri içerebildiği için bu dizin Git dışında tutulur. Son dock yerleşimi, ilk açılış, IPC ve küçük metin düzenlemelerinden sonra bütün 12 kontrol tekrar çalıştırılmadı; son paket yeniden derlendi.

## Açık sınırlar

- Wallpaper desteği **Resim modu** içindir. Slayt gösterisi algılandığında uygulama reddedilir. Spotlight/Wallpaper Engine sahipliği otomatik ve güvenilir biçimde tespit edilmez; kullanıcıdan önce Resim moduna geçmesi istenir. Kurtarma görsel yollarını geri getirir, üçüncü taraf sağlayıcı/slayt listesi/Spotlight modunu geri kurmaz.
- Anlık çökme halinde harici watchdog yoktur; arka plan bir sonraki açılışa kadar Claxe görselinde kalabilir. AppBar normal kapatmada doğrulandı; zorla process sonlandırma/Explorer restart matrisi yapılmadı. Güvenli mod kayıtlı entegrasyonu etkinleştirmez.
- Video/canlı wallpaper, bütün Windows masaüstü ikonlarını devralan yüzey, otomatik profil kuralları, gelişmiş pencere şablonları, maskot ve installer/uninstaller tamamlanmadı. Bunlar v1 bitti diye sunulmaz.
- Tam ekran oyun/RDP/sleep/mixed DPI ve erişilebilirlik matrisi hâlâ gereklidir. Bu doğrulama iki %100 DPI ekranla yapıldı. Varsayılan riskli entegrasyonlar kapalıdır.
- Arka plan varlıkları otomatik temizlenmez. Silinmiş eski Windows görseli kurtarmayı engellerse journal korunur ve kontrol merkezi hatayı gösterir.
- IPC yalnız yerel ve mevcut Windows kullanıcısına açıktır; eski v0.4 örneği IPC dinlemediğinden güncellemede önce tepsiden kapatılmalıdır. Tek örnek IPC akışı bu koşuda uçtan uca test edilmedi.

## Teknik kayıt

Core: isteğe bağlı masaüstü ayarları ve wallpaper recovery sözleşmesi. Platform: Core Audio, Start menu index, IDesktopWallpaper adaptörleri. App: DesktopBindings, DesktopIntegration, LauncherWindow, ControlCenterWindow, MediaService ve DesktopCommands. Yeni NuGet bağımlılığı yok; mevcut .NET Windows SDK ve WebView2 bağımlılıkları korunur.

[AppBar](https://learn.microsoft.com/en-us/windows/win32/shell/application-desktop-toolbars), [Core Audio](https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nn-endpointvolume-iaudioendpointvolume), [IDesktopWallpaper](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-idesktopwallpaper), [Windows medya oturumları](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager).
