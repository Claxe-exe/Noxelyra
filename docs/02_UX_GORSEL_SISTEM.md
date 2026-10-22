# UI/UX ve görsel tasarım sistemi

## Ürün hissi

**Midnight Claxe:** görünür Windows 11 üst katmanını tamamen özgün gösterebilen, atmosferik, koyu, anime-esintili ama gündelik kullanımda okunabilir, rafine bir desktop. 144 Hz monitör kullanıcısı için hızlı etkileşim; 75 Hz ikinci monitör dahil mixed-refresh çalışmada animasyon düzgün. KDE'nin ayar zenginliği + Hyprland'ın modüler yerleşimi; hiçbirine piksel piksel kopya değil.

## Referans görseller

- `assets/references/01_midnight_desktop.png`: varsayılan tema, üst panel + alt dock + widget'lar.
- `assets/references/02_widget_editor.png`: canlı düzenleyici, grid, inspector.
- `assets/references/03_settings_center.png`: sade sol kategori listesi + zengin içerik ve arama.
- `assets/references/04_layout_presets.png`: aynı motorun üç farklı profil düzeni.
- `assets/references/05_dual_monitor.png`: ekran başına widget/wallpaper override.
- `assets/moodboard`: önceki konuşmada sevilen AI anime manzaralarının atmosfer örnekleri. UI örnekleri değildir; opsiyonel kullanıcı temaları.

## Önerilen başlangıç tokenları (değiştirilebilir)

| Token | Varsayılan | Not |
|---|---|---|
| canvas | `#0C0B13` | koyu mor-siyah |
| elevated | `#191625` | ana panel |
| surface | `#242034` | kart |
| purple | `#A884FF` | ana vurgu |
| plum | `#754CBA` | ikincil |
| cyan | `#7FDBF2` | durum/vurgu |
| text | `#F3EFFA` | birincil |
| muted | `#AEA6BF` | ikincil |
| success | `#6ED8AE` | durum |
| border | `#FFFFFF1F` | ince sınır |
| panelOpacity | 0.82 | cam preset |
| blurStrength | 18 | performansa göre etkili blur |
| cornerRadius | 16 | kart |
| dockIconSize | 48 | DIP; kullanıcı ayarlar |
| panelHeight | 42 | DIP |
| defaultGrid | 12 | DIP |

Bu sayılar **tasarım başlangıcı**, teknik kısıt değil; hepsi tema/config üstünden değişebilir. WCAG seviyesinde okunurluk hedefle, kontrast kontrolleri yap.

## Üç görünüm ailesi

**Glass:** blur + translucent panels + ince parlama (varsayılan).  
**Flat:** daha az GPU kullanımı, keskin/düz okunaklı paneller.  
**Neon:** vurgu glow, gradient borders ve responsive hover; animasyonları kapatabilme.  
Her ailede popup, dock, widget, Settings tutarlı olmalı; profiller arasında anında değiştirilebilir.

## Sahne hiyerarşisi

1. **Wallpaper layer**: kendi motoru veya harici Wallpaper Engine modu; sistem masaüstünü bozmayan entegrasyon.
2. **Desktop widgets**: masaüstü etkileşimini engellemeyen, sürükleme modunda etkileşimli; normal modda pointer geçirme opsiyonu.
3. **Dock + panels**: floating veya edge-attached; çoklu monitörde konumlar ayrı.
4. **Transient UI**: launcher, volume flyout, quick settings, bildirimler, command palette.
5. **Settings/Editor**: standart resizable pencereler, tüm kontroller erişilebilir.

## Etkileşim tasarımı

- Widget düzenleme modu belirgin `Düzenlemeyi bitir`, `Kaydet`, `Geri al` eylemleriyle açılıp kapatılır; istemeden widget taşınmaz.
- Taşırken snap to grid ve alignment guide; ALT ile geçici snap kapatma opsiyonu (kısayol daha sonra değişebilir).
- Sürüklenme sırasında ekran sınırları ve DPI'da güvenli clamp; yanlış monitor söküldüğünde widget'lar görünür alana taşınır.
- Dock ikon hover büyütme opsiyonel; imleç üzerindeyken popup kaybolmaz; animasyon eşiği/az hareket modu.
- Arka plan görselleri crop/fit/fill/tile ve odak noktasına göre konumlandırma; görseli bozmadan farklı ekran oranlarına uyum.
- Sağ tık menüleri klavye üzerinden erişilebilir; ara/ayar kısayol çakışmasında uyarı.
- Başlat/ara: menü modu (KDE-list, Launchpad-grid, Rofi-compact); son kullanılanlar izni opsiyonel.
- Animasyonlar: global aç/kapat, modül bazında süre/easing/güç; FPS sınırlı; fullscreen/pil/az hareket otomatik profil.
- Maskot: ilk sürümde kapalı, kullanıcı açınca köşede küçük; ekran üstü her zaman görüntü istemez, tam ekran algılanınca gizlenir; kendiliğinden ağ kullanmaz.

## Yalnızca görselde değil gerçek işlevde tutarlılık

- UI mockup'larında görülen yeni özellikler henüz uygulanmadıysa aktif buton olarak ortaya çıkmasın.
- Ayarlar menüsünde `yakında` etiketi ancak kullanıcı deneyimini bozmayacak belgelenmiş yol haritasında; ideal olarak bitmemiş sayfayı hiç göstermeme.
- Görsel referansların hiçbirinde Windows API sınırları yok sayılmamalı.
