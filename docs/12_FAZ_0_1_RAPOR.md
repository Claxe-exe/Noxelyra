# Faz 0–1 teslim raporu

Tarih: **21 Ekim 2026**, Europe/Istanbul. Çalışma alanı: yerel geliştirme klasörü.

## Yapıldı

- Paket içindeki gereksinimler, 23 kategori / 267 ayarlık katalog, beş UI referansı ve üç atmosfer görseli incelendi. Orijinal paket değiştirilmeden çalışma alanına kopyalandı.
- Git deposu, `ClaxeDesktop.sln`, .NET 10 Core / Platform.Win / WPF App projeleri ve bağımlılıksız test projesi kuruldu.
- Midnight Claxe tema: koyu mor saydam yüzeyler, yuvarlatılmış panel, dock, gerçek saat/tarih ve CPU/RAM kartı.
- Panel/dock/widget hareketi; 12 DIP ızgara; panel genişlik/yüksekliği ve dock simge boyutu; monitör kimliğiyle konum kaydı.
- Yerel `.exe`/`.lnk` kısayol ekleme, kaldırma, gerçek başlatma; hatada kullanıcıya görünür durum.
- Üç kategorili ayarlar: görünüm/düzen, kısayollar, ekranlar/oturum. Canlı önizleme, Kaydet, Geri al; kapanışta kaydedilmemiş önizlemeyi iptal.
- Şema validasyonu, atomik JSON, yedek kurtarma, seri kayıtlar, gelecekteki şemayı salt okunur koruma; hata türü/kodu içeren yerel log.
- Tek süreç, tepsi menüsü, açık çıkış, kirli kapanış tespiti ve `--safe-mode`.
- İlk kurulumda Windows başlangıç tercihi sorusu; açık seçimle kullanıcı Startup klasöründeki Claxe kısayolu. Testlerde etkinleştirilmedi.
- Yüksek kontrast başlangıç paleti, kontrol etiketleri ve klavye odağı. Alt + yön tuşları ile hareket yolu eklendi.
- Yerel çalıştırılabilir paket: `artifacts/publish/ClaxeDesktop.App.exe`. Kurulum gerektirmez; kurulu .NET 10 Desktop Runtime kullanılır. Tüm publish klasörü birlikte tutulmalıdır.

## Ortam

| Bileşen | Gerçek sonuç |
|---|---|
| Windows | Build 26200.9711, 25H2, x64; registry ProductName eski `Windows 10 Pro` metnini döndürüyor |
| .NET SDK / MSBuild | 10.0.401 / 18.9.11 |
| Desktop Runtime | 10.0.12 |
| WebView2 Runtime | 155.0.4283.62; faz 1'de kullanılmıyor |
| Monitör | İki adet; her birinde 1920 × 1032 çalışma alanı, %100 DPI |
| Bellek ölçümü | Yaklaşık 15.9 GB toplam; kullanım anlık olarak gerçek Windows ölçümünden |
| Yeni NuGet bağımlılığı | Yok |

Sandbox içinde `dotnet --info` servis erişimi hatası, ilk build ise ayrıntısız hata verdi. Aynı komutlar izinli dış yürütmede başarılı oldu. SDK kurulumu gerekmedi. Normal uygulama manifesti `asInvoker` kullanır; yönetici istemez.

## Derleme/test

