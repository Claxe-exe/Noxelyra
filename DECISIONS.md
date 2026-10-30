# Mühendislik kararları

## 2026-10-22 · Faz 3

1. Kullanıcı Faz 3'ü onayladı. Yedi sade kategori ve tüm çalışan ayarlar üzerinde arama; 267 girdinin tamamını sahte kontrol olarak sunmak yerine açık runtime destek listesi.
2. `config/settings.catalog.json` gömülü kaynak olarak Core'da okunur. Etiketler ve sayısal sınırlar katalogdan gelir; runtime sınırlarıyla kesiştirilir. Panel yüksekliği bu sürümde 42–80 DIP; dock kenarları serbest/üst/alt. İki panel uzantısı (`panels.widthDip`, `panels.clockOnLeft`) runtime kayıtlarıdır. Kullanıcı açıklamaları ürün dilinde düzenlendi; uygulama ayrıntıları varsayılan UI'da gösterilmez.
3. Üç preset ve iki vurgu override'ı; WPF [DynamicResource](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/systems/xaml-resources-and-code) kaynakları üzerinden tutarlı palet. Kodla kurulmuş widget/panel öğeleri de dinamik kaynak kullanır. Yüksek kontrast sistem renklerine öncelik verir.
4. Glass saydam renkli yüzey, Flat tam opak yüzey, Neon accent kenarı ve düşük güçlü statik glow. Belgelenmemiş backdrop API kullanılmadı. Blur kontrolü gösterilmez.
5. Düzenleme ayrı immutable Settings snapshot'larıyla yapılır. Hatalı ham renk girdisi modele girmez; Save devre dışı kalır. Son 50 geçerli değişiklik geri/ileri alınır. Sayfa veya arama sonuçları sıfırlanırken diğer modüller korunur.
6. Tema dosyası `ClaxeDesktop.Theme`, format sürümü 1: yalnız tema ID, materyal, vurgu, opaklık, köşe ve kenar. Bilinmeyen alanlar [JsonUnmappedMemberHandling.Disallow](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members) ile reddedilir; tip, duplicate alan, boyut ve sürüm doğrulanır. Asset/path/çalıştırılabilir kod içermez. İçe aktarma disk ayarını değiştirmeden önizlenir.
7. Uygulama ayar şeması 2'ye çıkarıldı. Sürüm 1 snapshot'ı eski değerleri ve yerleşimi koruyarak yeni varsayılanlarla yüklenir; sonraki atomik kayıtta 2 olur. JSON kökü nesne değilse kurtarma yolu kullanılır.
8. Temanın önizlemesi Kaydet / Geri al / pencere kapatma ile yönetilir. Otomatik süre aşımı, sınırsız özel palet paketi, tam profil import/export ve tema asset paketlemesi uygulanmadı; bunlar için sahte ayar gösterilmez.
9. Arama Türkçe büyük/küçük harf ve aksan duyarlılığını ele alır; kelime kökü çıkarımı veya anlamsal arama iddiası yoktur. Bir test beklentisi `opaklık`/`opaklığı` kök değişimini gerektiriyordu; desteklenen alt metin eşleştirmesine düzeltildi.
10. Testler gerçek tema kartı Click olaylarını, SearchBox binding'ini, dinamik yüzey rengini ve Save bloklamasını denetler. Dosya seçme pencerelerinin fiziksel tıklaması ayrı kabul testi yapılmış sayılmaz.

## 2026-10-21 · Faz 2

