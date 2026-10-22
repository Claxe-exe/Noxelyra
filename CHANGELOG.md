# Değişiklikler

## 0.17.0 · 2026-10-22 · Win+D Noxelyra masaüstünü gösterir

- İsteğe bağlı (kapalı varsayılan) "Win+D Noxelyra masaüstünü göstersin": ilk basış uygulamaları küçültüp Noxelyra masaüstünü açar, ikinci basış pencereleri geri getirir. Win+M zaten Noxelyra masaüstünü gösteriyordu (ölçüldü).
- Belgelenmiş düşük seviye klavye kancası ayrı iş parçacığında; yalnız Win+D tüketilir, Win tek başına, Win+Shift+D ve diğer kısayollar dokunulmadan geçer. Başka sürece DLL yüklenmez. Yönetici yetkili pencere öndeyken Windows'un kendi davranışı sürer.
- Ölçüm: Win+D sonrası pencereyi öne almak Windows masaüstünü geçemedi, bu yüzden kısayolun kendisi ele alındı. Fiziksel tuş testi kullanıcıda.

## 0.16.0 · 2026-10-22 · Yönetici izinli servis kapatma

- Hafifletme penceresinde Telemetri (DiagTrack), SysMain ve Windows Search dizinleme servisleri: gerçek durum, etkisi ve seçilenleri kapatma.
- Değişiklik her seferinde UAC soran kısa ömürlü yardımcı süreçle yapılır (`--services`); ana uygulama yönetici olarak çalışmaz. Yardımcı yalnız bu üç servisi ve `services.json` kaydını kabul eder, yetkisiz başlatılırsa hiçbir şey yazmadan çıkar.
- Her servisin ilk başlangıç türü ve çalışma durumu kaydedilir; "Servisleri eski haline getir" ikisini de geri yükler, başarısız olursa kayıt korunur.
- Doğrulama: `--desktop` 21/21, `--widgets` 12/12, uygulama kontrolleri 12/12; yetkisiz yardımcı çıkış 4 ve kayıt yok.

## 0.15.0 · 2026-10-22 · Hafifletme merkezi

- Yeni "Hafifletme" penceresi (bardaki CPU/RAM düğmesi, kontrol merkezi, bildirim alanı): en çok bellek kullanan 10 uygulama; Windows açılışında başlayan programların gerçek açık/kapalı durumu.
- Kullanıcı kapsamındaki başlangıç girdileri Görev Yöneticisi'nin kullandığı StartupApproved değerleriyle açılıp kapatılır; yönetici gerektirenler salt okunur, Görev Yöneticisi bağlantısı var.
- Telefon Bağlantısı / Cross Device arka plan izni ve Başlat aramasındaki web önerileri. Windows Update kapatılmaz; Ayarlar'daki duraklatma sayfası açılır.
- Her değişikliğin önceki hali kaydedilir; "Hepsini eski haline getir" geri yükler. Yönetici izni gerektiren servis değişiklikleri bu sürümde yok.
- Doğrulama: `--desktop` 18/18, `--widgets` 12/12, uygulama kontrolleri 12/12.

## 0.14.0 · 2026-10-22 · Windows görünümü

- Kontrol merkezinde "Windows görünümü": Windows koyu modu, saydamlık efektleri ve vurgu rengini aktif palete eşleme (deneysel). Yalnız kullanıcının kendi HKCU kişiselleştirme değerleri yazılır; Ayarlar uygulamasının gönderdiği bildirimle anında uygulanır.
- İlk değişiklikten önceki değerler kaydedilir; "eski haline getir" her değeri (yoksa silinerek) geri yükler. Kabuk/Winlogon gibi bilinmeyen anahtarlar reddedilir.
- Doğrulama: `--desktop` 14/14, `--widgets` 12/12.

## 0.13.0 · 2026-10-22 · Waybar tarzı üst bar ve rice paletleri

- Üst panel ayrı cam adacıklara bölündü (Hyprland/Waybar düzeni). Adacıkların arasındaki boşluklar tıklamayı geçirir.
  - Solda: uygulama başlatıcı ve odaktaki pencerenin simgesi ve başlığı.
  - Ortada: gün, tarih ve saat.
  - Sağda: medya (yalnız bir oturum varken; önceki/oynat/sonraki), ses (tıkla sustur, tekerlek veya ok tuşlarıyla ±%2), CPU/RAM, palet menüsü, kontrol merkezi ve Noxelyra menüsü.
