# AYARLAR KATALOGU — Önerilen ürün ayarları

**Önemli:** Buradaki ayarlar **uygulanmış özellikler değil, geliştirme hedefi**. Codex bunu UI yapıp boş bırakmamalı: özelliği uygulayana kadar menüde etkisiz toggle göstermeyecek.

**Toplam:** 23 kategori, **267** tanımlı ayar/eylem. Ayrıntılı makine-okunur katalog: `config/settings.catalog.json`.

## Ayar mimarisi

- Sol kategori navigasyonu, arama, breadcrumb; sağ tarafta mantıksal gruplandırma. Aramada etiket + açıklama + setting key taranır.
- Her ayar: tür, default, min/max ya da izinli değer, açıklama, bağlı modül, etkilediği kaynak, kullanıcı profili kapsamı, rollback.
- Bazı girdiler **action**: reset/export/panic gibi; bunlar toggle değildir, buton olarak görünür.
- Varsayılanlar `safe by default`; sistem güvenliği öncelikli, özellikle autostart/taskbar/experimental.
- Ayar değişimi önce validasyon, gerekiyorsa anlık canlı önizleme; kullanıcı kaydet/iptal/geri alabilir. İşe yaramayan seçenek gizli ya da açıklamalı disabled; üretimde sahte seçenek yok.
- Ayarların çoğu global; tema/dock/widget/wallpaper/monitör/animasyon/perf ayarları per-profile ve per-monitor override alabilir; bu öncelik `docs/03_MIMARI_MODULLER.md`.
- `int(min..max)`, `enum(a,b)` ve `number(0..1)` katalog meta-tipidir; uygulama üretiminde schema doğrulayıcı buna göre yazılmalı.

## 01. Genel — 12 ayar

Genel işletim tercihleri, ilk kurulum ve kullanıcı kimliği.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `general.language` | Arayüz dili | `enum(tr-TR,en-US)` | `tr-TR` | Yerelleştirilmiş metinleri değiştirir |
| `general.startWithWindows` | Windows ile başlat | `bool` | `false` | İlk kurulumda ayrıca açık rıza sorulur |
| `general.minimizeToTray` | Kapatınca tepsiye küçült | `bool` | `true` | Tam çıkış menüsü ayrı görünür |
| `general.restoreLastSession` | Önceki profili geri yükle | `bool` | `true` | Crash sonrası safe-mode öncelikli |
| `general.showTrayIcon` | Bildirim alanı ikonu | `bool` | `true` | Acil çıkış yolu kalmalı |
| `general.openSettingsAtStartup` | Açılışta ayarları göster | `bool` | `false` | Kullanıcı tercihine bağlı |
| `general.launchMinimized` | Sessiz başlat | `bool` | `false` | Dock görünürlüğünü ayrı yönet |
| `general.checkForUpdates` | Güncellemeleri kontrol et | `bool` | `true` | Otomatik indirme başlamaz |
| `general.updateChannel` | Güncelleme kanalı | `enum(stable,beta)` | `stable` | Beta opt-in |
| `general.confirmExit` | Çıkarken onayla | `bool` | `false` | Tray'den güvenli çıkışı geciktirmesin |
| `general.firstRunWizardComplete` | Kurulum sihirbazı tamamlandı | `bool` | `false` | Kullanıcıdan auto-start ve izinler alınır |
| `general.defaultProfile` | Varsayılan profil | `id` | `null` | Profil yoksa Midnight Claxe |

## 02. Görünüm — 13 ayar

Tasarım tokenları ve global görünüm kontrolü.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `appearance.mode` | Renk modu | `enum(dark,light,system)` | `dark` | Varsayılan koyu atmosfer |
| `appearance.accentPrimary` | Ana vurgu rengi | `color` | `#A884FF` | Theme override |
| `appearance.accentSecondary` | İkincil vurgu | `color` | `#7FDBF2` | Nadir vurgu |
| `appearance.surfaceOpacity` | Panel opaklığı | `number(0.35..1)` | `0.82` | Kullanıcıya görünür önizleme |
| `appearance.blurRadius` | Bulanıklık | `int(0..30)` | `18` | Düşük güç profilinde sınırlanır |
| `appearance.cornerRadius` | Köşe yuvarlaklığı DIP | `int(0..32)` | `16` | Kart, panel, dock |
| `appearance.borderWidth` | Kenar çizgisi DIP | `number(0..4)` | `1` | Panel çevre çizgisi |
| `appearance.shadowStrength` | Gölge düzeyi | `int(0..100)` | `32` | Sıfır gölgeyi kapatır |
| `appearance.fontFamily` | Arayüz fontu | `font` | `Segoe UI` | Fallback varsayım |
| `appearance.baseFontSize` | Temel yazı DIP | `int(11..22)` | `13` | Erişilebilirlik |
| `appearance.uiScale` | Claxe UI ölçeği | `number(0.75..1.5)` | `1` | Windows DPI ile birlikte hesapla |
| `appearance.iconStyle` | İkon ailesi | `enum(outline,filled,duotone)` | `outline` | Tutarlı çizgi ağırlığı |
| `appearance.density` | Yoğunluk | `enum(compact,comfortable,spacious)` | `comfortable` | Padding/height tokenları |