1. Kullanıcı sonraki aşamaya geçişi açıkça onayladı. Hedef, gerektiğinde bütün masaüstü yüzeyini yöneten Claxe; bu faz dock/pencere yönetimi temelidir. Tam masaüstü ve wallpaper fazları henüz uygulanmadı.
2. EnumWindows + görünürlük/owner/tool/noactivate/cloaked filtresi. Kendi pencerelerimiz hariç tutulur. Dosya yolu okunamazsa işlem ID'siyle ayrım yapılır; yetki yükseltilmez. ApplicationFrameHost pencereleri yanlışlıkla tek uygulamaya birleştirilmez; tam paket kimliği desteği ileri çalışmadır.
3. [SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow) sınırları korunur. Odak izni zorlanmaz; reddedildiğinde açık durum gösterilir. Enjekte edilmiş girdi veya AttachThreadInput kullanılmaz. Gerçek koordinat tıklamasıyla başarılı geçiş ayrıca doğrulandı.
4. [SHGetFileInfo](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shgetfileinfow) ayrı STA işçisinde; dönen HICON DestroyIcon ile serbest bırakılır, WPF kopyası Freeze edilir. Önbellek kullanılır. Yerel .lnk hedef/ikon konumu ağ yoluna çıkıyorsa arka plan okuması atlanır.
5. Bu fazda AppBar kaydı/çalışma alanı rezervasyonu yapılmaz. Üst/alt kenar yerleşimi mevcut Windows çalışma alanına uyar. Kullanıcının görev çubuğunu değiştiren bir eylem yoktur.
6. DockAutoHide ve DockOnTop varsayılan kapalı. AutoHide yalnız kenara bağlı dock'ta çalışır; 6 DIP tutamaç önde kalır. Güvenli modda gizleme/önde tutma kapalı uygulanır. Fullscreen algısı kendi monitöründe dock'u gizler, 300/450 ms dengeleme sonrası geri getirir; wallpaper/widget duraklatma motoru bu fazda yoktur.
7. Sağ tık kısayol eylemleri yalnız kısayol listesini commit eder; açık ayar önizlemesindeki diğer değerleri istemeden kaydetmez. Dock sürüklemek serbest yerleşime geçer.
8. Pencere taraması yaklaşık saniyede bir arka planda yapılır. Görsel liste değişmedikçe yeniden çizim istenmez; pointer/klavye/menü/sürükleme etkileşimleri sırasında liste yenileme ertelenir.
9. Fiziksel çoklu DPI/hotplug testine erişim sınırlı: test adaptörüyle kontrollü topoloji değişimi ayrıca simülasyon olarak raporlanır. On yardımcı pencere gerçek native HWND testidir; on farklı üçüncü taraf uygulama kapsamını temsil ettiği iddia edilmez.
10. `--inspect` yalnız izole CLAXE_DATA_DIR ile test pencerelerini görev çubuğunda erişilebilir yapar; normal uygulama davranışına ek görev çubuğu pencereleri eklemez. WPF testleri pencerelerin hazır bildirimini bekler; sabit başlangıç gecikmesine dayanmaz.

## 2026-10-21 · Faz 0–1

1. Çalışma alanı `clxwind`: Masaüstü'ndeki devir paketi buraya kopyalandı; orijinal paket ve ayrı Claxe Browser projeleri değiştirilmedi.
2. .NET SDK 10.0.401 / `net10.0-windows` / WPF. WinRT kullanılmadığından Windows SDK sürüm ekli TFM gerekmiyor. Microsoft'un [Desktop SDK](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props-desktop) yönergeleri izlendi.
3. Üç proje: Core, Platform.Win, App. İleri faz modülleri için boş projeler oluşturulmadı. Bağımlılık enjeksiyonu constructor üzerinden composition root'ta yapılır; DI paketi gerekmedi.
4. Yerel test runner: faz 1'de NuGet bağımlılığı eklemeden anlamlı testler. `dotnet test` özel MSBuild hedefinden gerçek executable çalıştırır; standart test-adapter/TRX desteği yoktur. Gerekirse sonraki fazda geçilebilir.
5. NotifyIcon için framework içindeki WinForms kullanılır. WPF giriş noktasında DPI manifesti korunur; WinForms giriş noktasına özgü **WFO0003** bu projede bastırılır. Başka uyarı bastırılmaz; uyarılar derlemeyi durdurur.
6. Varsayılan saydam koyu yüzey; gerçek arka plan blur yok. Glass efekti için belgelenmemiş DWM/WorkerW yolları kullanılmaz. WPF ResourceDictionary ile görünüm ayarlanır; planning JSON teması doğrudan tema import sistemi olarak sunulmaz.
7. `#FFFFFF1F` planlama tokenı RGBA/ARGB açısından belirsiz; WPF'te görünür, düşük kontrastlı mor sınır kullanıldı. Kayıt şeması planlama profilinden bağımsız sürümlendi. Ayrıntılı katalog ancak özellik uygulanınca bağlanacak.
8. Windows [EnumDisplayMonitors](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaymonitors) + GetMonitorInfo çalışma alanı; EnumDisplayDevices cihaz arabirimi ID, bulunamazsa ekran cihaz adı. Panel/dock konumu monitör çalışma alanına göre DIP olarak saklanır; Win32 konumlandırması fiziksel piksele çevrilir. Aynı ekranın farklı port/driver sonrası ID değişmezliği garanti edilmez.
9. Güvenli mod kayıtlı yerleşimleri korur. Uygulama shell durumunu değiştirmediğinden shell geri alma işlemi gerekmiyor; temiz çıkış marker'ı ve bozuk config yedeği uygulanır.
10. İlk kurulumda autostart sorulur. Kullanıcı seçerse WScript.Shell COM ile kendi Startup kısayolu oluşturulur. Registry, admin veya shell ayarı kullanılmaz. Başlangıç tercihi anında uygulanır ve bunu UI açıkça belirtir.
11. Pencereler topmost değildir; normal uygulamaların arkasında kalmaları MVP sınırıdır. Tam masaüstü katmanı/oyun politikası sonraki fazlara bırakıldı. Tepsi üzerinden ulaşılabilirlik korunur.
12. Otomatik şema validasyonu C# invariants ile yapılır. İleri şemalı config salt okunur korunur; bozuk dosya yedekten kurtarılır. Profil birleştirme/otomasyon/pause motoru faz 1'de bulunmadığından bunların testleri ileri fazda eklenecektir.
13. UI otomasyonunda pencere konumu değişti uyarıları gözlendi. Görsel doğrulama ayrıca gerçek WPF görsel ağacı render'larıyla, davranış doğrulaması uygulama içi opt-in entegrasyon harness'ıyla yapıldı; fiziksel sürükleme/klavye kontrolleri tam kullanıcı kabul testi olarak sayılmadı.

