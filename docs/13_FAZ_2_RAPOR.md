# Faz 2 · Dock ve pencere yönetimi teslimi

Tarih: **21 Ekim 2026**, Europe/Istanbul. Sürüm: **0.2.0**. Kullanıcı Faz 2'yi açıkça onayladı; Faz 3'e başlanmadı.

## Yapıldı

- Windows'tan gerçek açık pencere listesi: görünmeyen/cloaked, araç, noactivate ve uygun olmayan owner pencereleriyle Claxe'ın kendi pencereleri filtrelenir. Küçültülmüş uygulamalar listede kalır.
- Uygulamaya göre gruplama, sayım, sabitlenmiş uygulamayla çalışan pencerenin birleştirilmesi; gruba tıklayınca kararlı sırayla pencere döngüsü; sağ tık menüsünde tek tek pencere seçimi.
- Pencere küçültülmüşse geri açma. Windows odak iznini reddederse açık durum ve Alt+Tab yönlendirmesi. İzin zorlayan bir mekanizma kullanılmaz.
- Gerçek Shell ikonları, yerel .lnk hedefi okuma, arka plan STA işçisi ve önbellek. Alınamayan simgede isim baş harfi. Native ikonlar kopyalandıktan sonra serbest bırakılır. Ağ hedefli kısayol/ikon metadata'sı arka planda yüklenmez.
- Sağ tıkla sabitle/kaldır, yeni örnek aç ve sıralama. Kısayol eylemleri kendi listesini hemen kaydeder; diğer ayar önizlemelerini istemeden commit etmez.
- Serbest veya üst/alt çalışma alanı kenarına bağlı dock. Tutamaktan sürükleme serbest yerleşime geçirir.
- İsteğe bağlı otomatik gizleme: 650 ms, 6 DIP görünür tutamaç, pointer/klavye/menü/sürükleme sırasında korunma. İsteğe bağlı önde tutma; tam ekranda dock'un geri çekilip yeniden görünmesi.
- Kapatılabilir hover büyümesi; sistem az hareket tercihi. Panel saatini sol/sağ tarafa taşıma ve panel sağ tık menüsü.
- Ekran değişiminde dock/panel ölçüleri ve yerleşim yeniden hesaplanır. Eksik monitörün kayıtlı kimliği korunur.
- Uygulama mimarisinde IWindowService ve test edilebilir filtre/grup/gizleme kararları. Yeni NuGet bağımlılığı yok.

## Nihai masaüstü hedefi

Kullanıcının netleştirmesi `docs/01_VIZYON_KARARLAR.md` içine işlendi: Claxe Windows ile açılan ana masaüstü deneyimi olacak; gerektiğinde mevcut arka plan görünümünü ve bütün masaüstü yüzeyini kendi görünümü/etkileşimleriyle yönetecek. Bu teslim bu hedefin dock/pencere altyapısıdır. Tam masaüstü yüzeyi, wallpaper motoru ve sistem kontrolleri henüz tamamlanmış değildir. Explorer güvenli geri dönüş altyapısı olarak korunur.

## Gerçek doğrulama

| Kontrol | Sonuç |
|---|---|
| Release build | **0 hata, 0 uyarı** |
| `dotnet test ClaxeDesktop.sln -c Release` | **30 / 30 başarılı** |
| Normal WPF entegrasyon testi | **28 / 28 zorunlu kontrol başarılı** |
| Güvenli mod WPF entegrasyon testi | **28 / 28 zorunlu kontrol başarılı** |
| Gerçek koordinatla dock grubu tıklaması | **Pencereye geçiş başarılı**; `dock-activation-succeeded` yerel olayı |
| Panelin gerçek çıkış düğmesi | **Temiz kapanış başarılı**; `clean-exit`, session.lock yok |
| Explorer | Test sonrasında PID **9560** ile aynı süreç çalışıyor |
| Yerel publish | `artifacts/publish/ClaxeDesktop.App.exe`, .NET 10 Desktop Runtime kullanır |

