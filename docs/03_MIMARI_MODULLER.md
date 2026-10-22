# Teknik mimari — Windows 11, .NET 10, WPF

## Teknoloji kararı

- **Çekirdek:** C# / .NET 10; `net10.0-windows10.0.22621.0` gibi Windows TFM (hedef derlemede kontrol).
- **UI:** WPF / MVVM, XAML ResourceDictionary / styles; DPI-aware app manifest.
- **Windows adaptörleri:** belgelenmiş Win32 Shell AppBar, Windows APIs, DWM, CoreAudio / WinRT uygun olduğunda; capability interface ile izolasyon.
- **Web widget motoru (ileri faz):** Microsoft WebView2, **her yerel widget için sınırlandırılmış izinler**; remote URL/HTML/CSS/JS otomatik native erişim yok.
- **Kalıcı veri:** `%LOCALAPPDATA%/ClaxeDesktop/` altında şema versiyonlu JSON; yazarken temp + atomik rename ve backup; uygulama verisi repo dışı.
- **Log:** yapılandırılabilir, kişisel dosya yollarını/ağ sırlarını varsayılan loglamayan, dönen yerel log dosyaları.

## Önerilen çözüm yapısı

```text
ClaxeDesktop.sln
src/
  ClaxeDesktop.App/             # WPF host, startup, dependency injection
  ClaxeDesktop.Core/            # settings, models, layout/profiles/rules
  ClaxeDesktop.Platform.Win/    # Win32/DWM/audio/monitor/taskbar adapters
  ClaxeDesktop.UI/              # resource dictionaries, controls, editors
  ClaxeDesktop.Widgets/         # native widget host + manifests
  ClaxeDesktop.WebWidgets/      # isolated WebView2 host (later phase)
  ClaxeDesktop.Wallpaper/       # wallpaper modes, own renderer (later)
  ClaxeDesktop.Automation/      # profile rules, app/time/monitor events
  ClaxeDesktop.Diagnostics/     # perf, crash recovery, logging
contracts/                       # public data structures, widget APIs
assets/                           # first-party themes/icons, licensed only
tests/
  ClaxeDesktop.Core.Tests/
  ClaxeDesktop.Integration.Tests/
```

Faz 1 için bu yapının tamamında boş projeler oluşturulmak zorunda değil. Erken gereksiz soyutlamadan kaçın; ancak katman sınırlarını koru.

## Temel arayüzler (taslak)

- `IMonitorService`: stable id, working bounds, scale, primary, topology changes.
- `IDockService`: pinned, running apps, activation, duplicate grouping.
- `IPanelService`: overlays, anchor, z-order, hide policy.
- `ISettingsStore`: versioned reads/writes, validation, rollback, schema migration.
- `IThemeService`: token resolution, preset/override, dynamic resources.
- `ILayoutService`: grid/free mode, coordinates normalized to per-monitor DIP.
- `IWindowObserver`: HWND tracking, foreground/maximized/fullscreen heuristics, privacy-aware.
- `IPerformancePolicy`: system/profile/fullscreen/reduced-motion -> effective animation state.
- `IWidgetHost` and `IWidgetPermissionBroker`: validated widget manifests/permissions.
- `IWallpaperService`: own/external/static mode; per-monitor pause/resume.
- `IProfileRulesEngine`: explicit priority order, debounce/hysteresis, manual override.
- `IRecoveryService`: pre-change snapshot, restore state, safe startup.

## Profil modeli ve öncelik

Önerilen çözüm: `defaults < theme < user profile < monitor overrides < temporary automation override < manual temporary override`. Kalıcı manuel ayar farklı kaydedilirse profil güncellenir; her override kaynak bilgisini gösterir.

Profil nesnesi: `id`, `name`, `themeId`, `layoutsPerMonitor`, `settingsOverrides`, `rules`, `version`, `assetReferences`. Import öncesinde schema ve asset validation, path traversal koruması. Eksik monitör: primary'ye uyarlama, veri kaybı olmadan kaydetme.

## Görev çubuğu/pencere katmanı

- İlk sürümde Windows taskbar'a dokunma; kendi panelini bağımsız window olarak göster.
- Güvenilir AppBar entegrasyonunu ve autohide durumunu sonraki fazda test et. Windows 11'in orijinal taskbar görünürlük davranışını her cihazda garantili programatik değiştirebildiğini **varsayma**.
- `explorer.exe` yaşam döngüsünü yönetme, shell replacement yapma.
- Normal kullanıcı yetkisiyle Win32 API; privileged app windows erişilemiyorsa tam yetki istemeden fallback.
- Modern UWP/WinUI uygulamalarını tanıma, icon retrieval, alt-tab eligibility, virtual desktop APIs mümkün olduğunca açıkça desteklenen yöntemlerle test edilmeli.
- İkinci monitör / çözünürlük değişimi / 125-150% DPI: her panelin mutlak/fiziksel ve DIP koordinatları dönüşümünü doğrula.

## Windows audio, Wi-Fi, Bluetooth

Ses göstergesi/mikser: uygun Windows ses API'leri; gerçek yetenekleri tespit et. Wi-Fi/Bluetooth: kullanılabilir sistem API varsa durum/kontrol; desteklenmiyorsa ilgili Windows ayar sayfasına açık eylem. Donanım olmayabilir. Sistem bildirimlerini oku-yönet yetkisi geniş değildir; Claxe'ın **kendi bildirimleri** ile OS bildirimi erişimini karıştırma.

## Performans / yaşam döngüsü

- UI thread boşta gereksiz frame üretmez. Widget timer'ları görünürlükle durur; ağa bağlı widget polling limiti var.
- Fullscreen detection için foreground HWND + monitor intersection, maximize vs borderless, dwell/debounce, process exception list. Shell AppBar full-screen bildirimi yardımcı sinyal.
- ViewModel event subscriptions `Dispose` / weak events ile temizlenir.
- Başlangıç süresini, private bytes, CPU%, frame stutter ve suspend/resume maliyetini ölç.
- Panel tıklaması odak çalmasın; geçiş sırasında Windows oyun performansını bozmasın.