## Faz 4 kararları · 22 Ekim 2026
- Widget örnekleri şema 3 içinde saklanır; eski tek widget konumu korunarak taşınır. Tema önizlemesi ve widget taslağı ayrı kayıt işlemleridir.
- WebView2 SDK 1.0.4258.31 kullanılır. Saydam WPF penceresi için CompositionControl seçildi; uygulama ve host `net10.0-windows10.0.17763.0` hedefler. İlk gerçek denemede eksik Windows.SDK.NET hatası bu hedefle giderildi. .NET 10 WPF mimarisi değişmedi.
- İlk web host ağ ve cihaz izni vermez; manifestte istenirse reddeder. Marketplace/ZIP yok; yalnız yerel klasör paketi doğrulanıp kopyalanır. WebView2 izolasyonu işletim sistemi çapında ağ veya CPU kotası değildir.
- Medya widget gerçek medya oturumu desteğiyle Faz 5'e bırakıldı; boş Play/Pause kontrolü eklenmedi. Grid atölye içindeki ekran önizlemesidir, tüm Windows pencerelerinin üstüne çizilen masaüstü katmanı değildir.
- Kaynak tüketimi için sert CPU/GPU/RAM kotası iddia edilmez. Gizlenen web örneği dispose edilir; tekrar görünür olduğunda JS oturumu baştan başlar. Shelf görünürlüğü bu oturuma aittir; tekil görünürlük kayıtlıdır.
- Günlük doğrulama kullanıcı talebiyle yaklaşık 10–12 ilgili kontrole odaklanır. Önceki regresyonlar saklanır, her küçük değişiklikte tamamı tekrar çalıştırılmaz.
- SDK hedefleme kaynağı: https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/tutorials/wpf?view=aspnetcore-10.0