Her WPF raporunda ek olarak **1 bilgi amaçlı otomatik foreground denemesi** vardır: Windows bu arka plan isteğini reddetti. Bu sonuç başarı sayısına dahil edilmez ve raporda `informational: true, passed: false` olarak açıkça bulunur. Gerçek koordinat tıklaması ayrı testte başarılı oldu. Reddin yönetilmesi ve pencerenin küçültmeden geri açılması da doğrulandı; otomatik odak isteğinin her koşulda çalıştığı iddia edilmez.

WPF testleri on **gerçek yardımcı HWND** ile liste doğruluğu, tool-window filtresi, gruplama/açma, ikon, bağlam menüsünden sabitleme/kaldırma ve kalıcılığı doğrular. On farklı üçüncü taraf uygulamanın tamamında kabul testi yapıldığı anlamına gelmez. Monitör sökülmesi, %150 DPI ve fullscreen sinyali testleri kontrollü adaptör **simülasyonlarıdır**; iki gerçek %100 DPI ekranında panel taşıma ayrıca doğrulanmıştır.

Kanıtlar:

- `artifacts/phase2-normal-results.json`
- `artifacts/phase2-safe-results.json`
- `artifacts/phase2-interaction-results.json`
- `artifacts/phase2-screenshots/` içindeki gerçek WPF render'ları; `Dock-Running.png` gerçek simgeleri ve on pencerelik grubu gösterir.

İlk entegrasyon denemelerinde yardımcı pencerenin oluşması/küçültülmesi bitmeden sayım yapılabildi. Test aracı hazır işareti ve durum bekleme ile düzeltildi; son iki moddaki sonuçlar yukarıdadır. Bir derleme, açık test executable'ı dosyayı kilitlediği için başarısız oldu; test süreci kapatıldıktan sonra son Release build temiz geçti. Sonuçlar bu başarısız denemeleri gizlemez.

## Değişen dosyalar

- Core: `Models.cs` içinde gerçek dock ayarları; `WindowModels.cs` içinde pencere sözleşmeleri/filtre/gruplama/gizleme kararları.
- Platform.Win: `WindowsWindowService.cs` ile documented Win32 pencere erişimi.
- App: DockWindow, PanelWindow, FloatingWindow, DesktopController, DesktopViewModel, SettingsWindow, App başlangıcı; yeni ShellIconService ve genişletilen SmokeVerifier.
- Testler: çekirdek takımına sekiz anlamlı test; kontrollü WPF WindowProbe projesi ve Verify-Desktop komutu.
- README, CHANGELOG, DECISIONS, ürün vizyonu ve bu teslim raporu güncellendi.

## Eksikler ve sınırlar

- **AppBar çalışma alanı rezervasyonu uygulanmadı.** Kenar yerleşimi Windows'un mevcut çalışma alanını kullanır; normal maximized pencerelerden kalıcı yer ayırmaz.
- Fiziksel monitör sök/tak, karışık DPI, on farklı uygulama, sleep/RDP, tam ekran oyun ve donanım performans matrisi tamamlanmadı. Fullscreen boyut kontrolü bir sezgiseldir; her oyun/renderer için garanti verilmez.
- Paketli uygulamaların AppUserModelID tabanlı simge/kimlik birleştirmesi ve elevated uygulamalara tam erişim yoktur. Path alınamazsa pencereler daha az gruplanabilir; sistem yetkisi zorlanmaz.
- Panelde saat sol/sağ taşınabilir; tüm panel öğeleri için görsel sürükle-bırak editörü ve tam profil yönetimi sonraki fazlardadır.
- Wallpaper, launcher, web widget, sistem kontrolleri ve tam masaüstü modunun kalan parçaları ileride. Dock fullscreen görünürlüğü, wallpaper/widget pause motoru olarak sunulmaz.
- Kurulum/kaldırma paketi ve ölçümlü idle CPU/GPU/frame performans raporu yok. Kullanıcı görselleri ikiliye eklenmedi.

## Sonraki öneri

**Faz 3:** kategori/arama/validasyon temelli kapsamlı ayarlar ve Midnight/Tempest/Sakura tema altyapısı. Kullanıcı onayı bekleniyor.