## 03. Tema Motoru — 10 ayar

Preset, canlı önizleme ve tema paketleri.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `themes.activeTheme` | Etkin tema | `id` | `midnight-claxe` | İlk kurulum varsayılanı |
| `themes.panelStyle` | Panel materyali | `enum(glass,flat,neon)` | `glass` | Dinamik kaynak sözlüğü |
| `themes.syncAccentToWallpaper` | Wallpaper'dan renk çıkar | `bool` | `false` | Yerel palet analizi |
| `themes.allowPerMonitorAccent` | Monitör bazlı vurgu | `bool` | `false` | Monitor override açılır |
| `themes.autoSwitchWithSystem` | Sistem temasıyla eşleş | `bool` | `false` | Manuel profil önde |
| `themes.livePreview` | Canlı tema önizlemesi | `bool` | `true` | İptal ile geri al |
| `themes.previewTimeoutSec` | Önizleme süre aşımı | `int(5..60)` | `20` | Görüntü kaybolursa geri al |
| `themes.exportIncludeAssets` | Export'a görsel ekle | `bool` | `false` | Hak/format/boyut denetimi |
| `themes.importConfirm` | Tema import önizlemesi | `bool` | `true` | Önce asset izin kontrolü |
| `themes.animationsFollowTheme` | Tema animasyon stili | `bool` | `true` | Az hareket tercihi her zaman öncelikli |

## 04. Dock — 15 ayar

Dock konumlandırma, görünürlük ve davranış.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `dock.enabled` | Dock aktif | `bool` | `true` | Varsayılan başlangıç modülü |
| `dock.position` | Konum | `enum(bottom,top,left,right,floating)` | `bottom` | Ekran kenarı veya serbest |
| `dock.monitor` | Hedef ekran | `monitorId` | `primary` | Monitör fallback |
| `dock.autoHide` | Otomatik gizle | `bool` | `false` | Kenar tetikleyici |
| `dock.hideDelayMs` | Gizleme gecikmesi | `int(0..2500)` | `450` | İmleç dock üzerindeyken gizlenmez |
| `dock.iconSizeDip` | İkon boyutu | `int(24..96)` | `48` | DIP |
| `dock.iconSpacingDip` | İkon aralığı | `int(0..32)` | `10` | Sık/seyrek dizilim |
| `dock.magnification` | Hover büyütme | `number(1..2)` | `1.16` | Animasyon kapalıysa anlık |
| `dock.showRunningApps` | Açık uygulamaları göster | `bool` | `true` | Gerçek pencere filtrele |
| `dock.groupWindows` | Aynı uygulamayı grupla | `bool` | `true` | Click to cycle |
| `dock.showBadges` | Uygulama göstergesi | `bool` | `true` | API durumu doğru |
| `dock.launchOnSingleClick` | Tek tıkla çalıştır | `bool` | `true` | Sürükleme ile karıştırma |
| `dock.pinnedAppsLimit` | Sabit öğe limiti | `int(4..64)` | `24` | Kullanıcı özelleştirir |
| `dock.alwaysOnTop` | Her zaman önde | `bool` | `false` | Tam ekranda otomatik geri çek |
| `dock.backgroundOpacity` | Dock opaklık | `number(0..1)` | `0.78` | Tema override |

## 05. Paneller — 13 ayar

Üst/alt/yan floating paneller ve modüller.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `panels.topEnabled` | Üst panel | `bool` | `true` | Varsayılan saat ve sistem |
| `panels.bottomEnabled` | Alt panel | `bool` | `false` | Dock ile çakışmayı önle |
| `panels.leftEnabled` | Sol panel | `bool` | `false` | Opsiyonel |
| `panels.rightEnabled` | Sağ panel | `bool` | `false` | Opsiyonel |
| `panels.heightDip` | Panel yüksekliği | `int(28..90)` | `42` | DIP |
| `panels.marginDip` | Kenar boşluğu | `int(0..40)` | `16` | Floating hissi |
| `panels.elementSpacingDip` | Öğe aralığı | `int(0..32)` | `12` | Sol/orta/sağ alan |
| `panels.autoHide` | Otomatik gizle | `bool` | `false` | Modül bazında override |
| `panels.autoHideDelayMs` | Gizleme gecikmesi | `int(0..3000)` | `600` | Kapanma titremesi olmasın |
| `panels.clockFormat` | Saat formatı | `enum(24h,12h)` | `24h` | Yerel saat |
| `panels.showSystemTray` | Sistem tepsisi benzeri alan | `bool` | `true` | Gerçek OS tray kopyası garanti edilmez |
| `panels.showWorkspaces` | Çalışma alanı göstergesi | `bool` | `false` | Desteklenen API varsa |
| `panels.revealOnEdgeHover` | Kenarda aç | `bool` | `true` | Fullscreen'de bastırılabilir |