- Simgeler Windows'un vektör simge fontundan (Segoe Fluent Icons; yoksa MDL2) gelir; karakter/emoji simge kaldırıldı. Saat ve yüzdeler sabit genişlikli rakamlarla çizilir.
- Dar yüzen barda modüller üst üste binmez; yer yetmezse önce tarih gizlenir.
- Yeni paletler: Catppuccin Mocha, Tokyo Night, Nord, Rosé Pine. Bardaki palet menüsünden tek tıkla uygulanıp kaydedilir; açık ayar penceresindeki diğer önizlemeler kaydedilmez.
- WCAG kontrast yardımcısı; bütün paletlerde metin ve soluk metin en az 4,5:1, vurgu en az 3:1 olarak test edilir.
- Doğrulama: `--desktop` 11/11, `--widgets` 12/12, Noxelyra uygulama kontrolleri 12/12. Bar önizlemeleri `artifacts/panel-previews`.

## 0.12.0 · 2026-10-22 · Masaüstü alanı görünümü

- Masaüstü alanı referans görünüme yaklaştırıldı: düğme sırası yerine "Masaüstü · N öğe" başlığı, cam arama/menü kapsülü ve tek "Görünüm" menüsü.
- Simgeler görünür widget, dock ve maskot pencerelerinin etrafından otomatik dizilir; elle taşınanlar yerinde kalır. Taşma ekranı kaplamak yerine aşağı kaydırılır. Yeni "Simgeleri otomatik diz" ve simge başına "Otomatik yerleşime al".
- Cam vurgulu simge karoları, etiket gölgesi, üst/alt okunabilirlik geçişi; sağ tıkta koyu, temaya uyan menüler ve ipuçları (uygulama geneli).
- İsteğe bağlı "Masaüstü simgelerini göster" (varsayılan açık): kapalıyken yalnız arka plan ve widget'lar görünür, arama yine dosya bulur.
- Masaüstü alanı için kendi yerel PNG/JPG/BMP arka planı; yönetilen klasöre kopyalanır, Windows arka planı değiştirilmez.
- Özgün gece çiziminin sağ kenarındaki siyah şerit düzeltildi.
- Doğrulama: yeni 8 masaüstü birim kontrolü (`--desktop`) 8/8, widget kontrolleri 12/12, Noxelyra uygulama kontrolleri 12/12. `publish-v0.12` EXE'si üzerinde de 12/12; `Noxelyra-0.12.0-win-x64.zip` paketlendi, başlangıç kısayolu yeni EXE'ye taşındı.

## 0.11.0 · 2026-10-22 · Masaüstünde kalan katman

- İsteğe bağlı masaüstü takibi: ayrı görev çubuğu girdisi olmadan masaüstünde kalma, native odağı çalmadan gösterme ve normal uygulamaların altına yerleşme.
- Esc/Alt+F4 ile geçici dönüş, sonraki masaüstü ziyaretiyle yeniden açılma; Alt+K tekrar çağırma korunur.
- WE 2.8'in pipe'a boş çıktı vermesi gerçek kurulumda teşhis edildi; kendi gizli konsolunu kullanan kısa ömürlü, sınırlandırılmış sorgu yardımcısı.
- WE v2 sahiplikli journal, süreçler arası kilit ve değişiklik öncesi hazır guardian; crash sonrası gerçek WE sahnesini geri yükleme.
- PowerModeChanged suspend/resume entegrasyonu. Son masaüstü/gerçek WE kabulü 12/12; fiziksel oyun turu son kullanıcı yönlendirmesiyle kapsam dışı.

## 0.10.0 · 2026-10-22 · Noxelyra

