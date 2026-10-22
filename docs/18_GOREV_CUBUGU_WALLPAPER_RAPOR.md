# Windows görev çubuğu ve wallpaper geçişleri · v0.6.0

Tarih: **22 Ekim 2026**, Europe/Istanbul. Kullanıcı, üst panelin çalıştığını teyit etti; “alt bar” ile **Windows'un kendi görev çubuğunu** kastettiğini netleştirdi ve sonraki aşamayı onayladı.

## Yapıldı

### Windows görev çubuğu

Kontrol merkezinde **Claxe açıkken Windows görev çubuğunu otomatik gizle** seçeneği eklendi. Varsayılan kapalıdır. Belgelenmiş `SHAppBarMessage(ABM_GETSTATE / ABM_SETSTATE)` kullanılır; taskbar HWND'si bulunamazsa açık hata oluşur. İstenen değişiklikten sonra gerçek durum tekrar okunur; Windows kabul etmezse ayar başarılıymış gibi kaydedilmez. Diğer taskbar durum bitleri korunur.

Bu seçenek görev çubuğunu kalıcı olarak yok etmez: ekran kenarında Windows'un kendi otomatik gösterme davranışı sürer. Explorer öldürülmez, yeniden başlatılmaz; registry/shell replacement, injection veya ShowWindow ile zorla saklama kullanılmaz.

Değişiklik öncesinde önceki tercih atomik ve WriteThrough/Flush(true) ile diske yazılır. Ayrı bir yerel kurtarma yardımcısı hazır olmadan değişiklik yapılmaz. Temiz çıkışta eski tercih geri yüklenir. Ana süreç beklenmedik sonlanırsa yardımcı ebeveynin çıkışını gözler ve eski tercihi geri yüklemeyi dener. PID + başlangıç zamanı ve journal sahibi kontrolü, eski yardımcının yeni oturuma müdahalesini önler. Dosya kilidi iki süreçte eşzamanlı journal değişikliğini engeller. Windows'un otomatik gizleme tercihi zaten açıksa sahiplik alınmaz; çıkışta kullanıcının tercihi değiştirilmez.

Kullanıcı Windows ayarını dışarıdan değiştirmişse önceki değer zorla dayatılmaz. Geri yükleme başarısızsa journal saklanır ve sonraki başlangıçta güvenli modda da kurtarma denenir. Bu özelliğin açık olduğu ve Claxe'ın gerçekten bir değişikliği sahiplendiği oturumda **bir ek yerel yardımcı süreç** bulunur; ağ veya telemetri yapmaz. Yardımcı devre dışı bırakılmaz/kapatılmazsa ana süreçle birlikte görevini tamamlar.

### Sonraki aşama — wallpaper geçişleri

- Ekran başına **1–20 yerel görsel** seçilebilir. Tek dosya sabit resim; birden fazla dosya sırayla geçiş yapar.
- UI aralıkları 30/60/120/300 saniye, model sınırı 30–3600 saniye. Kaynaklar uygulamanın yönetilen PNG varlıklarına kopyalanır; kaynak dosyalar silinmez. İçe aktarma arka planda ve uygulama yaşam döngüsünün iptal token'ıyla yürür.
- İlgili monitörde tam ekran, oturum kilidi, pilde çalışma, pil tasarrufu veya güç bilgisinin bilinmemesi durumunda kendi resim geçişleri bekletilir. Diğer monitörler bağımsız değerlendirilir. Çalışma alanı ile monitör tam sınırları eşleştirilir.
- Kullanıcı başka uygulamadan arka planı değiştirirse ilgili ekranın Claxe döngüsü oturum boyunca durur. Yeniden liste seçmek kontrolü açıkça geri alır.
- Görsel listeleri ayarlarda atomik saklanır; geçici aktif kare ayar dosyasına her saniye yazılmaz. Wallpaper kurtarma journal'ı her gerçek görsel değişikliğinde güncellenir. Eksik monitörün tercihleri korunur.

## Kullanım

1. Eski Claxe'ı tepsiden kapat.
2. `Claxe Desktop.cmd` veya **`artifacts/publish-v0.6/ClaxeDesktop.App.exe`** aç.
3. Kontrol merkezindeki **Claxe açıkken Windows görev çubuğunu otomatik gizle** seçeneğini aç. Değişiklik anında kaydedilir; çıkışta eski tercih döner.
4. Görsel listesi için Windows arka planını önce **Resim** yap, kontrol merkezinde bu tercihi doğrula, ekranı/aralığı seçip birden fazla görsel ekle.