## 06. Widgetlar — 12 ayar

Widget yaşam döngüsü, veri ve görünürlük.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `widgets.enabled` | Widget Engine aktif | `bool` | `true` | Native host |
| `widgets.showOnDesktop` | Masaüstünde göster | `bool` | `true` | Gizli panel ile beraber olabilir |
| `widgets.showShelf` | Widget çekmecesi | `bool` | `true` | Hotkey/launcher üzerinden |
| `widgets.lockLayout` | Düzeni kilitle | `bool` | `false` | Yanlış taşıma engeli |
| `widgets.pauseWhenHidden` | Gizliyken güncellemeyi durdur | `bool` | `true` | CPU ağ kullanımını azaltır |
| `widgets.defaultRefreshSec` | Varsayılan yenileme | `int(1..3600)` | `30` | Widget manifesti aralığı daraltabilir |
| `widgets.maxActive` | Aktif widget üst sınırı | `int(1..100)` | `30` | Doğrulama |
| `widgets.allowDuplicates` | Aynı widget çoklu | `bool` | `true` | Per-instance storage |
| `widgets.transparentBackground` | Şeffaf arka plan | `bool` | `false` | Tema modu override |
| `widgets.allowInteractions` | Tıklanabilir widgetlar | `bool` | `true` | Pointer passthrough moduyla ilişkili |
| `widgets.defaultAnchor` | Varsayılan bölge | `enum(top-left,top-right,bottom-left,bottom-right,center)` | `top-right` | Monitor safe area |
| `widgets.autoSuspendHeavy` | Ağır widget'ları uyut | `bool` | `true` | Kullanıcı eşiği |

## 07. Widget Editor — 12 ayar

Düzenleyici komutları, hizalama ve versiyonlama.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `editor.positionMode` | Yerleşim sistemi | `enum(free,grid)` | `grid` | İki mod arasında kayıpsız geçiş |
| `editor.gridSizeDip` | Izgara boyutu | `int(4..64)` | `12` | DIP |
| `editor.snapToGrid` | Izgaraya yapış | `bool` | `true` | Serbest modda da açılabilir |
| `editor.snapToWidgets` | Diğer widgetlara hizala | `bool` | `true` | Kenar/merkez |
| `editor.showGuides` | Hizalama kılavuzları | `bool` | `true` | Düzenleme modunda |
| `editor.showGridOverlay` | Izgara çizgileri | `bool` | `true` | Sadece edit mode |
| `editor.undoDepth` | Geri al adımı | `int(10..200)` | `50` | Komut geçmişi |
| `editor.multiSelect` | Çoklu seçim | `bool` | `true` | Ctrl+click |
| `editor.grouping` | Grup oluştur | `bool` | `true` | Group/ungroup |
| `editor.keepInSafeArea` | Güvenli alanda tut | `bool` | `true` | Monitör bounds |
| `editor.showCoordinates` | Koordinatları göster | `bool` | `false` | Gelişmiş kullanıcı |
| `editor.editShortcut` | Düzenleme kısayolu | `hotkey` | `Ctrl+Alt+E` | Çakışma kontrolü |

## 08. Wallpaper — 16 ayar