- Uygulama adı, EXE, ikon, panel ve kurulum Noxelyra oldu. Eski veriler/profil formatları/tek örnek kimliği korunur; sahip olunan açık startup yeni EXE'ye güncellenir.
- Alt+K artık her basışta çağırır: toggle ve geç deactivation iptali kaldırıldı, native z-order tekrar yükseltilir. Esc/uygulama seçimi/etkileşimsiz 15 saniye dönüşü korunur.
- Gerçek Desktop dosyaları ve shell ikonlarıyla bağımsız masaüstü alanı; arama, açma, F5/otomatik yenileme, sürükleme ve Alt+yön, bağımsız atomik yerleşim.
- Alan içinde sessiz yerel MP4/M4V/WMV döngü, codec hata görünümü ve gizleme/foreground/kilit/pil/fullscreen pause.
- Resmi WE CLI adaptörü; get/pause/play ve seçili videoyu uygulama, önceki dosyayı sahiplikli restore, harici değişikliği koruma ve kaldırıma/crash sonrası açılış kurtarması.
- Özgün gece/ay-dağ wallpaper, Nox ay kedisi iki ifade ve N+hilal uygulama ikonu. Referans karakterler dağıtılmaz.
- Son derleme 0 hata/0 uyarı; 12/12 odaklı kontrol geçti. Fiziksel VM/oyun/karma DPI ve kurulu WE kabulü bekler; Explorer içine gömülü video veya shell replacement iddiası yoktur.

## 0.9.0 · 2026-10-22

- Kullanıcının istediği global Alt+K: dock'u mevcut uygulamanın önüne geçici çağırma; Esc, ikinci basış, odak kaybında normal politikaya dönüş. Çakışma mesajı ve bildirim alanı menü alternatifi.
- Faz 9: SHA256 manifestli x64 ZIP, yönetici gerektirmeyen kullanıcı kurulum/kaldırma betikleri ve bağımlılık/lisans envanteri.
- Kaldırmada mevcut uygulamadan temiz çıkış istenir; sahip olunan görev çubuğu/arka plan durumu kurtarılmadan silme yapılmaz. Kişisel veriler korunur.
- Paketlenip kurulan uygulama üzerinde 12/12 odaklı kontrol geçti. Temiz Windows VM, fiziksel karma DPI/oyun/uyku ve uzun yük kabulü bekliyor; v1 iddiası yok.

## 0.8.0 · 2026-10-22

- Faz 7: üç başlangıç düzeni, portatif profil save/load/import/export, atomik profil kitabı ve yedek kurtarma.
- Uygulama/saat/ekran sayısı koşulları; öncelik, 3 sn kararlılık, 30 sn cooldown, kalıcı 30 dk manuel öncelik.
- Faz 8: gerçek pencere seçimi ve ikili/üçlü/2×2 şablonları; HWND/PID/start kontrolü, boyut doğrulaması ve son yerleşimi geri alma.
- İsteğe bağlı Ctrl+Alt+L; düzenleyicide Alt+1/2/3.
- Yerel PNG/JPG maskot importu, alternatif ifade, boyut/konum, hafif animasyon ve fullscreen/kilit/pil davranışı; varsayılan kapalı.
- Yeni profil önizleme kartları ve ayrı `publish-v0.8` paketi. 12/12 odaklı kontrol geçti; kapsam sınırları Faz 7–8 raporunda.

## 0.6.0 · 2026-10-22

- Windows görev çubuğunda isteğe bağlı otomatik gizleme; gerçek state doğrulaması ve eski tercihe dönüş.
- Atomik taskbar journal, owner/PID başlangıç kontrolü ve yalnız gerektiğinde ayrı yerel crash recovery yardımcısı.
- Ekran başına 1–20 görsellik yerel wallpaper listeleri; 30+ saniyelik geçiş ve fullscreen/kilit/pil/unknown power pause.
- Arka planda iptal edilebilir görsel importu; harici wallpaper değişince ilgili ekranın geçişini durdurma.
- Yan yana `publish-v0.6` paketi; 12 odaklı kontrolün tamamı başarılı. Video renderer ve fiziksel test sınırları raporda.

## 0.5.0 · 2026-10-22

- Windows AppBar ile gerçek çalışma alanına yerleşen panel ve bütünleşik modda alt kenar dock.
- Başlat menüsü uygulama araması, gerçek simgeler, liste/ızgara ve klavye erişimi.
- Kontrol merkezi: Core Audio ses/mute, Windows medya oturumu kontrolleri, sistem ayarı bağlantıları ve Claxe etkinliği.
- İsteğe bağlı global kısayollar, doğrudan başlangıç tercihi erişimi ve mevcut örneği açan yerel IPC.
- Monitör başına statik wallpaper, yönetilen görsel kopyası ve journal ile exit/sonraki-start kurtarması.
- Yalnız 12 kritik entegrasyon kontrolü; 12/12 başarılı. Sınırlar ve doğrulama ayrımı teslim raporunda.

