# Test planı — gerçek sistem doğrulaması

## Her faz zorunlu

- [ ] `dotnet restore` / `dotnet build` gerçek çıktı kaydı.
- [ ] `dotnet test` + test sayısı/başarı/başarısızlık.
- [ ] Aç/kapat/yeniden başlat; config saklanıyor.
- [ ] Normal Windows masaüstü, görev çubuğu, Alt+Tab etkilenmiyor.
- [ ] Tema renkleri ve font kontrastı okunabilir.
- [ ] İşlevsiz kullanıcı kontrolleri yok (varsa gizle).
- [ ] Pencere boyutlandırma, keyboard, DPI.
- [ ] Exception logging yalnızca gerekli metadata ve gizli bilgiler temizlenerek.

## Çoklu ekran matrisi

| Senaryo | Kontrol |
|---|---|
| 144Hz + 75Hz | mikro takılma, gereksiz FPS loop |
| 100% + 125/150% DPI | doğru widget koordinatı, tıklama ve boyut |
| Ana ekran solda/sağda | negatif virtual-screen X koordinatları |
| İkinci ekran sök-tak | kayıp widget yok, default monitor fallback |
| Ekranı döndürme | layout safe area ve taşma |
| Sleep/RDP/lock | doğru suspension/recovery |
| Oyun 1. ekranda fullscreen | kendi animasyonları o monitörde durur |
| Oyun 2. ekranda fullscreen | monitor scope doğru |
| Borderless fullscreen + Alt+Tab | debounce, overlay gözükmesi |

## Sistem panelleri

- Ses: gerçek veriye bağlı, değişince cihazla senkron.
- Wi-Fi/Bluetooth: izin/donanım yoksa net unavailable state/fallback.
- Medya: desteklenen oturumları algılıyor, yoksa boş durum.
- Bildirimler: Claxe kendi bildirimi ile OS bildirimini ayırt ediyor.
- Global hotkey: mevcut Windows kısayoluyla çakışma yakalıyor.

## Widget güvenliği

- Yol dışına erişim / `../` / symlink / untrusted URL / cross-origin messages engellenmiş.
- WebView2 çökmesi diğer widget'ları çökertmiyor.
- Network izni yokken network disabled; izin iptali anında etki.
- Gizli widget polling üretmiyor.
- Kullanıcı manipülasyonu alanları sanitize ve validate.

## Güvenli çıkış

- Taskbar default state korunur.
- Force quit / crash sonrası restore.
- `--safe-mode` deneysel efekt olmadan açılıyor.
- Installer/uninstall, config recovery.

## Performans kayıtları

`startup_ms`, `idle_cpu_percent`, `private_working_set_mb`, `gpu_utilization` (desteklenirse), `widget_update_latency_ms`, `frame_time_p95_ms`, `resume_delay_ms`, `crash_count`, `fullscreen_pause_latency_ms`. Test makinesi/hardware/senaryo açıkça yazılmalı; ölçülmemiş sonuç uydurulmamalı.