| Komut / senaryo | Sonuç |
|---|---|
| `dotnet restore ClaxeDesktop.sln` | Başarılı, dört proje güncel |
| `dotnet build ClaxeDesktop.sln -c Release --no-restore` | **0 hata, 0 uyarı** |
| `dotnet test ClaxeDesktop.sln -c Release --no-restore` | **22 / 22 başarılı** |
| `scripts/Verify-Desktop.ps1` | **12 / 12 WPF kontrolü başarılı**, normal mod, temiz çıkış |
| `scripts/Verify-Desktop.ps1 -SafeMode` | **12 / 12 WPF kontrolü başarılı**, kayıtlı konum korunuyor |
| Test sürecini zorla kapatıp yeniden açma | Kirli kapanış algılandı, otomatik güvenli mod açıldı; 10 kontrol başarılı (harness'a sonradan 2 kontrol daha eklendi) |
| `dotnet publish ... -c Release --no-restore -o artifacts/publish` | Başarılı |

Test yürütücüsü özel MSBuild hedefinden çalışan gerçek executable'dır; standart xUnit/MSTest adaptörü veya TRX üretmez. Sayılar gerçek çalıştırılan kontrollerdir. Üniteler: doğrulama, path/launch policy, eksik monitör, negatif koordinatlar, DPI clamp, grid, yedek kurtarma, ileri şema, iptal/eşzamanlı yazım, crash marker, log gizliliği, gerçek ekran/CPU/RAM.

WPF kontrolleri: üç gerçek pencerenin görünmesi; saat ve CPU bağları; başlangıçta görünüm yükleme; canlı boyut değişimi; geri alma; diskten tekrar okuma; panelin gerçek ikinci monitöre taşınması; hareketin kalıcılığı veya safe-mode koruması; gerçek Kaydet düğmesi olayı; render PNG üretimi; WindowsLauncher üzerinden gerçek bir test executable'ının başlatılması. Başlatma testi her çalışmada benzersiz çıktı dosyası kullanır.

Kanıtlar: `artifacts/normal-results.json`, `artifacts/safe-mode-results.json`, `artifacts/recovery-results.json`; `artifacts/screenshots/` altında Settings, Panel, Widget ve Dock PNG'leri. PNG'ler uygulamanın gerçek WPF görsel ağacı render'ıdır; mockup veya bütün masaüstü ekran görüntüsü değildir.

## Güvenli çıkış testleri

- Normal ve güvenli mod çıkışında `session.lock` kaldırıldı.
- Zorla kapatılan test uygulamasından sonra Explorer PID **9560** olarak aynı süreçte kaldı; testte kill/restart veya taskbar manipülasyonu yapılmadı.
- Bozuk ayar dosyasından yedek kurtarma ve iptal edilen kayıt sonrasında önceki dosyanın korunması test edildi.
- Testler yalnız `artifacts/runtime` içindeki izole verileri kullandı. Gerçek kullanıcı ayarları/başlangıç tercihi değiştirilmedi.
- Çıkış testi controller'ın gerçek uygulama çıkış yolunu çalıştırır. Her tepsi menüsü ve fiziksel mouse/klavye etkileşimi ayrıca kullanıcı kabulünden geçmiş sayılmaz.

## Eksikler ve teknik sınırlar

- **Faz 2+ uygulanmadı:** çalışan uygulamaları listeleme/gruplama, autohide, widget editörü/WebView2, wallpaper motoru, fullscreen pause, sistem ses/ağ panelleri, profil otomasyonları ve maskot.
- Gerçek DWM arka plan blur yok; saydam yüzey var. Dock ikonları ad baş harfleridir. Başka uygulamalar panel/dock'u örtebilir; tepsi erişimi vardır.
- Aynı DPI'lı iki gerçek ekranda konum testi yapıldı. Fiziksel %125/%150 karışık DPI, ekran sök/tak, 144/75 Hz, RDP, kilit/sleep dönüşü ve oyun matrisi tamamlanmadı. Saf yerleşim testleri fiziksel donanım testinin yerine geçmez.
- Autostart oluşturma/kaldırma, ilk kurulum sorusunda gerçek tercih değişikliği, erişilebilirlik okuyucusu ve yüksek kontrastta görsel kontrol elle doğrulanmadı.
- UI otomasyonu pencereleri okudu; bazı girdilerde `window bounds changed` / kullanıcı girdisi uyarısı nedeniyle fiziksel tıklama/sürükleme testleri tamamlanamadı. WPF entegrasyon harness'ı alttaki gerçek kod yollarını ve Save düğmesini doğruladı.
- Başlangıç süresi, idle CPU, GPU/FPS ve p95 frame time için ölçümlü performans raporu henüz yok. Sistemin genel CPU/RAM göstergeleri uygulamanın kendi performans ölçümü değildir.
- Installer/uninstaller yok. Kaynak kod yayım lisansı seçilmedi. Kullanıcının atmosfer görselleri ikili pakete gömülmedi ve Git'te dışarıda bırakıldı.

## Değişen dosyalar

Yeni uygulama kodları `src/`, testler `tests/`, test komutu `scripts/Verify-Desktop.ps1`; solution, build props, SDK pin, `.gitignore`, README, CHANGELOG ve DECISIONS oluşturuldu. Planlama belgeleri korundu; bu rapor uygulamanın mevcut durumunu tanımlar. Orijinal `FILES_MANIFEST.md` yalnız devir paketinin manifestidir.

## Sonraki faz önerisi

Önce kullanıcı v0.1'i kendi masaüstünde deneyip görünümü ve taşımayı değerlendirsin. Onay sonrası **Faz 2: gerçek uygulama ikonları/açık pencere takibi ve dock/panel davranışını olgunlaştırma**. Faz 2'ye başlanmadı.
