# Faz 7 + Faz 8 teslimi · v0.8.0

Tarih: **22 Ekim 2026**, Europe/Istanbul. Kullanıcı “bu sefer 2 aşama birden ilerle” diyerek Faz 7 ve Faz 8'i birlikte onayladı. Faz 9'a geçilmedi. v0.8.0 numarası bu iki kapsamın teslimini gösterir; önceki video wallpaper sınırlamalarının kapandığı veya tüm v1 kabul matrisinin tamamlandığı anlamına gelmez.

## Yapıldı — profiller ve otomasyon

- Üç başlangıç düzeni: **Midnight · Çalışma**, **Tempest · Odak**, **Sakura · Serbest**. Bunlar yalnız renk değiştirmez; widget türü/konumu, panel ölçüsü ve dock düzeni de değişir. Liste şematik ana ekran önizlemeleri gösterir; bu önizlemeler gerçek uygulama kontrolü veya canlı masaüstü görüntüsü değildir.
- Mevcut tema/panel/dock/yerel widget düzenini adlandırılmış profil olarak kaydetme, seçerek uygulama, silme, JSON içe/dışa aktarma. Dışa aktarma sabit uygulama/wallpaper yolları, web paketleri, taskbar, başlangıç veya maskot tercihleri taşımaz. **Not widget metinleri taşınır**; metindeki kişisel bilgi kendiliğinden temizlenmez.
- Portatif ekran yuvaları: ana ekran önce, diğerleri yatay sırayla. Eksik yuva ana ekrana uyarlanır; kayıtlı profil değişmez. Eşit çözünürlüklü ekranların port/kimlik değişimlerinde birebir fiziksel eşleme garantisi yoktur.
- Kural koşulları: öndeki uygulamanın `.exe` adı, yerel saat aralığı ve bağlı ekran sayısı. Koşullar AND ile birleşir. Gece yarısını aşan aralık desteklenir; eşit başlangıç/bitiş bütün gün anlamına gelir. Tam EDID/çözünürlük/topoloji desen eşleme bu sürümde yoktur.
- Öncelik 0–100; yüksek kazanır, eşit öncelikte listedeki ilk kural kazanır. 3 saniye kararlılık ve 30 saniye geçiş aralığı thrash'i sınırlar. Aynı etkin profil tekrar yazılmaz. Eşleşme yoksa mevcut görünüm korunur; otomatik eski profile dönüş yapılmaz.
- Manuel profil seçimi **30 dakika** öncelik alır; süre ayarlarda saklanır ve yeniden açılışta korunur. Otomasyona dön düğmesi bu beklemeyi kaldırır; otomasyon anahtarı ayrıca açık olmalıdır. Kural/görünüm/widget/kontrol pencereleri açıkken otomasyon bekler.
- Profil kitabı ayrı sürüm 1 dosyasıdır: en fazla 32 profil, 64 kural ve 1 MiB. Strict alan/boyut/ID doğrulaması, atomik geçici dosya + flush + sağlam yedek; gelecek sürüm dosyası salt okunur korunur. Bozuk dosya kurtarma durumu görünürdür.
- Profil uygulaması tek settings kaydıyla gerçekleşir, ardından aynı UI dispatcher turunda kendi pencereleri güncellenir. Hata halinde önceki kayıt/görünüm geri yüklenmeye çalışılır. Web widget örnekleri korunur; profil üzerinden yeni web izni veya riskli Windows ayarı açılmaz. 30 widget toplam sınırı aşılırsa uygulama hata verir.

## Yapıldı — pencere düzenleri ve yerel maskot

- Açık Windows pencerelerinden Ctrl/Shift ile seçim, hedef monitör, **ikili / üç sütun / 2×2** şablonları. Seçilenler liste sırasıyla kullanılabilir çalışma alanına, DPI'a göre boşluk bırakarak taşınır.
- HWND + PID + işlem başlangıç zamanı kontrol edilir. Kapalı, sabit boyutlu, Claxe'a ait veya erişilemeyen pencere taşınmaz. Win32 hatası UI'de görünür; UIPI bypass, yönetici izni yükseltme veya injection yoktur.
- Normal/büyütülmüş/küçültülmüş yerleşim GetWindowPlacement ile yakalanır. Hareket sonrası gerçek GetWindowRect sonucu doğrulanır. Uygulama minimum boyut vb. sebeple reddederse batch geri alınmaya çalışılır. SetWindowPlacement ile önceki show/normal konumu geri getirilir; farklı coordinate space'ler karıştırılmaz. Asenkron yerleştirme bayrakları kullanılır.
- **Son yerleşimden önceki konumlara dön** yalnız son başarılı düzen için oturum içi geri almadır. Pencere konumları kullanıcı eylemi olduğundan Claxe kapanırken otomatik geri çevrilmez. Kalıcı uygulama aç/yerleştir otomasyonu yoktur.
- İsteğe bağlı **Ctrl+Alt+L** düzenleyiciyi açar. Düzenleyicide **Alt+1/2/3** seçilen pencerelere şablon uygular. Global kısayol çakışması açık hata verir; varsayılan kapalı, çıkışta serbest bırakılır.
- Yerel maskot: kendi PNG/JPG görselini seçip uygulama verisine PNG kopyalama, boyut 80–240 DIP, tutamaktan sürükleme ve konum kaydı, sağ tıkla kapatma. İkinci bir ifade görseli seçilirse tıklamada iki saniye gösterilir. Genel animasyon açıkken hafif dikey hareket vardır; azaltılmış hareket, high contrast, pil ve gizlemede hareket durur.
- Maskot varsayılan kapalı; tam ekran/kilitte gizlenir, çıkınca geri gelir. Eksik/bozuk varlık açık hata verir; kalan masaüstü kullanılabilir. GIF/video veya yapay yüz ifadesi üretimi yoktur. Referans anime/chibi görselleri paket içine eklenmedi; kullanıcı varlığı için dağıtım hakkı iddia edilmez.

