# Faz 3 · Ayar merkezi ve tema altyapısı

Tarih: **22 Ekim 2026**, Europe/Istanbul. Sürüm: **0.3.0**. Kullanıcı Faz 3'ü onayladı; Faz 4'e başlanmadı.

## Yapıldı

- Yedi kategori: Görünüm, Dock, Paneller, Animasyonlar, Yerleşim, Uygulama kısayolları, Ekranlar ve oturum.
- Katalogdan etiket/metadata alan **25 çalışan ayar satırı**. Desteklenmeyen ayarlar kullanıcıya etkin kontrol olarak gösterilmiyor. Kaynak katalogdaki 23 düzenlenebilir girdi ve iki yerel panel uzantısı kullanılıyor; başlangıç tercihi ayrı oturum sayfasında.
- Türkçe etiket, açıklama, kategori ve anahtar üzerinden arama. Çok kelimeli sorgular bütün kelimeleri arar; sonuç yoksa sıfırlama kapatılır.
- Midnight Claxe / Tempest / Sakura paletleri. Panel, dock, saat kartı ve ayar merkezi birlikte güncellenir.
- Glass saydam yüzey, Flat opak yüzey, Neon vurgu kenarı ve hafif glow. İki vurgu rengi, opaklık, köşe ve kenar kalınlığı gerçek yüzeylere bağlandı.
- Dock gizleme gecikmesi, simge büyütmesi ve boyutu; animasyon aç/kapat/süresi; 12/24 saat ve saniye; ızgara aralığı ve hizalama çalışır.
- Bağımlı kontroller koşula göre kapatılır: örneğin Flat'te saydamlık, serbest dock'ta otomatik gizleme, animasyon kapalıyken hareket süresi.
- Geçersiz değer modele uygulanmaz. Hata görünürdür ve Kaydet engellenir. Geri al son kalıcı kayda döner; 50 adımlı geri/ileri geçmişi geçerli düzenlemeler arasında gezinir.
- Sayfa veya arama sonuçlarını sıfırlama; diğer kategoriler/yerleşim/kısayollar korunur. Kaydet veya iptal davranışı korunur.
- Dar JSON tema içe/dışa aktarma. İçe aktarma doğrulanır ve yalnız önizlenir. Dosya yalnız görsel tercihler içerir; uygulama yolları, monitor yerleşimi, kod veya varlıklar eklenmez.
- Ayar şeması 2; önceki şema 1 değerleri ve layout kaybolmadan yüklenir. Sonraki atomik kayıt sürüm 2 olur. İleri sürüm dosyası salt okunur korunur.
- Saat/CPU bildirimlerinin ayar editörlerini sürekli yenilemesi önlendi. Harici NuGet bağımlılığı eklenmedi.

## Derleme ve test

| Kontrol | Gerçek sonuç |
|---|---|
| Release build | **0 hata, 0 uyarı** |
| `dotnet test ClaxeDesktop.sln -c Release` | **45 / 45 başarılı** |
| Normal WPF entegrasyon testi | **52 / 52 zorunlu kontrol başarılı** |
| Güvenli mod WPF entegrasyon testi | **52 / 52 zorunlu kontrol başarılı** |
| `dotnet publish ... -c Release --no-restore -o artifacts/publish` | Başarılı, v0.3.0 |
| Görsel QA | Midnight, Tempest, Sakura, Dock sayfası ve gerçek yüzey render'ları incelendi |

WPF kontrolleri gerçek tema kartı Click olayını, SearchBox veri bağını, yanlış renk sonrasında Save bloklamasını, doğru renk önizlemesini, geri/ileri almayı, dinamik panel opaklığı/kenarı/köşesini, bozuk importun ayarları korumasını, import önizlemesinin diski değiştirmemesini ve tema kaydını doğruladı. Bütün kategori geçişlerinin ayarları değiştirmediği ayrıca denetlendi.

Çekirdek testleri: runtime katalog sınırları, Türkçe arama, üç paletin metin kontrastı, tam RGB renk doğrulama, tema JSON roundtrip, kişisel yolların export'a girmemesi, bilinmeyen/tekrarlı/bozuk/ileri sürüm/çok büyük tema reddi, kategori sıfırlama, geçmiş, ayarlanabilir grid/delay ve şema 1→2 geçişi. Önceki ayar/Windows testleri de çalıştı. Test yürütücüsü hâlâ bağımlılıksız executable'dır; xUnit/MSTest adaptörü değildir.