Kullanıcı resimleri, video, çoklu ekran, harici mod.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `wallpaper.mode` | Wallpaper kaynağı | `enum(system,external,internal-static,internal-animated)` | `system` | Başlangıçta Windows korunur |
| `wallpaper.externalProvider` | Harici sağlayıcı | `enum(wallpaper-engine,none)` | `none` | Kullanıcı seçimi |
| `wallpaper.picturePath` | Görsel yolu | `path` | `null` | Yalnız yerel güvenli referans |
| `wallpaper.videoPath` | Video yolu | `path` | `null` | İleri fazda destek |
| `wallpaper.fitting` | Görüntü yerleşimi | `enum(fill,fit,stretch,center,tile)` | `fill` | Aspect korunmalı |
| `wallpaper.focalX` | Yatay odak | `number(0..1)` | `0.5` | Crop merkezi |
| `wallpaper.focalY` | Dikey odak | `number(0..1)` | `0.5` | Crop merkezi |
| `wallpaper.brightness` | Parlaklık çarpanı | `number(0..2)` | `1` | UI readability |
| `wallpaper.dimAmount` | Karartma | `number(0..1)` | `0.15` | Widget okunurluğu |
| `wallpaper.blurAmount` | Wallpaper blur | `int(0..30)` | `0` | GPU perf |
| `wallpaper.slideshowEnabled` | Slideshow | `bool` | `false` | Kullanıcı klasör kaynağı |
| `wallpaper.slideshowIntervalMin` | Slideshow aralığı | `int(1..1440)` | `30` | Varsayılan 30 dk |
| `wallpaper.slideshowShuffle` | Karıştır | `bool` | `false` | Tekrarlama |
| `wallpaper.videoMuted` | Video sesini kapat | `bool` | `true` | Default sessiz |
| `wallpaper.maxVideoFps` | Animasyon FPS sınırı | `enum(15,30,60)` | `30` | Görsel kalite/enerji |
| `wallpaper.perMonitorMode` | Ekrana özel wallpaper | `bool` | `true` | Ortak tema yine varsayılan |

## 09. Animasyonlar — 11 ayar

Global, modül bazında efekt ve azaltılmış hareket.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `animations.enabled` | Animasyon aktif | `bool` | `true` | Global switch |
| `animations.level` | Efekt yoğunluğu | `enum(off,subtle,balanced,showcase)` | `balanced` | Showcase görsel ağırlıklı |
| `animations.durationMs` | Geçiş süresi | `int(0..1200)` | `230` | Tüm bileşenlerde uygulanır |
| `animations.easing` | Easing eğrisi | `enum(linear,ease-out,spring)` | `ease-out` | Tutarlı motion |
| `animations.hoverEffects` | Hover efektleri | `bool` | `true` | Gerekirse azalt |
| `animations.glowEffects` | Neon parlama | `bool` | `false` | Neon tema açabilir |
| `animations.particles` | Parçacık efekti | `bool` | `false` | İleri faz, pahalı |
| `animations.dockBounce` | Dock bounce | `bool` | `false` | Gösterişli isteğe bağlı |
| `animations.launcherTransitions` | Launcher geçişleri | `bool` | `true` | İleri faz |
| `animations.widgetEntrance` | Widget giriş efekti | `bool` | `true` | Kullanıcı kapatabilir |
| `animations.reduceMotionOverride` | Az hareket önceliği | `bool` | `true` | OS erişilebilirlik ayarına uyar |

## 10. Performans — 15 ayar

Resource guard, fullscreen, güç tasarrufu.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `performance.mode` | Performans profili | `enum(eco,balanced,visual)` | `balanced` | Varsayılan dengeli |
| `performance.fullscreenPolicy` | Tam ekran modu | `enum(off,reduce,pause,stop)` | `pause` | Kendi motoru için |
| `performance.fullscreenScope` | Tam ekranda kapsama | `enum(active-monitor,all-monitors)` | `active-monitor` | 2 monitör |
| `performance.pauseWidgetsInFullscreen` | Widget güncellemesini durdur | `bool` | `true` | Etkilenen monitör |
| `performance.pauseWallpaperInFullscreen` | Wallpaper animasyonu durdur | `bool` | `true` | Sadece kendi renderer |
| `performance.maxUiFps` | UI animasyon üst sınırı | `enum(30,60,90,120)` | `60` | Monitör ihtiyacına göre |
| `performance.idleUiFps` | Boşta hedef | `enum(0,15,30)` | `0` | Event-driven tercih |
| `performance.onBatteryProfile` | Pilde otomatik profil | `enum(none,eco)` | `eco` | Laptop cihazlara uygun |
| `performance.fullscreenDebounceMs` | Tam ekran bekleme | `int(100..3000)` | `450` | False positives |
| `performance.fullscreenRecoveryMs` | Dönüş bekleme | `int(100..5000)` | `650` | Thrash önleme |
| `performance.maxImageCacheMB` | Görsel önbellek limiti | `int(32..1024)` | `256` | LRU cache |
| `performance.widgetBudgetMs` | Widget yenileme bütçesi | `int(4..100)` | `20` | Aşırı widget tespit |
| `performance.logPerfDiagnostics` | Performans günlükleri | `bool` | `false` | Yerel logging ve izin |
| `performance.suspendOnLock` | Kilit ekranında uyut | `bool` | `true` | Boşta render yok |
| `performance.suspendOnRdp` | Uzak masaüstünde uyut | `bool` | `true` | RDP kaynak kullanımı |