Eski `artifacts/publish` v0.5 paketi korunmuştur. Yeni sürüm ayrı klasördedir; açık eski exe üzerine yazılmaz. `.cmd` yeni klasörü açar. Yeni exe, DLL'leri ve WebView2 lisans dosyaları birlikte tutulmalıdır. Önceden oluşturulmuş Windows başlangıç kısayolu eski exe'yi gösteriyorsa, yeni sürümde Windows ile başlatmayı kapatıp yeniden açarak hedefi güncelle.

## Derleme ve doğrulama

`scripts/Verify-Taskbar.ps1` **12/12 kritik kontrol** ile geçti. Eski geniş takım tekrar çalıştırılmadı. Kontroller:

1. Riskli taskbar özelliğinin varsayılan kapalı olması.
2. Kalıcı journal ve hazır yardımcının değişiklikten önce gelmesi (fake adapter).
3. Yeni oturumun eski tercihi kurtarması (fake adapter).
4. Önceden açık kullanıcı otomatik gizleme tercihine sahiplik alınmaması (fake adapter).
5. Harici kullanıcı değişikliğini koruma (fake adapter).
6. Eski sahibin yeni oturumun journal'ını geri alamaması (fake adapter).
7. Windows benzeri bir adaptör değişikliği yok sayarsa açık doğrulama hatası (fake adapter).
8. Monitör bazlı fullscreen pause kararı.
9. Kilit/pil/bilinmeyen güç durumunda pause kararı.
10. Ayrı test ebeveyni temiz uygulama kapanışı olmadan sonlandığında gerçek yardımcı sürecin **dosya tabanlı sahte taskbar durumunu** kurtarması.
11. Bu Windows makinesinde gerçek görev çubuğunun otomatik gizlemeyi kabul edip durum bitini açması.
12. Gerçek Windows görev çubuğunun önceki tercihine dönmesi.

Native testin başlangıç tercihi **kapalıydı**; açılıp kapandığı gerçek API ile doğrulandı. Test ana süreç ve yardımcısında temiz çıkış kayıtları var. Explorer PID **4088**, başlangıç **08:40:11**; test **09:12** civarında çalıştı, dolayısıyla bu test sırasında Explorer yeniden başlatılmadı. Uygulama Explorer yaşam döngüsüne müdahale eden kod içermez.

Kanıt: `artifacts/phase6-taskbar-results.json`. Son küçük metin, güç bilgisindeki unknown eşlemesi ve resim importunu arka plana alma değişiklikleri sonrasında paket yeniden derlendi; 12'li takım tekrar çalıştırılmadı.

## Sınırlar ve tamamlanmayanlar

- Bu aşama **statik görsel döngüsüdür; video/GIF/scene wallpaper renderer değildir**. Güvenli temel olarak undocumented WorkerW/injection eklenmedi. Windows IDesktopWallpaper statik görselleri yönetir. Wallpaper Engine ile video için ayrı motor seçeneği değerlendirilebilir; bu sürüm Wallpaper Engine komutlarını otomatik göndermez ve onun durumunu bildiğini iddia etmez.
- Resim modu gerekir. Slayt gösterisi algılanırsa reddedilir; Spotlight veya harici motor yönetimi otomatik geri kurulmaz. Harici motor kullanırken Claxe'ın arka planını kapatmak gerekir.
- Gerçek 30+ saniyelik görsel geçişi, fiziksel pil değişimi, gerçek oyunda pause, sleep/RDP, Explorer yeniden başlatma ve karma DPI matrisi bu 12 kontrolde uçtan uca denenmedi. Pause karar fonksiyonu test edildi. Resim decode/encode kontrolü derlenmiştir; uzun süreli kullanıcı kabulü gerekir.
- Windows görev çubuğunun primary state biti doğrulandı. Her ikincil monitörün görsel hover davranışı, üçüncü taraf taskbar değiştirme araçları ve bütün Windows build'leri garanti edilmez.
- Yardımcı da çöker veya Windows API geri yüklemeyi reddederse anlık kurtarma garanti edilmez; journal sonraki açılışa kalır. Test fixture çökmesi gerçek Explorer çökmesiyle eşdeğer değildir.
- Varlık temizleme aracı, video motoru, profiller/kural otomasyonları, gelişmiş pencere şablonları ve installer henüz tamamlanmadı. Tam v1 iddiası yoktur. Faz 7'ye geçilmedi.

[ABM_SETSTATE resmi belgesi](https://learn.microsoft.com/en-us/windows/win32/shell/abm-setstate), [ABM_GETSTATE resmi belgesi](https://learn.microsoft.com/en-us/windows/win32/shell/abm-getstate), [Wallpaper Engine'in gerçek CLI kapsamı](https://help.wallpaperengine.io/en/functionality/cli.html).
