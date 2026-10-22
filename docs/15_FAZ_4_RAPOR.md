# Faz 4 teslim raporu · Widget Engine ve Editor

Tarih: **22 Ekim 2026**, Europe/Istanbul. Sürüm: **0.4.0**. Kullanıcı Faz 4'e geçişi onayladı. Faz 5'e başlanmadı.

## Yapıldı

- Bağımsız widget örnekleri: gerçek saat/tarih, CPU/RAM, aylık takvim, yerel not. 30 örnek sınırı; kimlik, boyut, metin, görünürlük, kilit, katman, grup ve monitör konumu doğrulanır.
- Midnight Claxe atölyesi: monitör seçimi, ekran düzeni önizlemesi, ızgara, serbest/snap, çoklu seçim, Alt+yön ve başlıktan taşıma, köşe tutamacıyla boyut, X/Y ve boyut alanları, hizalama, grup/çöz, katman, çoğalt/kaldır, kilit, gizleme.
- Taslak, 50 adım geri al/yinele, Kaydet/İptal. Kaydet ve İptal inspector altında sabittir. Widget kaydı atomiktir ve tema önizlemesini yanlışlıkla kaydetmez. Kayıt hatası editörde görünür.
- Gizlenebilir raf; web widget gizlenince denetim dispose edilir. Oturum kilidi/tam ekran görünürlüğü yönetilir. Güvenli modda web ve widget düzenleme kapalıdır.
- Şema 1/2 → 3 geçişi; eski saat kartının konumu yeni örneğe aktarılır. Monitör kaybolduğunda fallback, kayıtlı kimliği koruyarak uygulanır; küçük çalışma alanında pencere ölçüsü geçici olarak sığdırılır.
- Yerel HTML/CSS/JS paket importu, manifest ve dosya sınırları, kontrollü origin, dar tema/saat mesajları, izin/navigasyon/kaynak engelleri. Çalışan offline saat örneği `samples/widgets/offline-clock` içindedir.
- Web host ayrı projedir; Microsoft WebView2 SDK lisans/bildirimleri pakette yer alır. Explorer ve Claxe Browser değiştirilmedi.

## Derleme/test

- Release derlemesi: **0 hata, 0 uyarı**.
- Son günlük widget seti: `dotnet run --project tests/ClaxeDesktop.Core.Tests -c Release -- --widgets` → **12/12 geçti**. Kullanıcının test sayısını azaltma tercihi AGENTS.md'ye kaydedildi. Eski regresyonlar silinmedi; tam takım 57 kontrol içerir ve rutin olarak tekrar koşturulmaz.
- Bu tercih gelmeden önce geniş regresyon yapıldı: 60/60 çekirdek kontrolü; gerçek WPF normal modunda **73/73**, güvenli modda **54/54** gerekli kontrol geçti. Foreground odak sonucunun Windows tarafından reddedilmesi bilgi amaçlıdır, gerekli başarı sayısına dahil değildir. Son footer/tutamaç/ekran sığdırma dokunuşlarından sonra bu geniş matris tekrar çalıştırılmadı.
- Gerçek iki monitör bulundu (1920×1032 çalışma alanı, %100 DPI). Native HWND ikinci ekrana taşındı, konumu ve widget içeriği diskte yeniden okundu. Bu kabul testi programatik Win32 konumlandırmadır; fiziksel fareyle iki ekran sürükleme ve karışık DPI kabulü ayrıca gereklidir.
- Gerçek WebView2 yerel HTML açtı; iki host mesajına yanıt geldi. Harici fetch CSP ile, harici HTTPS ve file navigasyonları navigasyon politikasıyla engellendi. Bilinmeyen host yöntemi yanıtsız kaldı. Gizlemede kontrol dispose edildi; gösterimde yeni örnek oluştu.
- İlk deneme Windows.SDK.NET hedef eksikliğini buldu ve düzeltildi. Bir sonraki koşuda WebView2 bağlamı beklenmedik dispose oldu; teşhis durumu eklendi. Son normal koşu geçti ancak o tek yaşam döngüsü kesintisinin kesin nedeni izole edilmedi; uzun süreli WebView2 dayanıklılığı açık sınırdır.
- Paket: `artifacts/publish/ClaxeDesktop.App.exe`, **0.4.0**. Klasörün tamamı birlikte kullanılmalıdır. .NET 10 Desktop Runtime ve web için WebView2 Runtime gerekir.

## Güvenli çıkış

Normal ve güvenli entegrasyon koşuları `clean-exit` ile kapandı, `session.lock` temizlendi. Son süreç okumasında Explorer aynı **9560** PID ve önceki başlangıç zamanıyla çalışıyordu. Shell kayıt değeri, görev çubuğu ayarı ve Explorer işlemi değiştirilmedi. Ayrı Claxe Browser dosyalarına dokunulmadı.

## Eksikler / teknik sınırlar

- Medya widget ve gerçek medya oturumu kontrolleri Faz 5'te ele alınacak; boş Play/Pause yok.
- Izgara atölye önizlemesidir. Tam masaüstü yüzeyi, wallpaper motoru, Widget docking/breakpoint/preset sistemi ve sınırsız tema/profil motoru bu sürümde yoktur.
- WebView2 tam işletim sistemi güvenlik sınırı veya sert kaynak kotası değildir. Ağın bütün Chromium/Runtime yollarında sıfır olduğu iddia edilmez. Yalnız güvenilen yerel paketler kullanılmalıdır. Ayrıntılar `docs/16_WEB_WIDGET_GUVENLIK.md` içindedir.
- Gizleme web JS durumunu sıfırlar. Paket/kullanıcı verisi temizleme UI'si henüz yoktur.
- Oyun/RDP/sleep, fiziksel mixed DPI/sök-tak, uzun süreli kaynak tüketimi, screen reader ve tam klavye kabul matrisi tamamlanmadı. Statik notlar yereldir; şifreleme iddia edilmez.
- Normal pencerelerin üzerinde kalıcı desktop/shell katmanı kurulmadı. Nihai Windows ile açılıp masaüstü deneyimini yönetme hedefi korunuyor; Explorer güvenli fallback olmaya devam eder.

## Değişen dosyalar ve kanıt

Core widget modelleri/geçmişi/paket politikası, yeni WebWidgets projesi, WidgetWindow/Workspace/Editor/Verifier, controller/persistence entegrasyonu, SDK hedefi ve 12 odaklı test. README, CHANGELOG, DECISIONS ve güvenlik belgesi güncellendi.

Kanıt: `artifacts/phase4-normal-results.json`, `artifacts/phase4-safe-results.json`, `artifacts/phase4-screenshots`. PNG'ler uygulamanın kendi WPF görsel ağacı ve WebView2 CapturePreview çıktılarıdır; masaüstü ekran görüntüsü değildir. Editor PNG'si sabit footer son düzenlemesinden öncedir.

Sonraki öneri: **Faz 5 — gerçek launcher/quick settings ve medya yetenekleri**. Kullanıcı onayı beklenir.
