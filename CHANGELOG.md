# Değişiklikler

## 0.36.3 · 2026-10-26 · Ada videosu: doğru pencere

- Tam ekran oyunda Noxelyra'nın pencere listesi duraklıyordu; ada yeni açılan mini oynatıcıyı görmüyor, küçültülmüş eski pencereye bakıyordu. Oynatıcı penceresi artık her seferinde taze listeden seçiliyor ve küçültülmemiş pencere (mini oynatıcı) öne alınıyor. Test edilmeden kuruldu.

## 0.36.1 · 2026-10-25 · Ada videosu düzeltmesi

- Video alanı bulunurken art arda kuyruktaki kareler okunuyordu; aralarında fark olmadığından hareket görünmüyor, yalnız kapak çıkıyordu. Artık her örnekte en yeni kare alınıyor, eşik düşürüldü ve video bulunamazsa 15 sn'de bir yeniden aranıyor. Test edilmeden kuruldu.

## 0.36.0 · 2026-10-25 · Kartın arkasında video, döşeme Snap'e karışmıyor

- Ada kartının 1. sayfasının arka planında çalan videonun kendisi oynar. Noxelyra oynatıcı penceresini (YouTube Music, YouTube, Spotify) yaklaşık 1,5 sn Windows Graphics Capture ile izleyip sürekli değişen alanı bulur ve yalnız o kısmı kartı dolduracak şekilde DWM ile gösterir. Görüntü kaydedilmez, hiçbir yere gönderilmez. Pencere boyutu değişince alan yeniden bulunur.
- Video yoksa (sabit kapaklı parça, duraklatılmış video) arka planda bulanık kapak görünür. Kapağa tıklamak arka plan videosunu açar/kapatır. 0.35'teki pencerenin tamamını gösteren önizleme kaldırıldı.
- Döşeme artık Windows Snap ile kavga etmiyor: elle taşınan, sürüklenip Snap düzenine bırakılan veya Win+oklarla yerleştirilen pencere döşemeden çıkar. Win+oklar ve Shift+Win+oklar yine Windows'un; döşemede odak Ctrl+Alt+Win+oklar, yer değiştirme Ctrl+Alt+Shift+Win+oklar.

Test edilmeden kuruldu.

## 0.35.0 · 2026-10-25 · Çalan uygulamanın sesi ve canlı önizleme

- Ada kartında şarkı kontrollerinin altında ses barı: yalnız çalan uygulamanın sesini değiştirir (Windows ses karıştırıcısındaki uygulama sesi). Tarayıcıda çalıyorsa Windows sekme başına ses vermediği için barın yanında "tüm Chrome" gibi yazar ve tarayıcının tamamını ayarlar.
- Kapağa tıklayınca müziği çalan pencerenin (tarayıcı, Spotify) canlı görüntüsü kartın altında açılır; kapaktaki küçük rozet görüntü olduğunu gösterir. Windows videonun kendisini vermediği için bu pencerenin tamamıdır; simge durumundaki pencerenin görüntüsü yoktur.
- Alt+Tab hızlandı: pencere listesi işlem yolunu artık hızlı ve önbellekli okuyor (`QueryFullProcessImageName`), kart önce yerleşip sonra görünüyor (eski yerinde parlamıyor), Windows geçişi reddederse görünmez bir tuşla izin alınıp yeniden deneniyor.

QA: 101/101 birim testi ve `Verify-V033.ps1` 13/13 geçti. Uygulama sesi ve canlı önizleme gerçek oynatıcıyla elle denenmedi. Alt+Tab düzeltmesi test edilmeden paketlendi.

## 0.34.0 · 2026-10-25 · Ada kartı ve Alt+0