## 0.4.0 · 2026-10-22

- Bağımsız saat, takvim, CPU/RAM ve not widget örnekleri.
- İki ekranlı widget atölyesi: taşıma/boyut, seçim, hizalama, grup/katman/kilit, çoğaltma, kaydet/iptal ve geçmiş.
- Yerel/offline WebView2 host; dar mesaj API'si, paket/manifest doğrulama, origin/CSP/izin sınırları. Gizlemede web kontrolü kapatılır.
- Şema 1/2 → 3 geçişi ve tema önizlemesinden ayrı widget kaydı.
- Sürümlü Windows SDK hedefi; WebView2 lisansı ve bildirimleri pakette.
- Kullanıcı talebiyle günlük widget kontrolleri 12'ye indirildi; eski regresyonlar korundu.

## 0.3.0 · 2026-10-22

- Faz 3 kullanıcı onayıyla uygulandı: yedi kategorili, aramalı ve katalog temelli ayar merkezi; 25 gerçek ayar satırı.
- Midnight Claxe / Tempest / Sakura paletleri; Glass / Flat / Neon yüzeyleri; vurgu renkleri, köşe ve kenar ayarları.
- Gerçek simge büyütme/süre, dock gizleme gecikmesi, 12/24 saat ve ızgara aralığı denetimleri.
- Canlı önizleme, değer doğrulama, sayfa/arama sonucu sıfırlama, 50 adımlı geri/ileri ve son kayda dönme.
- Dar, doğrulanmış JSON tema içe/dışa aktarma; yerleşim, uygulama yolu ve kullanıcı görselleri tema dosyasına girmez.
- Ayar şeması 2 ve sürüm 1'den kayıpsız yükleme; ileri sürüm koruması.
- 45 otomatik test ve normal/güvenli modda 52'şer zorunlu WPF kontrolü; üç tema ve Dock sayfası render'ları.
- Saat/CPU bildirimlerinin ayar satırlarını gereksiz yenilemesi önlendi. Yeni harici bağımlılık eklenmedi.
- Faz 4'e geçilmedi.

## 0.2.0 · 2026-10-21

- Kullanıcı Faz 2'yi onayladı; nihai bütün masaüstü deneyimi hedefi vizyona eklendi.
- Gerçek Windows pencerelerini filtreleme, uygulamaya göre gruplama ve pencereye geçiş; küçültülmüş pencereyi geri açma.
- Arka planda okunan, önbelleklenen gerçek Shell ikonları; yerel kısayol hedefi çözümleme. Uzak hedef/ikonlar arka planda okunmaz.
- Dock sağ tık menüleri: pencere seçimi, yeni örnek, sabitleme/kaldırma ve sıra değiştirme. Kısayol eylemleri kalıcıdır.
- Üst/alt çalışma alanı kenarı, isteğe bağlı otomatik gizleme ve önde tutma; menü/odak/sürükleme koruması; tam ekran sonrası geri görünme.
- Kapatılabilir simge hover animasyonu, panel saati sol/sağ yerleşimi, panel sağ tık menüsü.
- Monitör değişince boyut ve yerleşim yeniden uyarlanır; kayıtlı monitör kimliği korunur.
- 30 çekirdek/platform testi, on gerçek pencereyle WPF kontrolleri ve açıkça etiketlenmiş topoloji/fullscreen simülasyonları.
- Faz 3'e geçilmedi.

## 0.1.0 · 2026-10-21

- Faz 0: devir belgeleri, üç katmanlı .NET 10/WPF çözümü, test altyapısı, ortam ve karar kayıtları.
- Faz 1: Midnight Claxe panel/dock/saat ve CPU/RAM kartı; uygulama kısayolları; ayarlar, canlı önizleme, geri al/kaydet; monitör algılama ve konum kalıcılığı.
- Atomik JSON, yedekten kurtarma, gelecekteki şema koruması, safe-mode, tek süreç ve güvenli çıkış.
- Bildirim alanı erişimi, isteğe bağlı Windows başlangıcı, yüksek kontrast başlangıç paleti ve klavye odak göstergeleri.
- 22 çekirdek/platform testi ve ayrı WPF entegrasyon doğrulaması.
- İlk çalışma testinde bulunan ProgressBar iki yönlü binding ve pencere tema sorunları düzeltildi.
- Faz 2 ve sonrası uygulanmadı.