## v0.5 entegrasyon kararları · 22 Ekim 2026
- Kullanıcının masaüstüyle iç içe olma isteği için Windows AppBar ile gerçek çalışma alanı ayırma seçildi; taskbar/Explorer üzerinde gizli müdahale yok. Risk nedeniyle varsayılan kapalı ve açık kullanıcı tercihiyle anında kaydedilir.
- Launcher yerel Başlat menüsü kısayollarını indexler. Store/AUMID-only uygulamalar için AppsFolder erişimi açık fallback'tir; tam kapsama iddiası yoktur.
- Ses Core Audio, medya Windows.Media.Control ile gerçek aygıt/oturum yeteneğine dayanır. Wi-Fi/Bluetooth için Windows ayar sayfaları tercih edildi; sahte anahtar yok.
- Wallpaper motorunun ilk gerçek entegrasyonu statik IDesktopWallpaper'dır. Video için WorkerW veya injection eklenmedi. Resim modu kullanıcıca doğrulanır; Spotlight/Wallpaper Engine yönetimi otomatik restore ediliyormuş gibi sunulmaz.
- Wallpaper journal önce diske flush edilir; exit/sonraki-start kurtarması yalnız hâlâ bizim görselimiz aktifse uygulanır. Kaybolan monitörün kaydı tutulur. Watchdog yoktur, crash sonrası anlık restore iddia edilmez.
- Masaüstü ayarları, widget kayıtları ve tema önizlemesi birbirinden ayrı kaydedilir. Tema Undo/Redo kaydedilmiş entegrasyonu geri almaz.
- Kullanıcıya özel named pipe yalnız launcher/kontrol merkezi aktivasyonu taşır. Shell komutu, dosya yolu veya keyfi metot yoktur; boyut ve bağlantı süresi sınırlıdır.
- Günlük doğrulama 12 kritik kontrolle sınırlandı. Gerçek wallpaper yazma/ses değiştirme/medya oynatma test sırasında yapılmadı; fake recovery testleri açıkça etiketlendi.

## v0.6 görev çubuğu ve wallpaper kararları · 22 Ekim 2026
- Kullanıcı “alt bar” ile Windows'un kendi görev çubuğunu kastettiğini netleştirdi. Dock için yeni zorla gizleme davranışı eklenmedi.
- Belgelenmiş ABM_GETSTATE/ABM_SETSTATE ile opt-in otomatik gizleme, gerçek okuma ile doğrulama ve journal sahipliği seçildi. Explorer/registry/ShowWindow müdahalesi yoktur.
- Crash restore için yalnız bu özellik gerektiğinde bir yerel kurtarma yardımcısı kullanılır. Hazır sinyali olmadan mutasyon yok; PID+start time, owner journal ve süreçler arası dosya kilidi vardır. Ek süreç maliyeti nedeniyle özellik kapalı başlar.
- Wallpaper aşamasında desteklenen statik görsel döngüsü, per-monitor pause ve güç/kilit kuralları uygulandı. Video için unsupported WorkerW baseline seçilmedi; henüz çalışmayan video kontrolü gösterilmedi. Wallpaper Engine CLI dokümanı incelendi; pause/play gerçek olsa da önceki oynatma durumunu güvenilir okumadan otomatik sahiplik/restore iddia edilmedi.
- Yeni paket `artifacts/publish-v0.6` içindedir. Çalışan eski sürümü öldürmek veya dosyalarını zorla değiştirmek yerine yan yana yeni paket hazırlanır. Başlangıç kısayolunu yeni exe'de yeniden açmak gerekir.
- Günlük kapsam 12 kritik kontrolle sınırlı tutuldu. Gerçek taskbar state değişimi ve geri dönüşü doğrulandı; crash helper testi file-backed fake sistem durumu kullandı. Fiziksel oyun/pil/karma DPI ve uzun süreli wallpaper kabulü tamamlanmış sayılmaz.

## Noxelyra 0.11 kararları · 22 Ekim 2026

- Kullanıcı onayından sonra hedef masaüstünde kalan ve gerektiğinde öne çağrılan katman olarak netleşti. Oyun kabulü istenmiyor. Explorer shell replacement/registry müdahalesi yerine kapalı varsayılan foreground izleme + normal pencere sırası kullanıldı.
- Native hook OUTOFCONTEXT; mevcut Explorer desktop sınıfları yalnız okunur. Shell hiyerarşisi değiştirilmez. Otomatik gösterim odağı çalmaz; başlangıçta başka uygulama açıksa katman onun altına yerleştirilir. Esc/Alt+F4 geçici bastırma sağlar.
- Gerçek WE 2.8 çıktısı stdout redirection ile boş, kendi gizli konsoluyla dolu bulundu. Kullanıcı konsoluna attach/read yok; özel çocuk konsolu + bounded yerel sonuç kullanılır. Slash/canonical yol farkı da düzeltildi.
- WE mutasyonundan önce aynı yerel guardian hazır olmalı. Owner PID/start ve cross-process journal kilidi eklenerek crash/new-session yarışı önlenir. Gerçek fixture video / eski scene.pkg / izole crash üçlüsü makinedeki WE üzerinde doğrulandı.
- Kabul 12 kritik kontroldür; Shell MinimizeAll katman görünürlüğünü sınar. Başka uygulama foreground kaldığı için fiziksel Win+D/shell foreground uçtan uca testi sayılmaz. PowerMode olayları çağrıldı; fiziksel uyku/oyun/VM yapıldı denmez.