## Kullanım

Eski sürümü tepsiden kapatıp `Claxe Desktop.cmd` veya **`artifacts/publish-v0.8/ClaxeDesktop.App.exe`** aç. Kontrol merkezinde **Profiller ve kurallar**, **Pencere düzenleri**, **Yerel maskot** düğmeleri bulunur; tepsi menüsünden de açılır. Otomasyon ve kısayol/maskot özellikleri başlangıçta kapalıdır.

Profil uygulamadan önce ayrı görünüm ayarları ve widget düzenleyicisini kapat. Profil kaydetmek mevcut görünüm ve kaydedilmiş yerel widget düzenini alır. Not metinlerinin dışa aktarılacağını göz önünde tut. Maskot için ana görsel seçmeden sadece göster anahtarını açmak doğrulama hatası verir.

Paket .NET 10 Desktop Runtime ister; web widget için WebView2 Runtime ayrıca gerekir. Yeni NuGet bağımlılığı yoktur. v0.6 klasörü korunur; yeni paket ayrı klasördedir. Windows başlangıç kısayolu eski exe'yi gösteriyorsa, yeni sürümde Windows ile başlatmayı kapatıp tekrar açarak hedefi güncelle.

## Derleme / test / görsel doğrulama

`scripts/Verify-Advanced.ps1` **12/12 gerekli kontrol** ile başarılı oldu. Önceki geniş takımlar tekrar çalıştırılmadı.

1. Portatif profil birleştirmesi risk tercihlerine ve mevcut web örneklerine dokunmuyor.
2. Bilinmeyen, tekrarlanan ve gelecek sürüm importları reddediliyor.
3. Öncelik ve eşit öncelik sırası tek kazanan seçiyor.
4. Kararlılık, cooldown ve etkin profil kontrolü thrash'i önlüyor.
5. Manuel öncelik settings kaydı/yüklemesi sonrası korunuyor.
6. Gece yarısı, uygulama adı ve ekran sayısı koşulları birlikte çalışıyor.
7. Profil kitabı yedekten kurtuluyor, gelecek sürüm salt okunur kalıyor.
8. Gerçek WPF hostunda profil settings'e yazılıyor ve widget workspace güncelleniyor.
9. Negatif orijinli %150 DPI sanal monitörde hücreler güvenli alanda kalıyor.
10. **Açtığımız test uygulamasının iki gerçek HWND'si** şablona taşınıp orijinal sınırlarına dönüyor. Kullanıcının diğer pencereleri taşınmadı.
11. Üretilmiş basit fixture görselli gerçek maskot penceresi **simüle fullscreen sinyaliyle** gizleniyor, geri geliyor ve kapatılıyor.
12. İsteğe bağlı düzen kısayolu kaydedilip serbest bırakılıyor veya çakışma açıkça raporlanıyor; riskli özelliklerin varsayılanları kapalı.

Test helper process yalnız kendi oluşturduğumuz probe'dur; finally içinde yalnız bu process temizlenir. Normal ve fixture uygulaması temiz çıktı; session marker kaldırıldı. Explorer ve ayrı Claxe Browser yaşam döngüsüne müdahale edilmedi.

Kanıt: `artifacts/phase78-results.json`. Son görünüm kartları ve küçük doğrulama/hata yönetimi değişikliklerinden sonra paket yeniden derlendi; 12'li takım tekrar çalıştırılmadı. Güncel dört WPF PNG'si `--capture-advanced` ile ayrı izole veride alındı ve `assets/references/04_layout_presets.png` ile karşılaştırıldı. `artifacts/phase78-screenshots/Advanced-0.png` profil kartlarını, diğerleri kural/pencere/maskot sayfalarını gösterir. Bunlar WPF visual-tree render'larıdır. Önizleme modunda pencere kaynağı test adaptörüdür; boş liste kullanıcının PC'sinde hiç uygulama olmadığı iddiası değildir.

## Kalan sınırlar

- Otomasyon motoru karar fonksiyonları ve gerçek manuel profil geçişi test edildi. Gerçek oyun/foreground değişimiyle uzun süreli kural otomasyonu, fiziksel mixed DPI/sök-tak/RDP/sleep, erişilemeyen yüksek yetkili uygulamalar ve screen reader matrisi tamamlanmadı.
- Uygulamalar kendi minimum boyutlarını, fullscreen politikalarını veya güvenlik sınırlarını dayatabilir. Geri yükleme kapalı pencereye uygulanmaz; group rollback crash anında garanti edilmez. Oturum içi Undo kapanıştan sonra tutulmaz.
- Portatif profiller wallpaper/web paketi/maskot/uygulama dosyalarını taşımaz. Web örneği + profil widget toplamı limiti aşarsa hata verilir; sessizce veri silinmez.
- Maskot hazır karakter paketi veya GIF/sprite engine değildir. Yerel iki ifade görseli ve hafif hareket desteklenir. Nihai kanonik karakter tasarımı hâlâ seçilmedi.
- Önceki video wallpaper ve kurulum/stabilizasyon boşlukları devam eder. Faz 9 installer/uninstaller, temiz Windows VM ve geniş dayanıklılık kabulü için ayrıca onay gerekir.

[SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos), [WINDOWPLACEMENT coordinate spaces](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-windowplacement).
