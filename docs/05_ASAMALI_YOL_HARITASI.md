# Aşamalı teslim planı ve kabul ölçütleri

**Kural:** Codex yalnızca Faz 0 + Faz 1'le başlar. Sonraki aşamaya geçiş için kullanıcı onayı gerekir. Her faz küçük çalışır entegrasyon + test + belge ile biter.

## Faz 0 — Planlama/ortam (P0)

- Windows sürümü / SDK / donanım yetenekleri kontrol; gerekli framework var mı doğrula.
- Git repo, solution, klasör yapısı, tema/ayar model mimarisi, temel test altyapısı, README.
- Desteklenen API ile belirsiz/deneysel API ayrım tablosu.
- **Kabul:** solution projeleri hazırlanmış, `dotnet restore/build/test` mevcut sistemde denenmiş, dürüst rapor.

## Faz 1 — Çalışan kabuk MVP (P0)

- Tek çalışan WPF host + **Midnight Claxe** teması; ayarlara girebilen gerçek menü.
- Floating dock/panel; uygulama kısayollarını ekleme/çıkarma ve gerçekten başlatma.
- Gerçek saat + tarih; CPU/RAM widget ancak gerçek Windows ölçümünden.
- Monitor discovery (en az iki monitör olduğunda iki monitörü listele); temel monitor ID map.
- Panel/dock konumu ve boyutunu değiştir, yerel JSON'a kaydet, yeniden açılınca korunur.
- Tam çıkış ve güvenli mod/restore iskeleti; Windows görev çubuğu **değiştirilmez**.
- **Kabul:** restart sonrası ayarlar kalır, pencere/değiştirilmeyen Explorer çalışır, kısa 2-monitor testi, temel klavye kullanılabilirliği.

## Faz 2 — Dock + panel bileşenlerinin olgunlaştırılması (P1)

- Açık Windows uygulamalarını güvenilir filtreleme/gruplama (Alt-Tab benzeri); pencereye geç.
- AppBar/edge docking uygun olduğu kadar; dock drag, auto-hide, ani kaybolma sorunu çözümü.
- Dock icon scale hover; panel öğelerini taşıma, sağ tık menüsü; profil duyarlı.
- **Kabul:** 10 uygulama açıkken doğru liste; farklı DPI ve bir monitor unplug test; görev çubuğunu kaybetmeden kapat.

## Faz 3 — Tema ve kapsamlı Settings altyapısı (P0/P1)

- `config/settings.catalog.json` temel alınarak category navigation, search, validation, reset/undo, theme presets.
- Midnight/Tempest/Sakura tema paletleri; glass/flat/neon; live preview + apply/cancel.
- Kategori görünürlüğü modül desteğine göre; etkisiz toggle yok.
- **Kabul:** seçilen değer hem görseli hem config'i değiştirir; import bozuk JSON güvenle reddedilir.

## Faz 4 — Widget Engine ve Editor (P1)

- Native time/date/system/media gibi widget seti; gizlenebilir widget shelf.
- Editor: serbest/ızgara, snap, alignment, çoklu seçim, undo/redo, groups/layers.
- Kişisel HTML/CSS/JS widget host; sandbox, permission manifest, origin/messaging validation.
- **Kabul:** 2 monitörde sürükleme/kalıcılık, web widget kötü amaçlı URL engeli, gizlenince polling durur.

## Faz 5 — Launcher, Quick Settings ve bildirimler (P1)

- Menü/ızgara/arama launcher profilleri; global kısayollar.
- Ses/media; Wi-Fi/Bluetooth gerçek API capability veya Windows Settings fallback.
- App kendi bildirimleri; üçüncü taraf sistem bildirimleri teknik/izin sınırına bağlı.
- **Kabul:** launcher gerçekten uygulama açar; ses gerçek değer gösterir; unavailable state açıkça anlaşılır.

## Faz 6 — Wallpaper modları (P1)

- Static image/video/animated renderer [desteklenen formatlarla], per-monitor.
- Wallpaper Engine'le **yan yana/ayrı mod**; sahipliği ve çizim çakışmaları yönet. Harici app'in API'sini icat etme.
- Fullscreen ve battery rules, per-monitor suspend/resume, forced performance preset.
- **Kabul:** fullscreen senaryolarında kendi render/polling durur, app minimize/screen lock/resume sağlam.

## Faz 7 — Profiller ve kural otomasyonları (P2)

- Presets, user profile save/load/export; uygulama, saat, monitor topology koşulları.
- Conflict resolver, priority, manual override, cooldown; profile switch atomic.
- **Kabul:** yanlış kuralla profil thrash yok; manuel override korunur; dışa aktarma portatif.

## Faz 8 — Window management + opsiyonel maskot (P2)

- Snap layout templates, uygulama yerleşim kısayolları; focus/failover handling.
- Maskot: opsiyonel, hafif, kapatılabilir; kullanıcı seçimli varlık importu/ifade animasyonu.
- Deneysel/unsupported UI açıkça test edilip ayrı menüde olur.
- **Kabul:** pencere sistem kontrolünü bozmadan düzenleme; maskot tam ekranda gizli.

## Faz 9 — Stabilizasyon ve GitHub hazırlığı (P2)

- Installer/uninstaller, uninstall taskbar restore, clean boot/recovery.
- Tüm kararsız monitor/dpi, heavy load, suspend, game fullscreen, video playback testleri.
- README, dependencies/license inventory, originals for distributable assets, accessibility audit.
- **Kabul:** temiz Windows 11 sanal makinesinde kur/çalıştır/kaldır ve shell geri yükleme testi.

## Sürüm kırılımı

- **MVP v0.1:** Faz 0–1 (gerçek ama küçük).
- **Beta v0.4:** Faz 0–4.
- **Daily-driver hedefi v0.7:** Faz 0–6.
- **v1 aday:** Faz 0–9 + QA.

Numaralar hedef niteliğindedir, vaat edilen takvim değildir. Ölçülebilir kabul koşulları sağlanmadan sürüm yükseltme yok.