## Noxelyra 0.10 kararları · 22 Ekim 2026

- Kullanıcı Alt+K'nin aralıklı davranışını düzeltmeyi, adı Noxelyra yapmayı ve kalan özellikleri uygulamayı istedi. Bu onay masaüstü alanı, yerel video, WE adaptörü ve özgün başlangıç görsellerini kapsar.
- Alt+K toggle kaldırıldı; her çağrı idempotent öne getirmedir. Deactivation üzerine anlık gizleme yerine etkileşimsiz 15 saniye kullanılır. Kalıcı topmost ayarı veya konum değiştirilmez.
- Belgelenmiş API sınırında bağımsız normal WPF masaüstü alanı seçildi. Kullanıcının gerçek dosyalarını listeler ve ayrı konumlarını tutar; Explorer'ın ikonlarını gizlemez, registry/WorkerW parenting/injection/shell replacement yapmaz. Bunun tam Explorer masaüstü devri olduğu söylenmez.
- Yerel video WPF/Windows codec ile bu alan içinde gerçek decode edilir; Windows'un statik wallpaper API'sine video veriyormuş gibi yapılmaz. Wallpaper Engine ayrı kurulu üründür ve resmi CLI ile isteğe bağlı kullanılır. Önceki dosya restore edilebilir; önceki pause/play durumu okunamadığından otomatik global oynatma sahipliği kurulmaz.
- Ad değişikliği EXE/product/UI/kurulumda yapılır. Namespace/schema/profil kimlikleri, data klasörü ve named pipe/mutex geriye uyumluluk için kalır. Ayrı Claxe Browser'a dokunulmaz.
- Özgün kod çizimleri dağıtılabilir bir başlangıç varlık seti sağlar; referans anime veya kullanıcı masaüstü varlıkları pakete eklenmez. Kaynak/dağıtım lisansı henüz kullanıcı kararıdır.
- Doğrulama 12 kritik kontrol; gerçek MP4 encode/decode, native hotkey/message ve metadata/restore testleri. WE kurulu uçtan uca test ve fiziksel VM/oyun/uyku/DPI ayrı kabul işleridir.

## Faz 9 kararları · 22 Ekim 2026

- Kullanıcı Faz 9'u ve Alt+K kısayolunu açıkça istedi. Bu tek kısayol genel Ctrl+Alt kısayol ayarından bağımsız kaydedilir; kalıcı Windows ayarı veya klavye kancası kurulmaz. Çıkışta UnregisterHotKey çalışır. Kısayol çakışması kullanıcıya gösterilir; mevcut sahibinin kaydı değiştirilmez.
- “Üste çıksın” pencere sırası olarak uygulandı; dock ekranın üst kenarına taşınmaz. Kayıtlı konum değişmez. Öne çağırma geçicidir: Esc/ikinci Alt+K/başka pencereye geçiş veya etkileşimsiz 15 saniye sonunda normal topmost/auto-hide/fullscreen kuralları geri gelir. Aktif dock'ta klavye/fare etkileşimi süreyi uzatır. Güvenli masaüstü ve exclusive fullscreen aşılmaz.
- Yeni SDK/installer bağımlılığı yerine kullanıcı kapsamlı PowerShell kurulum/kaldırma seçildi. Sadece bilinen kurulum kökü, işaret dosyası, reparse reddi ve sahip olunan kısayollar üzerinden işlem yapılır. Kurulum uygulamayı/ağı/otomatik başlatmayı etkinleştirmez; kişisel veri kaldırmada korunur. Güncelleme şu an kaldırıp yeniden kurmadır.
- Kaynak lisansı sahibi tarafından seçilmedi. WebView2 özgün lisans/bildirimleri korunur; referans görselleri ve kişisel asset'ler dağıtılmaz. İmzalı setup.exe/Apps kaydı, temiz VM, karma DPI/uyku/oyun/uzun yük kabulü eksiktir; 0.9 bir Faz 9 adayıdır, v1 değildir.
- Doğrulama kurulan paketi kullanır: 7 WPF/dock/kurtarma + 5 kurulum/bütünlük/silme koruması kontrolü, toplam 12. Fullscreen sinyali test adapter'ından gelir; fiziksel tuşa basma yerine gerçek kayıt ve WM_HOTKEY işleme sınanır. Görev çubuğu gerçek mutasyon testi bu tur tekrar edilmedi.