## 11. Başlatıcı — 11 ayar

Başlat menüsü, arama, komut paleti.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `launcher.enabled` | Başlatıcı aktif | `bool` | `true` | Özel kısayol |
| `launcher.layout` | Menü stili | `enum(list,grid,compact)` | `compact` | Rofi-like varsayılan |
| `launcher.position` | Açılma konumu | `enum(center,top-center,cursor,dock)` | `center` | Multi monitor |
| `launcher.widthDip` | Panel genişliği | `int(360..1200)` | `650` | DPI uyumlu |
| `launcher.showRecentApps` | Son kullanılanları göster | `bool` | `false` | Mahremiyet opt-in |
| `launcher.showPinnedApps` | Sabitlenenler | `bool` | `true` | Gerçek launcher |
| `launcher.showSystemCommands` | Sistem komutları | `bool` | `true` | Güvenli eylemler |
| `launcher.searchWebEnabled` | Web'de ara | `bool` | `false` | İnternet opt-in |
| `launcher.searchFilesEnabled` | Dosyalarda ara | `bool` | `false` | Yetki/indeks farkı |
| `launcher.openShortcut` | Başlatıcı kısayol | `hotkey` | `Ctrl+Space` | Konflikt çözümü |
| `launcher.rememberLastCategory` | Son kategori | `bool` | `true` | Sadece yerel |

## 12. Sistem Kontrolü — 12 ayar

Ses, medya, Wi-Fi, Bluetooth ve hızlı ayarlar.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `system.audioPanelEnabled` | Ses paneli | `bool` | `true` | Gerçek API |
| `system.audioDeviceSelector` | Çıkış aygıtı seçimi | `bool` | `true` | Destekli aygıt |
| `system.showInputDevices` | Giriş aygıtları | `bool` | `false` | Gizlilik açısından kapalı |
| `system.mediaControls` | Medya kontrolleri | `bool` | `true` | Desteklenen medya oturumları |
| `system.wifiPanel` | Wi-Fi paneli | `bool` | `true` | Donanım yoksa unavailable |
| `system.bluetoothPanel` | Bluetooth paneli | `bool` | `true` | API yetkisi yoksa settings deep-link |
| `system.calendarFlyout` | Takvim açılır menü | `bool` | `true` | Yerel tarih, izinli takvim kaynağı opsiyonel |
| `system.brightnessSlider` | Parlaklık slider | `bool` | `false` | Harici monitörlerde destek garanti değil |
| `system.batteryStatus` | Pil göstergesi | `bool` | `true` | Masaüstünde gizlenebilir |
| `system.quickSettingsColumns` | Hızlı ayar sütunları | `int(2..5)` | `3` | Resize |
| `system.preferredSettingsFallback` | Sistem ayarlarına bağlantı | `bool` | `true` | Unsupported API durumunda |
| `system.clockSeconds` | Saatte saniye göster | `bool` | `false` | Gereksiz timer düşür |

## 13. Bildirimler — 10 ayar

Claxe bildirimleri, filtre ve sessiz mod.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `notifications.enabled` | Claxe bildirimleri | `bool` | `true` | OS bildirim geçmişi yerine geçmez |
| `notifications.position` | Bildirim yeri | `enum(top-right,top-left,bottom-right,bottom-left)` | `top-right` | Monitör bazlı |
| `notifications.displayMs` | Görünme süresi | `int(1500..20000)` | `5000` | Sistem accessibility |
| `notifications.quietHoursEnabled` | Sessiz saatler | `bool` | `false` | Süre tanımlanabilir |
| `notifications.quietStart` | Sessiz başlangıç | `time` | `22:00` | Yerel saat |
| `notifications.quietEnd` | Sessiz bitiş | `time` | `08:00` | Ertesi güne taşabilir |
| `notifications.muteWhileFullscreen` | Tam ekranda sessize al | `bool` | `true` | Oyun odağı |
| `notifications.storeHistory` | Bildirim geçmişi | `bool` | `false` | Mahremiyet opt-in |
| `notifications.groupByApp` | Uygulamaya göre grupla | `bool` | `true` | Claxe kaynakları |
| `notifications.soundEnabled` | Bildirim sesi | `bool` | `false` | Varsayılan sessiz |

## 14. Pencere Yönetimi — 10 ayar