- Dinamik adaya tıklayınca, bir şey çalmasa da iki sayfalık ada kartı açılır; iki sayfa aynı boyutta, sağdaki okla (veya Tab) geçilir.
  - **Sayfa 1:** çalan parça (kapak, ilerleme, önceki/oynat/sonraki, **±10 sn sarma**), hava durumu ve CPU/GPU/RAM. Bir şey çalmıyorsa büyük saat görünür. Kartın kendi şehri yoksa ilk hava durumu widget'ının şehri kullanılır.
  - **Sayfa 2:** ses düzeyi ve sessiz, mikrofon aç/kapa, monitör parlaklığı (DDC/CI; desteklemeyen monitörde açıkça yazar), ekran görüntüsü, donan uygulamayı kapatma (iki kez basınca; Windows'un kabuk süreçleri ve Noxelyra kapatılamaz) ve dock'taki ilk uygulamalar.
- **Alt+0** kartı nerede olursan ol ekranın üstüne getirir, tekrar basınca kaldırır. Odak oyunda kalır; kart açıkken ←/→ 10 sn sarar, ↑/↓ sesi değiştirir, Boşluk oynatır/duraklatır, Tab sayfa değiştirir, Esc kapatır. 12 sn dokunulmazsa kendiliğinden kapanır ve bu tuşlar oyuna geri döner. Kısayol Klavye penceresinden değiştirilebilir.
- Gerçek "özel tam ekran" oyunlarda hiçbir uygulama üste çıkamaz; kart "kenarlıksız pencere" modunda çalışır. Sarma, uygulama izin veriyorsa çalışır (YouTube, Spotify gibi); izin vermeyende düğmeler kapalı görünür.
- Sağ tık zamanlayıcı menüsünü açar; kartta da zamanlayıcı düğmesi var.

QA (bu bilgisayarda): 100/100 birim testi; `Verify-V033.ps1` 13/13 (kart iki sayfa, Alt+0 odak almadan açılır ve tuşları geri verir). Sarma ve "uygulamayı kapat" gerçek uygulamaları etkilediği için otomatik denenmedi; elle denenmeli.

## 0.33.0 · 2026-10-25 · Linux hissi: klavye, dinamik ada, döşeme ve rice

0.28-0.33 arası planlanan altı sürüm tek pakette.

**Klavye (0.28-0.29)**
- Acil çıkış: **Ctrl+Shift+Ü** her durumda Noxelyra'yı animasyonsuz kapatır ve Windows'u eski haline getirir. Arayüz donsa bile kendi iş parçacığında çalışır; 2,5 sn içinde kapanmazsa görev çubuğunu ve arka planı oradan geri yükleyip süreci bitirir. Kısayol Noxelyra masaüstünün sol alt köşesinde ve hızlı ayarlarda yazar.
- Klavye ve kısayollar penceresi: her Noxelyra kısayolu "Değiştir"e basıp yeni tuşlara basarak değiştirilir; çakışmalar ve Windows'a ayrılmış birleşimler reddedilir.
- Win tuşuna tek basış Noxelyra başlatıcısını açar (Win+E, Win+L gibi kısayollar çalışmaya devam eder). Başlatıcı iki stilde: ortada arama kartı veya tam ekran ızgara.
- Kendi Alt+Tab'ı: canlı DWM önizlemeli kartlar; Tab/oklar gezer, Alt'ı bırakmak geçer, Esc vazgeçer.

**Döşeme (0.30)**
- Hyprland "dwindle" otomatik döşeme: yeni pencereler uzun kenardan bölünerek yerleşir. Diyaloglar, küçük ve büyütülmüş pencereler, oyunlar serbest kalır. Win+T döşemeden çıkarır, Win+Q kapatır, Win+F tam ekran, Win+oklar odak, Shift+Win+oklar yer değiştirir. Boşluk 0-40 px.
- Win başlatıcı, Alt+Tab ve döşeme yeni kurulumlarda kapalı gelir.

**Panel (0.31)**
- Dinamik ada: bardaki saat adası çalan parçayı (kapak ve başlık), ses değişimini, Noxelyra bildirimlerini ve zamanlayıcıyı gösterir; genişliği yumuşakça değişir. Tıklayınca parça kartı (ilerleme, önceki/oynat/sonraki) veya zamanlayıcı menüsü (1-25 dk, kronometre) açılır. Ayarlar → Paneller'den kapatılabilir.
- Hızlı ayarlar barın sağ üstünden kayarak açılır: rahatsız etme, döşeme, Win başlatıcı, Alt+Tab, koyu Windows, görev çubuğu gizleme, dinamik ada, animasyonlar; ses, çalan parça, paletler, Wi-Fi/Bluetooth/Ekran/Ses ayarları ve bildirimler. Dışarı tıklayınca kapanır.
- Bildirim merkezi: güncelleme, zamanlayıcı ve kısayol çakışması bildirimleri (en fazla 100, rahatsız etme modu). Başka uygulamaların Windows bildirimleri okunamaz; yalnız Noxelyra'nınkiler.
- "Panel için çalışma alanı ayır" modunda bar artık ayrılan şeridin dışına itilemez; Windows şeridi aşağı kaydırırsa bir kez yeniden ayırır ve günlüğe yazar.

**Widget'lar (0.32)**
- Müzik kartı (kapak, ilerleme, kontroller), hava durumu, sistem grafiği (son 60 sn CPU, GPU, RAM, disk, ağ; Windows performans sayaçları) ve büyük saat. Widget'lar yalnız görünürken çalışır.
- Hava durumu yalnız sen bir şehir yazınca Open-Meteo'ya (iki sabit HTTPS adresi, anahtar ve konum izni yok) sorar, 30 dakikada bir ve yalnız widget görünürken yeniler.

**Rice (0.33)**
- Duvar kâğıdından renk: Noxelyra masaüstü görselinden veya Windows duvar kâğıdından okunaklılığı denetlenmiş bir palet (palet menüsü, hızlı ayarlar veya kontrol merkezi).
- Rice dışa/içe aktarma: tema, bar/dock görünümü ve widget düzeni tek `.noxrice` dosyasında; istersen masaüstü görseli ve açılış klibi de. Not metinleri, hava durumu şehri, kısayollar ve Windows ayarları dosyaya girmez. İçe aktarma dosyayı sıkı denetler, önceki görünümü yedekler; "Önceki görünüme dön" geri alır.
- Görev çubuğu tamamen gizliyken bardaki ˄ düğmesi görev çubuğunu kısa süre gösterip sistem tepsisine odaklanır (Win+B), sonra yeniden gizler.

**Hafiflik ve temizlik**
- Görev çubuğu koruyucusu artık WPF yüklemeden çalışır; açılıştan 20 sn sonra başlangıç belleği Windows'a geri verilir; eşzamanlı GC kapalı.
- Doğrulayıcılar `Verification/` klasörüne taşındı; koyu temaya uyan ince kaydırma çubuğu.

**Düzeltmeler**
- Windows'un hatırladığı ama bağlı olmayan bir ekran, duvar kâğıdı ekran listesini COM hatasıyla bozuyordu (kontrol merkezi → Masaüstü arka planı). Bağlı olmayan ekranlar artık atlanır.
- Ayar kataloğu testi eski sayıyı bekliyordu; gerçek sayıya (27) güncellendi.

QA (bu bilgisayarda): 99/99 birim testi; `Verify-V033.ps1` 11/11, `Verify-Noxelyra.ps1` 12/12, `Verify-Integration.ps1` 13/13 (ayrılan şeritte bar konumu dahil), `Verify-Taskbar.ps1` 12/12. Döşeme ve tepsi düğmesi gerçek pencereleri taşıyıp odakladığı için otomatik doğrulamaya alınmadı; elle denenmeli.

## 0.27.0 · 2026-10-23 · Tek tıkla kurulum ve kendini güncelleme

- `Noxelyra-win-Setup.exe` (~90 MB): yönetici izni olmadan kurar, masaüstü ve Başlat menüsü kısayolu, Uygulamalar listesinde kaldırma. .NET pakete gömülü. Ayrıca taşınabilir zip.
- Kurulu kopya açılıştan ~25 sn sonra GitHub'daki yeni sürümü denetler, indirir ve kapanışta kurar; kontrol merkezinde "Şimdi denetle", "Güncelle ve yeniden başlat" ve kapatma anahtarı.
- Kaldırırken Windows görev çubuğu, arka plan ve WE geri yüklenir, başlangıç kısayolu silinir; kişisel ayarlar korunur.
- README'ye kurulum kutusu. QA: bu bilgisayarda Setup.exe ile kurulum, kısayollar, Uygulamalar girdisi ve başlangıç kısayolunun yeni konuma taşınması doğrulandı.

## 0.26.1 · 2026-10-23 · İlk herkese açık sürüm

- GitHub'da MIT lisansıyla yayımlandı. Test çıktıları (bu makinenin masaüstü, açık uygulamaları ve medya başlıklarını içeren `artifacts/`) depodan ve geçmişten çıkarıldı; commit kimliği GitHub noreply adresine taşındı.
- Sürüm paketi CI derlemesiyle üretilir: PDB'lerde yerel klasör yolu yerine `/_/` ve GitHub SourceLink bulunur.
- SECURITY.md (gizli açık bildirme ve güvenlik modeli) ve vitrin README'si.

## 0.26.0 · 2026-10-22 · Kararma + TV, ayarlanabilir kararma, MP4 düzeltmesi

- Üçüncü geçiş "Kararma + TV açılışı": klip seçilen sürede karanlığa iner, kısa bir karanlık an, sonra TV açılışı.
- Kararma süresi kontrol merkezinden 0,3-6 sn (varsayılan 1,5 sn). "Kararma" artık önce siyaha iner, sonra siyah masaüstünden kalkar. TV açılışı yavaşladı: görüntü çizgiye çöker, çizgi ekran boyu uzar, ekran ~1,2 sn'de açılır.
- MP4 düzeltmesi: süre artık GUID adlı kopyadan ölçülür (adında # veya % olan dosyalar açılmıyordu). WPF'in açamadığı, 1080p'den büyük ya da 10 sn'den uzun videolar Windows'un kendi kodlayıcısıyla H.264 MP4'e (en fazla 1920×1080, ilk 10 sn) dönüştürülür. Windows'un çözemediği biçimde açık bir hata ve çözüm yolu gösterilir. Değiştirilen eski klip kopyası silinir.
- QA: "klip #1 %20.mp4" içe aktarıldı; gerçek dönüştürme 800×480 2 sn H.264 üretti.

## 0.25.0 · 2026-10-22 · Kendi açılış klibin

- Kontrol merkezi → Açılış: en fazla 10 saniyelik yerel MP4/M4V/WMV veya GIF seçilebilir. Süre kopyalamadan önce ölçülür; uzun klip, çözülemeyen video veya 200 MB üstü video / 40 MB üstü GIF reddedilir. Klip Noxelyra veri klasörüne kopyalanır.
- Klip bitince seçilen geçiş: "TV açılışı" (görüntü parlak bir çizgiye iner, ekran o çizgiden açılarak masaüstü görünür) veya "Kararma". Varsayılan açılış animasyonu klip yokken aynen oynar; klip açılamazsa ona dönülür.
- "Önizle" açılışı hemen oynatır, "Varsayılana dön" klibi kaldırır. GIF'ler kare gecikmeleri ve konumlarıyla kendi oynatıcısında oynar.
- QA: gerçek MP4 2,0 sn ölçüldü, 3 kareli GIF çözüldü, ikisi de içe aktarıldı ve sıfırlandı.

## 0.24.0 · 2026-10-22 · TV gibi açılış ve kapanış

- Açılış yeniden: ekranın ortasında bir ışık çizgisi, tema renginde dört nokta sırayla iki kez zıplar, merkezde birleşip hilale dönüşür, ardından NOXELYRA harf harf gelir ve üzerinden bir parıltı geçer. "masaüstün uyanıyor" yazısı kaldırıldı.
- Kapanışta aynısı tersten oynar (harfler söner, hilal noktalara ayrılır, ışık çizgisi kapanır); Noxelyra altta kaydedip kapanır (güncelleme/kaldırma kapanışında da oynar). Güvenli modda ve doğrulama koşularında oynamaz.

## 0.23.0 · 2026-10-22 · Açılış fragmanı ve pencere geçişleri

- Noxelyra açılırken yaklaşık 3 saniyelik giriş: yıldızlı gece, yükselen hilal ve hale, harf harf NOXELYRA. Tıklama/Esc/herhangi bir tuş geçer; 4,5 saniyeden uzun sürmez. "Animasyonlar → Açılış fragmanı" ile kapatılır; animasyonlar kapalıyken, azaltılmış harekette, güvenli modda ve doğrulamada oynamaz.
- Fragmandan sonra bar yukarıdan iner, dock aşağıdan yükselir, widget'lar sırayla belirir.
- Kontrol merkezi, ayarlar, hafifletme, başlatıcı ve profiller pencereleri yumuşakça açılır (belirme + hafif büyüme) ve daha hızlı kapanır. Uygulama kapanırken animasyon beklenmez.

## 0.22.0 · 2026-10-22 · Önceki görünüm geri, gerçek cam isteğe bağlı

- Kullanıcı 0.20 öncesi görünümü daha güzel buldu. Varsayılan yeniden boyalı cam (16 px köşe, tema yüzeyi). Animasyonlar (yükselerek belirme, dock dalgası) kaldı.
- Windows 11 Acrylic/Mica artık "Panel stili → Gerçek cam (deneysel)" seçeneği; Windows köşeleri 8 px'e zorlar ve kendi tonunu ekler. Seçim yeniden başlatınca uygulanır.

## 0.21.0 · 2026-10-22 · Cam düzeltmesi

- 0.20'de Windows Acrylic'in gri tonu tema rengini bastırıyor, dock ve widget'lar gri kutulara dönüyordu. Tema rengi yeniden baskın; cam yalnız arkayı yumuşatır. Mica pencerelerde de tema tonu koyulaştırıldı.

## 0.20.0 · 2026-10-22 · Tasarım v1.0 · adım 1: gerçek cam ve hareket

- Dock ve widget'lar Windows 11 Acrylic arka planı kullanır (belgelenmiş DWM öznitelikleri): gerçek bulanık cam, Windows'un yumuşak yuvarlak köşeleri ve tema renginde ince kenar. Kontrol merkezi, ayarlar, hafifletme ve başlatıcı Mica kullanır. Windows 11 22H2 öncesinde veya yüksek kontrastta eski boyalı yüzeyler kalır.
- Ortak hareket dili: yavaşlayan geçiş eğrisi, tek kapatma anahtarı (uygulama ayarı, Windows animasyon efektleri, yüksek kontrast).
- Yüzeyler açılırken hafifçe yükselip belirir. Dock'ta macOS benzeri dalga büyütme: imlecin altındaki simge en çok, komşuları azalarak büyür.
- Üst bar adacıkları henüz boyalı camdır; tek pencerede ayrı adacıklar için gerçek cam bir sonraki adımda.

## 0.19.0 · 2026-10-22 · Dock'a uygulama ekleme ve sayfalar

- Dock'a .exe/.lnk sürükleyip bırakarak, "+" menüsünden (başlatıcı veya dosya seçici) ya da başlatıcıda sağ tık "Dock'a sabitle" ile uygulama eklenir; başlatıcıdan kaldırılabilir.
- Dock 64 uygulamaya kadar tutar; ekrana sığmayanlar sayfalara bölünür (alttaki 1-2-3 düğmeleri veya fare tekerleği).

## 0.18.0 · 2026-10-22 · Masaüstü katmanları, dock ve görev çubukları

- Masaüstüne tıklamak artık bar, dock ve widget'ları arkaya atmıyor; alan etkinleşince Noxelyra parçaları üste alınır.
- Yüzen bar aynı ekrandaysa masaüstü başlığı ve simgeler barın altından başlar.
- Dock yeniden tasarlandı: kutusuz simgeler, yumuşak üzerine gelme vurgusu, çalışan uygulama için altta vurgu çizgisi, vektör tutamak ve ekle simgeleri.
- İsteğe bağlı "Görev çubuklarını tamamen gizle (tüm ekranlar)": otomatik gizlemenin 2. ekranda takılı kalmasına karşı Explorer görev çubuğu pencereleri gizlenir; Win tuşu Başlat'ı açmaya devam eder (ölçüldü). Çıkış, bakım ve çökme yardımcısı görev çubuklarını geri gösterir; yardımcı hazır değilse gizleme yapılmaz.

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





