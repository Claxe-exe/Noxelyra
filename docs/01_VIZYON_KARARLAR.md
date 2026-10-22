# Ürün vizyonu ve dört turda alınan kararlar

## Kullanıcı niyeti

### 21 Ekim 2026 · Kullanıcı netleştirmesi

Claxe nihai sürümde Windows ile açılan ana masaüstü deneyimi olacak. İsteğe bağlı tam masaüstü modunda mevcut arka plan görünümünü kaldırıp/değiştirip bütün masaüstü yüzeyini, dock/panel/widget ve etkileşimleri kendi tasarımıyla yönetecek. Bu hedef yalnız küçük overlay widget'larla sınırlı değildir. Mevcut görsel dosyalarının silinmesi istenmiyor; görünümün sahipliği değişiyor. Explorer güvenli geri dönüş altyapısı olarak korunur. Bu netleştirme Faz 2 onayını içerir; wallpaper ve tam masaüstü yüzeyi için ayrı fazların fizibilite ve kabul koşulları geçerlidir.

Windows 11 neredeyse tanınmayacak kadar özelleştirilebilir görünmeli; fakat Windows üzerinde bağımsız ve güvenli uygulama olarak çalışmalı. KDE Plasma'nın esnekliği, Hyprland'ın floating bar düzenleri ve özgün anime atmosferi harmanlanmalı. Sadece kullanıcı için geliştirilecek, sonra GitHub'da paylaşılabilir. Claxe Browser paralel geliştirilir; ortak tasarım dili mümkün, çalışma zamanı bağımlılığı olmamalıdır.

## 1. Tur — Ürün kapsamı (8 yanıt)

1. **Düzey C:** kapsamlı desktop environment hissi; orijinal Windows olduğu neredeyse anlaşılmayacak.
2. **Stil D:** KDE + Hyprland + anime/cyberpunk; kullanıcı değiştirilebilir.
3. **Dock D:** paneller/dock tamamen taşınabilir ve özelleştirilebilir.
4. **Widget C:** geniş sistem widget kütüphanesi + kullanıcı widget'ları.
5. **Anime B:** wallpaper, widget, animasyon ve özel ikonlarda görünür anime estetiği; aşırı karakter kalabalığı zorunlu değil.
6. **Launcher D:** farklı başlat menüsü/launcher düzenleri seçilebilir.
7. **Pencere B:** ek pencere düzenleri + kısayollar, tam Hyprland otomatik compositor değil.
8. **Hedef B:** önce kişisel, gelecekte GitHub açık kaynak olasılığı.
9. **Ek:** Wallpaper Engine ve kişisel resim desteği, çift monitör, çok fazla özelleştirme.

## 2. Tur — Davranış (8 yanıt)

1. Görev çubuğu **kullanıcı tercihine göre gizlenebilir/normal**. Varsayılan değişiklik yapılmaz.
2. Widget yerleşimi hem **serbest** hem **ızgara/snap** modu.
3. Çift monitör kararı kullanıcıca devredildi: **ortak tema varsayılan, her monitörde bağımsız wallpaper/dock/widget düzeni altyapısı**.
4. Kendi animasyonlu wallpaper motoru + Wallpaper Engine ile birlikte kullanılabilirlik hedefi.
5. HTML/CSS/JS ile kişisel widget oluşturma.
6. Ses, medya, Wi-Fi, Bluetooth, takvim gibi bütünleşik sistem panelleri.
7. **Dengeli** kaynak tüketimi ile yüksek kalite animasyon.
8. Hazır preset + kullanıcı profili kaydetme, içe/dışa aktarma.
9. **Ek:** Tam ekran uygulama/oyun varken animasyonları duraklat, etkinlik durunca geri yükle.

## 3. Tur — Görsel kimlik (7 yanıt)

1. Varsayılan siyah/mor, gotik/atmosferik **Midnight Claxe** (Misaki Mei hissi; karakteri zorunlu kılmaz).
2. Tema seçimine göre glass/flat/neon widget ve panel stili.
3. Gösterişli animasyonlar destekli ama kolayca kapatılabilir.
4. Widget: masaüstünde sürekli veya kısayolla görünür gizli panel.
5. Hem hazır temalar hem gelişmiş canlı görsel düzenleyici.
6. Windows Ayarları gibi sade kategorili Settings; *içerik çok ayrıntılı*.
7. Küçük, isteğe bağlı, etkileşimli maskot.
8. **Ek:** ayar çeşitliliği kapsamlı, eksiksiz düşünülmeli; katalog kaynak gerçek olsun.

## 4. Tur — Mimari/fazlama (6 yanıt)