Windows pencere yerleşimleri ve kısayollar.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `windows.snapLayouts` | Hazır bölünmüş düzen | `bool` | `true` | Safe Win32 positioning |
| `windows.snapGridColumns` | Sütun sayısı | `int(2..6)` | `3` | Yerleşim template |
| `windows.snapGapDip` | Pencere aralığı | `int(0..40)` | `10` | Ekran DPI |
| `windows.rememberPositions` | Konumları hatırla | `bool` | `false` | Uygulama izleme veri sınırı |
| `windows.excludeElevatedApps` | Yükseltilmiş uygulamaları atla | `bool` | `true` | Privilege escalation yok |
| `windows.excludeFullscreenApps` | Tam ekranları hariç tut | `bool` | `true` | Oyunlar |
| `windows.focusFollowsMouse` | Fare odak takibi | `bool` | `false` | Deneysel yüksek risk |
| `windows.enableGlobalHotkeys` | Global düzen kısayolu | `bool` | `true` | Conflict check |
| `windows.keepWindowsInMonitor` | Monitör alanında tut | `bool` | `true` | Offscreen fallback |
| `windows.placementAnimation` | Yerleşim animasyonu | `bool` | `true` | Reduced motion |

## 15. Monitörler — 11 ayar

Ekran algılama, DPI, per-monitor override.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `monitors.sharedThemeDefault` | Ortak tema varsayılanı | `bool` | `true` | Override yapılabilir |
| `monitors.independentLayouts` | Ayrı düzenler | `bool` | `true` | Her monitörde widget/dock |
| `monitors.independentWallpapers` | Ayrı arka planlar | `bool` | `true` | Opsiyonel |
| `monitors.monitorFallback` | Kaybolan monitör politikası | `enum(primary,nearest,preserve)` | `primary` | Layout saklanır |
| `monitors.reactToHotplug` | Hotplug otomatik uyum | `bool` | `true` | Debounce |
| `monitors.respectWorkArea` | Çalışma alanına uy | `bool` | `true` | Görev çubuğu safe zone |
| `monitors.dpiAwareness` | DPI davranışı | `enum(per-monitor-v2)` | `per-monitor-v2` | Değiştirilemeyen engine requirement |
| `monitors.showDockOnAll` | Her ekranda dock | `bool` | `false` | İlk kurulumda ana monitör |
| `monitors.showWidgetsOnAll` | Her ekranda widget | `bool` | `true` | Per instance |
| `monitors.mirrorLayout` | Düzenleri aynala | `bool` | `false` | Diğer override silinmez |
| `monitors.detectReorder` | Ekran sıralamasını algıla | `bool` | `true` | Stable ID fallback |

## 16. Kısayollar — 10 ayar

Kısayol yöneticisi ve çakışma analizi.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `shortcuts.enabled` | Kısayollar etkin | `bool` | `true` | Global manager |
| `shortcuts.openLauncher` | Launcher | `hotkey` | `Ctrl+Space` | Rebind edilebilir |
| `shortcuts.openSettings` | Ayarları aç | `hotkey` | `Ctrl+Alt+S` | Global çakışma doğrulaması |
| `shortcuts.toggleWidgets` | Widgetları göster/gizle | `hotkey` | `Ctrl+Alt+W` | Fallback tray menu |
| `shortcuts.toggleDock` | Dock göster/gizle | `hotkey` | `Ctrl+Alt+D` | Başka programı bozma |
| `shortcuts.nextProfile` | Sonraki profil | `hotkey` | `Ctrl+Alt+P` | Override status |
| `shortcuts.enableMouseGestures` | Fare jestleri | `bool` | `false` | Yanlış tetiklemeyi azalt |
| `shortcuts.showConflictWarning` | Çakışma uyarısı | `bool` | `true` | Kayıt hatası feedback |
| `shortcuts.panicExit` | Acil çıkış | `hotkey` | `Ctrl+Alt+Shift+F12` | Windows hotkey conflict check |
| `shortcuts.keyboardNavigation` | Klavye navigasyonu | `bool` | `true` | Accessibility |

## 17. Maskot — 10 ayar

İsteğe bağlı mini maskot ve etkileşim.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `mascot.enabled` | Maskot göster | `bool` | `false` | Varsayılan kapalı |
| `mascot.assetId` | Maskot varlığı | `id` | `null` | Kullanıcı onaylı asset |
| `mascot.position` | Konum | `enum(bottom-left,bottom-right,custom)` | `bottom-right` | Floating widget |
| `mascot.scale` | Boyut çarpanı | `number(0.5..2)` | `1` | Ekranı kaplamamalı |
| `mascot.clickAction` | Tıklama eylemi | `enum(none,launcher,settings,expression)` | `none` | Native yetki sınırlı |
| `mascot.showExpressions` | İfadeler | `bool` | `true` | Asset desteklerse |
| `mascot.idleAnimation` | Boşta animasyon | `bool` | `false` | Performans |
| `mascot.hideFullscreen` | Tam ekranda gizle | `bool` | `true` | Oyun dostu |
| `mascot.allowSpeechBubbles` | Konuşma balonları | `bool` | `false` | İsteğe bağlı, ağ bağlantısı yok |
| `mascot.reduceMotion` | Maskot animasyonları azalt | `bool` | `true` | OS preference |

