# CLAXE DESKTOP — Codex Devir Paketi

> **Uygulama durumu (22 Ekim 2026):** Faz 0–8 teslim özellikleri ve v0.8 yerel çalıştırılabilir paket hazır. Güncel kullanım için `README.md`, gerçek test sonuçları ve sınırlar için `docs/19_FAZ_7_8_RAPOR.md` okuyun. Aşağıdaki metin orijinal devir planıdır.

**Proje:** Claxe Desktop · **Hedef:** Windows 11 üzerinde son derece özelleştirilebilir, güvenli, modüler ve anime atmosferli masaüstü deneyimi (tam işletim sistemi/shell değişimi değildir).  
**Durum:** Gereksinimler kilitlendi; henüz uygulama geliştirilmedi.  
**Dil:** Kullanıcı iletişimleri Türkçe; kaynak kodda tutarlı İngilizce adlandırma.  
**Tarih:** 21 Ekim 2026.

## Codex'e nasıl verilir?

1. ZIP'i bilgisayarında boş bir proje dizinine çıkar (`ClaxeDesktop/`).
2. Codex'i bu dizinde başlat. **İlk oku:** `CODEX_BASLANGIC_PROMPTU.md` ve `AGENTS.md`.
3. Şu isteği ver: **"CODEX_BASLANGIC_PROMPTU.md ve AGENTS.md dosyalarını oku; Faz 0 ve Faz 1'i uygula, derle/test et ve rapor ver. Onayım olmadan sonraki faza geçme."**
4. `assets/references` altındaki görselleri **uygulanmış ekran görüntüsü değil, tasarım hedefi** olarak değerlendir; birebir çizim kopyalama gerekmez.

## Önce hangi dosya?

| Dosya | İçeriği |
|---|---|
| `CODEX_BASLANGIC_PROMPTU.md` | Codex'e doğrudan verilecek ana uygulama talimatı |
| `AGENTS.md` | Codex için sürekli mühendislik kuralları |
| `docs/01_VIZYON_KARARLAR.md` | 4 turda kesinleştirilen tüm tercihler |
| `docs/02_UX_GORSEL_SISTEM.md` | Görsel tasarım, davranış, tasarım tokenları, örnekler |
| `docs/03_MIMARI_MODULLER.md` | C#/.NET/WPF mimari plan, entegrasyon sınırları |
| `docs/04_AYARLAR_KATALOGU.md` | **20+ kategori / 200+ gerçek ayar hedefi**, varsayılanlar ve davranış |
| `config/settings.catalog.json` | Katalogun yapılandırılabilir/makinece okunabilir biçimi |
| `docs/05_ASAMALI_YOL_HARITASI.md` | MVP'den kapsamlı shell katmanına fazlar ve kabul ölçütleri |
| `docs/06_WIDGET_EKLENTI_TASARIMI.md` | Yerel HTML/CSS/JS widget sandbox ve izinleri |
| `docs/07_WALLPAPER_VE_FULLSCREEN.md` | Kendi motoru/Wallpaper Engine modları ve pause/restore |
| `docs/08_GUVENLIK_GERI_ALMA.md` | Safe-mode, görev çubuğu kurtarma, telif/izinler |
| `docs/09_TEST_KONTROL_LISTESI.md` | Test matrisi, hata/performans ölçümü |
| `docs/10_KAYNAKLAR_VE_REFERANSLAR.md` | Windows dokümanları ve referans resimleri |
| `docs/11_ACIK_KONULAR.md` | Doğrulanmamış API'ler, bilinçli ertelenen kararlar |
| `assets/references/*.png` | Özgün tasarım örnekleri, 2 monitör, ayarlar, widget editörü |
| `assets/moodboard/*.png` | Önceki konuşmada beğenilen anime atmosferli görsellerden seçkiler; yalnızca yerel referans |

## Beş temel ilke

1. **Kullanıcı seçimleri doğrudan kaynak gereksinimdir.** Windows 11 neredeyse tanınmayabilir, fakat Explorer kapatılmayacak ve güvenli dönüş korunacak.
2. **MVP küçük ama GERÇEK çalışır.** Sahte toggle, işlevsiz buton, çalışmayan panel, uydurma Windows API kabiliyeti yok.
3. **Ayarlar birinci sınıf ürün bileşenidir.** Ayar şeması, doğrulama, anında önizleme, geri al/varsayılan, profil katmanları.
4. **Görsel kalite ile verimlilik dengeli.** Tam ekran oyun/uygulama etkili monitörde animasyonları duraklat, gizli öğeler iş üretmesin.
5. **Lisans konusunu bahane yapıp durma, ama ihlal de etme.** Kullanıcının sağladığı kişisel görselleri yerel olarak destekle; dağıtımda hak durumunu ayır. Ücretsiz/özgür alternatif sun.

## Ne değildir?

- Linux/Wayland'a dönüşüm veya gerçek Hyprland compositor uygulaması değil.
- Windows sistem dosyalarını yamalayan bir mod değil.
- Yönetici izni isteyen sürücü/hook/enjeksiyon projesi değil.
- Bir oturumda bütün modüller tamamlanacak bir istek değil.

## İlk kabul

Faz 0 + Faz 1 sonunda normal Windows oturumu bozulmadan çalışan WPF uygulaması, bir floating panel/dock, saat ve sistem verisi widget'ı, ayarların kaydedilmesi, görsel referanslara benzeyen gece teması, çoklu monitör bilgisi ve güvenli çıkış gösterilmelidir.