## Faz 7–8 kararları · 22 Ekim 2026
- Kullanıcının iki aşama onayı Faz 7 profil/kurallar ve Faz 8 pencere yönetimi/opsiyonel maskot olarak uygulandı. Faz 9'a geçilmedi.
- Portatif profiller yalnız tema, panel/dock ve yerel widget düzenini taşır. Riskli Windows tercihleri ve web paketleri otomatik profille değiştirilmez. Not metni export'a dahildir; asset/uygulama yolları profil alanları olarak taşınmaz.
- Kural motoru AND koşulları, yüksek öncelik/listede ilk tie-break, 3 sn dwell, 30 sn cooldown ve kalıcı 30 dk manuel öncelik kullanır. Açık düzenleme pencerelerinde otomasyon bekler. Topoloji ilk kapsamda ekran sayısıdır.
- Pencere şablonları kullanıcı seçimine dayanır; HWND/PID/start-time kontrolü ve belgelenmiş SetWindowPos/SetWindowPlacement ile best-effort rollback vardır. Son Undo oturum içidir; app crash/exit sonrası masaüstü pencerelerini otomatik yeniden oynatma yapılmaz.
- Maskot yerel asset ve iki ifade görseli desteğiyle kapalı varsayılandır. Hazır telifli karakter dağıtımı veya GIF/video engine iddiası yoktur. Fullscreen/kilit görünürlüğü ve azaltılmış hareket/pil politikası gerçek uygulama davranışıdır; kabulte fullscreen sinyali simüle edilmiştir.
- Yeni paket `artifacts/publish-v0.8` içinde yan yana tutulur. 12 odaklı kontrol geçti; son UI kartları/koruma dokunuşlarında geniş takım tekrar çalıştırılmadı. Görsel QA ayrı izole capture ile yapıldı.

## Masaüstü alanı görünümü · 22 Ekim 2026

- Kullanıcı amacı Windows'un varsayılan arayüzünü Linux "rice" düzeyinde özelleştirmek olarak netleştirdi ve önce masaüstü alanını referans görsele yaklaştırmayı seçti.
- Referanstaki saat/sistem kartları masaüstü alanına kopyalanmadı; zaten var olan bağımsız widget pencereleri kullanılır ve alan simgeleri bu pencerelerin etrafından dizer. Böylece aynı bilgi iki kez gösterilmez.
- Yerleşim hesabı test edilebilir olsun diye Core'daki `DesktopIconFlow` içindedir. Ayrılmış alanlar yalnız ilk sayfada uygulanır; taşma kaydırılır, simge düşürülmez.
- Arka plan görseli video ile aynı yönetilen kopya modelini kullanır (yerel, 64 MB, reparse reddi). Windows'un kendi arka planı değiştirilmez.

## Üst bar ve tasarım skill'leri · 22 Ekim 2026