## 18. Profiller — 10 ayar

Hazır ve kullanıcı preset yönetimi.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `profiles.active` | Etkin profil | `id` | `midnight-default` | Başlangıç preset |
| `profiles.autoSaveChanges` | Değişikliği otomatik kaydet | `bool` | `false` | Kaydet/iptal kontrolü |
| `profiles.confirmSwitch` | Geçerken onayla | `bool` | `false` | Kayıp değişiklikte sor |
| `profiles.includeMonitorOverrides` | Ekran ayarlarıyla birlikte sakla | `bool` | `true` | Device mapping |
| `profiles.includeWallpaperAssets` | Görselleri export'a göm | `bool` | `false` | Telif ve boyut kontrolü |
| `profiles.backupBeforeImport` | Import öncesi yedek | `bool` | `true` | Geri alınabilir |
| `profiles.importOverwritePolicy` | İçe alma çakışması | `enum(rename,ask,replace)` | `ask` | Asla sessiz overwrite |
| `profiles.storeVersionHistory` | Profil versiyonları | `bool` | `true` | Undo/restore |
| `profiles.maxBackups` | Yedek sayısı | `int(1..30)` | `5` | Disk alanı |
| `profiles.showPresetGallery` | Hazır preset galerisi | `bool` | `true` | Kullanıcı presets |

## 19. Otomasyon — 10 ayar

App, saat ve monitör koşulları, conflict.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `automation.enabled` | Profil kuralları etkin | `bool` | `false` | Kullanıcı kuralları açınca aktif |
| `automation.switchOnApp` | Uygulama açılınca değiştir | `bool` | `false` | Sadece kullanıcı tanımlı kural |
| `automation.switchOnTime` | Saate göre değiştir | `bool` | `false` | Yerel saat |
| `automation.switchOnMonitor` | Monitör durumuna göre | `bool` | `false` | Hotplug debounce |
| `automation.priority` | Kural önceliği | `enum(manual-first,rules-first)` | `manual-first` | Manuel seçim korunur |
| `automation.cooldownSec` | Profil geçiş aralığı | `int(1..300)` | `15` | Thrashing engelle |
| `automation.manualOverrideMinutes` | Manuel kilit süresi | `int(0..1440)` | `30` | 0 süre = oturum sonu olarak yorumlanmaz |
| `automation.pauseOnFullscreen` | Tam ekranda otomatik performans | `bool` | `true` | Claxe performans guard |
| `automation.notifyOnProfileSwitch` | Profil değişince bildir | `bool` | `false` | Bildirim spam yok |
| `automation.logRules` | Kural günlüğü | `bool` | `false` | Yerel minimal log |

## 20. Gizlilik & Güvenlik — 12 ayar

İzinler ve açık rıza.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `privacy.telemetryOptIn` | Tanılama telemetrisi | `bool` | `false` | Default tamamen kapalı |
| `privacy.crashReportingOptIn` | Çökme raporu gönder | `bool` | `false` | Dış ağ yok |
| `privacy.allowRemoteWidgets` | Uzak widget içerikleri | `bool` | `false` | Faz 1 dışı |
| `privacy.requireWidgetPermission` | Widget yetkisi sor | `bool` | `true` | Origin/broker kontrollü |
| `privacy.allowWidgetNetwork` | Widget ağı | `bool` | `false` | Explicit allowlist |
| `privacy.allowWidgetClipboard` | Widget pano erişimi | `bool` | `false` | İzin bazlı |
| `privacy.allowWidgetMicrophone` | Widget mikrofonu | `bool` | `false` | Varsayılan yasak |
| `privacy.allowWidgetCamera` | Widget kamerası | `bool` | `false` | Varsayılan yasak |
| `privacy.allowWidgetLocation` | Widget konumu | `bool` | `false` | Varsayılan yasak |
| `privacy.obfuscatePathsInLogs` | Loglarda dosya yollarını maskele | `bool` | `true` | Gizlilik |
| `privacy.keepWidgetDataAfterUninstall` | Kaldırınca widget verisi kalsın | `bool` | `false` | Uninstall wizard |
| `privacy.showPermissionAudit` | İzin denetim ekranı | `bool` | `true` | Her widget geçmiş izin |

## 21. Gelişmiş — 12 ayar