1. Entegrasyon B: güvenli yöntem varsayılan, deneysel özellikler izole ayrı ayarlar menüsünde.
2. Windows ile otomatik başlatmayı ilk kurulumda sor, sonra değiştirilebilir.
3. Wallpaper Engine ve kendi motoru **ayrı seçilebilir modlar**.
4. Profil geçişi manuel + uygulama / saat / monitör durumuna göre kurallarla otomatik.
5. MVP: önce altyapı, sonra ufak ama gerçekten çalışan parçalar.
6. Başlangıçta yalnızca güvenli yerel HTML/CSS/JS widget; topluluk dağıtım/marketplace gelecekte değerlendirilecek.
7. **Ek:** anime görsel/telif tartışmasının yapay engel olmasını istemiyor. Yerel referans/kişisel asset özgürlüğü sağla; dağıtım izinlerini doğru yönet.

## Bilinçli ürün kararı

- Monitör ayarları: ortak renk/tema varsayılanı + monitor başına override; monitor ayrılırsa düzen zarar görmez.
- Maskot: tasarım kesinleşmedi; önce placeholder/kapalı seçenek, sonra kullanıcı onayıyla orijinal maskot. Önceden oluşturulan chibi görsel **onaylı nihai maskot değildir**.
- Tam ekran optimizasyonu: aktif monitöre göre, pencere/oyun tanıma yanlış-pozitiflerini önleyecek kullanıcı istisnaları ile.
- Temanın anime esintisi başka eserlerin varlıklarının projeye otomatik gömülmesi anlamına gelmez.

## 22 Ekim 2026 · Windows ile bütünleşme

Kullanıcının “hâla pc ile tamamen iç içe hissettirmiyor bitirelim artık şu işi” talebiyle sonraki entegrasyon işine geçildi. Faz 5 launcher/sistem kontrolleri ve Faz 6'nın statik arka plan bölümü ortak günlük kullanım akışına bağlandı. Bu yönlendirme, önceki Faz 4 sonrası durma talebinin yerine bu entegrasyonları yapma yetkisi olarak yorumlandı. Tüm yol haritası tamamlandı iddiası yoktur; kapsam ve sınırlar `docs/17_WINDOWS_BUTUNLESME_RAPOR.md` içindedir.

## 22 Ekim 2026 · Windows görev çubuğu tercihi

Kullanıcı üst panel entegrasyonunun çalıştığını teyit edip sonraki aşamaya geçişi onayladı. “Alt taraftaki bar” açıklamasını **Windows'un kendi görev çubuğu** olarak netleştirdi. Claxe açıkken isteğe bağlı otomatik gizleme, eski tercihi koruma ve crash/exit kurtarması bu kapsamda uygulandı. Sonraki wallpaper adımı statik görsel listesi ve performans pause kurallarıyla ilerletildi; video renderer tamamlandı iddiası yoktur.

## 22 Ekim 2026 · İki aşama onayı

Kullanıcı v0.6 sonucunu başarılı buldu ve “bu sefer 2 aşama birden ilerle” dedi. Bu onayla **Faz 7 ve Faz 8** birlikte uygulandı: profiller/kurallar, pencere şablonları ve kapalı varsayılan yerel maskot. Kanonik maskot tasarımı hâlâ kullanıcı kararıdır; referans karakterler otomatik son varlık sayılmadı.

## 22 Ekim 2026 · Faz 9 ve dock çağırma

Kullanıcı Faz 9'a geçişi ve Alt+K ile dock'u masaüstüne dönmeden açık uygulamanın önüne çağırmayı istedi. Kısayol geçici pencere sırası davranışıdır; ekranın üst kenarına taşıma değildir. Bu açık tercih nedeniyle tek Alt+K kaydı genel kısayollar anahtarından bağımsız etkinleşir. Kurulum/kaldırma ve açık kabul sınırları docs/20_FAZ_9_RAPOR.md içindedir.

## 22 Ekim 2026 · Noxelyra

Kullanıcı uygulama adını Noxelyra olarak değiştirdi; Alt+K aralıklı çalışmasını düzeltme ve daha önce eksik belirtilen özellikleri uygulama onayı verdi. Eski kayıtlar korunur; bağımsız masaüstü alanı, video, resmi Wallpaper Engine adaptörü ve özgün görseller bu kapsamda eklendi. Teknik sınırlar docs/21_NOXELYRA_RAPOR.md içinde açıklanır.

## 22 Ekim 2026 · Masaüstü odağı

Kullanıcı kalan işleri onayladı; sonra oyun/tam ekran kontrolü istemediğini, katmanın masaüstünde kalıp gerektiğinde öne gelmesini istediğini netleştirdi. Kapalı varsayılan masaüstü takip modu, odağı çalmayan normal pencere sırası ve gerçek WE kabulü bu kapsamda uygulandı. Explorer shell replacement veya fiziksel VM/uyku testi yapıldığı iddia edilmez.