- Kullanıcı tasarım için `ui-ux-pro-max` ve `design-taste-frontend` skill'lerini kurdurdu ve kullanılmasını istedi. ui-ux-pro-max'ın `--design-system` önerisi landing page kalıbı ve altın vurgu döndürdü; ürüne uymadığı için kullanılmadı. WPF stack kuralları (resource dictionary, merkezi fırçalar) ve erişilebilirlik kuralları uygulandı.
- Taste skill mor/indigo vurguyu, cam/bulanıklığı ve yüzen hap navigasyonu "üretilmiş görünüm" sayar. Bunlar kullanıcının `docs/01_VIZYON_KARARLAR.md` seçimleridir (Midnight mor, cam, Hyprland barı) ve AGENTS.md gereği genel kuralların önüne geçer. Skill'den alınanlar: emoji yerine tek vektör simge seti, parlama olmaması (neon malzemesi hariç), süs amaçlı "·" etiketlerinin kaldırılması, sabit genişlikli rakamlar, en fazla birkaç yazı boyutu, vurgu renginin veri ve aktif durumla sınırlı kalması, her modülün gerçek veri taşıması.
- Çalışma alanları modülü eklenmedi: Windows sanal masaüstleri için kararlı, belgelenmiş genel API yok (`docs/11_ACIK_KONULAR.md` #7). Yerine odaktaki pencere gösterilir.
- Rice paletlerinin renk değerleri her projenin yayımladığı paletten eşlendi; yazılım bağımlılığı eklenmedi.
- Kullanıcı ayrıca Windows'un arka plan uygulamalarının RAM'i ve görüntüyü bozmasından şikâyetçi. Ölçüm: RAM'in çoğu kullanıcı uygulamalarında, Windows tarafı kabaca 1 GB. Hafifletme merkezi bir sonraki adımdır; yönetici izni gereken servis değişiklikleri ayrı onaya tabidir.

## Bilinen hata · 22 Ekim 2026

- "Panel için Windows çalışma alanında yer ayır" açıkken (v0.14, kullanıcının 1920×1080 ekranı) Windows en üstteki 0-48 px'i ayırıyor ama panel 48-96 px'e yerleşiyor; üst şerit boş kalıyor. Tek başına bir test AppBar'ı Windows'ta doğru konuma yerleşti, yani paneli kaydıran Noxelyra'nın kendi kodu. Kullanıcı ayarı kapattı ve incelemenin şimdilik bırakılmasını istedi. Yüzen bar etkilenmiyor.

## Yönetici izni sapması · 22 Ekim 2026

- Plan "yönetici izni isteyen proje değil" diyordu. Kullanıcı servis kapatmayı açıkça ve geri alınabilir olarak istedi. Sapma dar tutuldu: yalnız DiagTrack, SysMain ve WSearch; her işlemde UAC; ayrı kısa ömürlü yardımcı; ana uygulama normal yetkide. Defender, Windows Update ve kabuk servisleri izin listesinde yok.

## Gerçek cam isteğe bağlı · 22 Ekim 2026

- DWM Acrylic/Mica köşe yarıçapını 8 px'e sabitler ve Windows'un gri tonunu ekler; kullanıcı önceki boyalı görünümü tercih etti. Gerçek cam "acrylic" panel stili olarak kapalı varsayılanla kaldı.

## Setup.exe ve kendini güncelleme · 23 Ekim 2026

- Kullanıcı Sorune'deki gibi tek tıkla kurulan Setup.exe ve kendini güncelleyen uygulama istedi. Velopack (MIT) seçildi; makinede zaten kullanılan vpk sürümüyle eşleşir. Uygulama self-contained yayımlanır, hedef bilgisayarda .NET gerekmez.
- Güncelleme denetimi projenin "gizli ağ isteği yok" kuralına karşı görünür ve kapatılabilir bir ayardır; kullanıcının isteğiyle varsayılan açıktır. Yeni sürüm arka planda indirilir, kapanışta kurulur; "Güncelle ve yeniden başlat" normal kapanış (Windows durumunu geri yükleme) sonrasında uygular.
- Velopack kancaları özel `Program.Main` içinde, WPF ve tek-örnek mutex'inden önce çalışır. Kaldırma kancası çalışan Noxelyra'yı kapatır, görev çubuğunu/arka planı/WE'yi geri yükler ve başlangıç kısayolunu siler.

## 0.28-0.33 klavye ve kabuk hissi · 25 Ekim 2026

- Win tuşu, Alt+Tab ve döşeme için tek bir belgelenmiş düşük seviye klavye kancası (WH_KEYBOARD_LL) kullanılır; yalnız açık olan özelliklerin tuşlarını işler, tuş kaydetmez. Win veya Alt'ı tek başına yutmak Başlat'ı/menü çubuğunu açtığından araya atanmamış 0xE8 tuşu gönderilir (Windows'un da kullandığı "maske" yöntemi). Kendi ürettiğimiz tuşlar her zaman geçer.
- Win+B (tepsiye odaklan) belgelenmiş bir Windows kısayoludur; tepsi düğmesi bunu SendInput ile gönderir, görev çubuğu yalnız Noxelyra gizlediyse kısa süre gösterilir.
- Acil çıkış Ctrl+Shift+Ü: RegisterHotKey kendi iş parçacığında; arayüz donarsa 2,5 sn sonra geri yükleme o iş parçacığından yapılır. Ü tuşu klavye düzeninden çözülür, düzende Ü yoksa kaydedilen sanal tuş kullanılır.
- Döşeme yalnız yeniden boyutlanabilen, büyütülmemiş, en az 320×240 ve ekranı kaplamayan pencerelere uygulanır; konumlanamayan pencere "taşınamaz" olarak bırakılır. Varsayılan kapalı.
- Dinamik adada sürekli animasyon yoktur: bar katmanlı (AllowsTransparency) pencere olduğu için her kare tüm barı yeniden çizer. Yalnız durum değişiminde genişlik ve saydamlık geçişi oynar.
- Hava durumu widget'ı "gizli ağ isteği yok" kuralının bilinçli istisnasıdır: kullanıcı widget'ı ekleyip şehir yazmadan istek yapılmaz; yalnız `geocoding-api.open-meteo.com` ve `api.open-meteo.com` HTTPS, yönlendirme ve çerez yok, yanıt 256 KB ve 10 sn ile sınırlı. Veri Open-Meteo'nun CC BY 4.0 lisansıyla; widget'ta kaynak yazar.
- GPU/disk/ağ grafikleri belgelenmiş PDH sayaçlarından İngilizce yol adlarıyla okunur (Windows dilinden bağımsız). Sayaç yoksa sıfır değil "Yok" gösterilir.
- Rice dosyası paylaşım içindir: kişisel metin, web widget paketleri, kısayollar ve Windows değişiklikleri dışarıda kalır. Yerel yedek (`rice-backups`, son 5) kişisel metni korur. İçe aktarmada zip yalnız sabit adlı en fazla üç dosya içerebilir; klasör, fazladan dosya, bilinmeyen JSON alanı ve beyan edilenden büyük içerik reddedilir.
- Bellek: açılıştan 20 sn sonra bir kez SetProcessWorkingSetSizeEx(-1,-1) ile boşta kalan sayfalar Windows'a verilir; sayfalar gerektiğinde geri gelir, bu bir "RAM temizleyici" değildir.

## Ada kartı ve Alt+0 · 25 Ekim 2026

- Kullanıcı Shift+0 istedi; Türkçe klavyede Shift+0 "=" yazdığı için Alt+0 seçildi (soruldu).
- Alt+0 ile açılan kart WS_EX_NOACTIVATE ile odak almaz, böylece oyun alta inmez. Fare kilitli oyunlarda kullanılabilsin diye kart açıkken ←/→/↑/↓/Boşluk/Tab/Esc RegisterHotKey ile geçici olarak karta bağlanır; 12 sn boşta kalınca veya kart kapanınca bırakılır. Tuş kaydı ya da genel kanca yoktur.
- Monitör parlaklığı belgelenmiş Monitor Configuration API (dxva2, DDC/CI) ile; desteklenmiyorsa sahte kaydırıcı yerine açıklama gösterilir. Gece ışığı ve ses çıkış aygıtı değiştirme için belgelenmiş API olmadığından eklenmedi.
- "Uygulamayı kapat" iki aşamalıdır ve explorer, dwm, csrss, winlogon, svchost gibi kabuk/oturum süreçleri ile Noxelyra'yı reddeder; yönetici olarak çalışan uygulamalar kapatılamazsa bunu söyler.

## Yeni kurulumda önerilen ayarlar · 30 Ekim 2026

- Kullanıcı yeni bir PC'de kurunca Win tuşu ve Alt+Tab'ın Windows'ta kaldığını gördü ve bunların varsayılan olarak Noxelyra'nınki olmasını istedi. Kural "riskli özellik varsayılan kapalı" olduğu için ayar modelinin varsayılanları kapalı kaldı; bunun yerine ilk kurulum rehberi Win başlatıcısı, Alt+Tab, döşeme, görev çubuğu gizleme ve Windows ile başlamayı açıklamalı ve işaretli sunar. Kullanıcı Bitir'e basana kadar hiçbir şey değişmez; "Atla" hepsini kapalı bırakır; acil çıkış rehberin ilk sayfasında yazar.