Deneysel/diagnostics/geri dönüş.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `advanced.experimentalEnabled` | Deneysel özellikleri etkinleştir | `bool` | `false` | Ayrı onay ve rollback |
| `advanced.safeModeOnCrash` | Crash sonrası safe-mode | `bool` | `true` | Önce durum kurtar |
| `advanced.restoreTaskbarOnExit` | Çıkışta görev çubuğunu geri yükle | `bool` | `true` | Eğer Claxe değiştirmişse |
| `advanced.renderDebugOverlay` | Çizim tanılama | `bool` | `false` | Yalnız debug |
| `advanced.verboseLogging` | Detaylı log | `bool` | `false` | Özel bilgiler maskelenmeli |
| `advanced.maxLogFiles` | Log dosyası sayısı | `int(1..30)` | `7` | Rotating |
| `advanced.maxLogSizeMB` | Log dosyası limiti | `int(1..100)` | `10` | Disk limiti |
| `advanced.hardwareAcceleration` | Donanım hızlandırma | `enum(auto,on,off)` | `auto` | GPU sorununa fallback |
| `advanced.showDeveloperTools` | Geliştirici araçları | `bool` | `false` | Web widget debug ayrı |
| `advanced.resetUI` | Görsel ayarları sıfırla | `action` | `n/a` | İşlem onayı gerekir |
| `advanced.exportDiagnostics` | Tanılama paketi dışa aktar | `action` | `n/a` | Kişisel veri maskelenmeli |
| `advanced.panicRestore` | Windows'a güvenli dön | `action` | `n/a` | Acil görünür eylem |

## 22. Erişilebilirlik — 10 ayar

Görsel ve motor erişilebilirliği.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `accessibility.followSystemReduceMotion` | Sistem az hareket ayarına uy | `bool` | `true` | İlk öncelik |
| `accessibility.highContrast` | Yüksek kontrast | `enum(system,on,off)` | `system` | Tema token override |
| `accessibility.largeText` | Büyük metin modu | `bool` | `false` | UI ölçeğiyle birlikte |
| `accessibility.keyboardFocusRing` | Klavye odak halkası | `bool` | `true` | Her etkileşimli öğe |
| `accessibility.screenReaderLabels` | Erişilebilir etiketler | `bool` | `true` | UI Automation |
| `accessibility.animationPhotosensitivity` | Yoğun ışık efektlerini sınırla | `bool` | `true` | Rapid flashing yok |
| `accessibility.tooltips` | Açıklama baloncukları | `bool` | `true` | Ayar açıklaması |
| `accessibility.colorBlindPresets` | Renk körlüğü profili | `enum(off,protan,deutan,tritan)` | `off` | Renk tek gösterge değil |
| `accessibility.cursorTargetSize` | Tıklama hedefi | `enum(normal,large)` | `normal` | Minimum hedef boyutu |
| `accessibility.firstRunTour` | İlk kullanım rehberi | `bool` | `true` | Erişilebilir kapatma |

## 23. Yedekleme ve Depolama — 10 ayar

Yerel yedek, export güvenliği, disk.

| Ayar ID | UI etiketi | Tür | Varsayılan | Ne yapar? |
|---|---|---|---|---|
| `storage.configLocation` | Config dizini | `path` | `%LOCALAPPDATA%/ClaxeDesktop` | Kullanıcı klasörü |
| `storage.atomicWrites` | Atomik config kaydı | `bool` | `true` | Kapalı bırakılamayan invariant |
| `storage.autoBackup` | Otomatik config yedek | `bool` | `true` | Crash restore |
| `storage.backupIntervalHours` | Yedekleme aralığı | `int(1..168)` | `24` | Disk alanı |
| `storage.maxBackupCount` | Yedek sayısı | `int(1..50)` | `7` | Rotasyon |
| `storage.cacheLimitMB` | Toplam önbellek limiti | `int(32..2048)` | `512` | Wallpaper/thumbnail |
| `storage.clearCacheOnExit` | Çıkışta cache temizle | `bool` | `false` | Startup maliyeti |
| `storage.exportAnonymizePaths` | Export'ta mutlak yol çıkar | `bool` | `true` | Portable profil |
| `storage.importConfirm` | Import öncesi kontrol | `bool` | `true` | Path validation |
| `storage.preserveDataOnUpdate` | Güncellemede veriler kalsın | `bool` | `true` | Şema migration |

## Gerçek uygulama koşulu

Bir ayarı etkinleştirmeden önce değerini **gözlenebilir** bir sisteme bağla. Örnek: `dock.iconSizeDip` gerçek dock ölçülerini değiştirir; `performance.pauseWallpaperInFullscreen` kendi renderer yaşam döngüsünü durdurur; `system.wifiPanel` donanım yoksa gerçek `Unavailable` durumu gösterir. Tamamlanan her ayar için en az bir manuel veya otomatik acceptance testi tanımla.