Her WPF raporunda ayrıca başarı sayısına dahil edilmeyen **1 otomatik foreground denemesi** bulunur. Son normal koşuda Windows isteği reddetti; güvenli mod koşusunda kabul etti. Bu platform davranışı bilgi amaçlıdır ve `informational` alanıyla ayrıdır. Kullanıcı tıklamasıyla geçiş Faz 2'de doğrulanmıştı; her arka plan isteğinin başarılı olacağı iddia edilmez.

İlk test turunda 44/45 sonucu vardı: arama testindeki `opaklık` / katalog etiketi `opaklığı` eşleştirmesi kelime kökü çıkarımı gerektiriyordu. Test desteklenen alt metin aramasına (`opak`) düzeltildi; son takım 45/45 geçti. İlk görsel QA'da seçim kutusu kayıt nesnesini yazıyordu; seçenek adı gösterilecek biçimde düzeltildi ve yeni render'lar kontrol edildi.

Kanıt dosyaları:

- `artifacts/phase3-normal-results.json`
- `artifacts/phase3-safe-results.json`
- `artifacts/phase3-screenshots/Theme-midnight-claxe.png`
- `artifacts/phase3-screenshots/Theme-tempest.png`
- `artifacts/phase3-screenshots/Theme-sakura.png`
- `artifacts/phase3-screenshots/Settings-Dock.png`

PNG'ler gerçek WPF görsel ağacından render edilir; mockup veya bütün masaüstü ekran görüntüsü değildir.

## Güvenli çıkış ve kapsam

- Normal/güvenli testler temiz çıkışla tamamlandı, `session.lock` kaldırıldı. Test ayarları yalnız `artifacts/runtime` dizininde kullanıldı.
- Windows shell, görev çubuğu ve sistem wallpaper'ı değiştirilmedi; Explorer son kontrolde aynı süreçte çalışıyordu.
- Tam masaüstü yüzeyi ve wallpaper sahipliği hedefi korunuyor. Bu faz bu deneyimin ayar ve tema temelidir; wallpaper motoru/launcher/widget editörü tamamlandı sayılmaz.

## Değişen dosyalar

- Core: `Themes.cs`, `SettingsCatalog.cs`, `SettingsHistory.cs`; model/sınır/şema ve mağaza yükleme güncellemeleri. Katalog gömülü kaynak oldu.
- App: yeni `ThemeService.cs`, `SettingsEditorViewModel.cs`; SettingsWindow yeniden kuruldu. Tema kaynakları, FloatingWindow/PanelWindow/WidgetWindow/DockWindow ve controller/viewmodel bağları güncellendi.
- Testler: 15 yeni çekirdek kontrolü, genişletilmiş WPF tema/arama/form kontrolleri ve görsel çıktılar.
- `config/settings.catalog.json` uygulama işaretleri, README, CHANGELOG, DECISIONS ve başlangıç belgesi güncellendi. Orijinal paket manifesti tarihsel devir kaydı olarak kalır.

## Eksikler ve teknik sınırlar

- Katalogdaki 267 ayarın tümü uygulanmadı. Henüz olmayan modüllerin kontrolleri gizli kalır.
- Tema import/export sabit üç preset ve sınırlı görsel tercihler içindir. Sınırsız özel palet dosyası, tema asset paketi, tam profil export ve otomatik önizleme süre aşımı uygulanmadı.
- Gerçek DWM blur yok. High contrast sistem renkleriyle ele alınıyor; fiziksel screen reader/high contrast ve dosya seçici mouse/klavye kabul testi tamamlanmadı.
- Farklı DPI ve monitör sökülmesi kontrolleri simülasyon olarak etiketli. Fiziksel %125/%150 + sök/tak, sleep/RDP ve oyun matrisi hâlâ gerekli.
- Ölçülmüş uygulama idle CPU/GPU/frame performans raporu henüz yok. Sistem kartındaki veriler uygulamanın kendi maliyet ölçümü değildir.

## Sonraki öneri

**Faz 4:** native widget motoru ve masaüstü düzenleyicisi; serbest/ızgara taşıma-boyutlandırma, yerleşim geçmişi ve monitör bazlı widget örnekleri. Kullanıcı onayı bekleniyor.
