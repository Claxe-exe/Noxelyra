# Görsel ve teknik referanslar

## Pakete eklenen görseller

Referans PNG'ler kendi ürünün *nasıl hissedebileceğini* anlatan **özel hazırlanmış Claxe Desktop mockup'larıdır**. Ürünün çalışan ekran görüntüleri değildir. Dock, panel, settings ve 2 monitör düzenlerini aynı tasarım diline oturturlar.

| Dosya | Ne işe yarar |
|---|---|
| `assets/references/01_midnight_desktop.png` | Varsayılan desktop hiyerarşisi |
| `assets/references/02_widget_editor.png` | Widget edit modu, inspector, drag grid |
| `assets/references/03_settings_center.png` | Kategorili ayrıntılı ayar ekranı |
| `assets/references/04_layout_presets.png` | Hyprland/KDE/Anime esintili preset varyasyonları |
| `assets/references/05_dual_monitor.png` | Ekran başına bağımsız panel/widget/wallpaper |
| `assets/moodboard/01_neon_night.png` | Karanlık anime/cyberpunk arka plan atmosferi |
| `assets/moodboard/02_moon_gothic.png` | Misaki Mei havası için gotik/mor ay atmosferi |
| `assets/moodboard/03_celestial_city.png` | Mavi fantastik alternatif tema atmosferi |

Masaüstü referansları, önceki sohbette gösterilen Hyprland ve KDE **tarzına** göre bu paket için yeniden çizilmiştir. Önceki dış internet görsellerinin aynısı elde edilemediği için birebir dosya olarak sunulmaz; dışarıdan alınmış ekran görüntülerinin telifini belirsiz şekilde ZIP'e gömmek yerine **özgün kompozisyonlar** verildi.

## Proje dışı görsel yönlendirme

- Hyprland Wiki: https://wiki.hypr.land/ (floating bars/rounded corners/tiling ilkeleri)
- KDE Plasma: https://kde.org/plasma-desktop/ (özelleştirme, widget sistemleri)
- Wallpaper Engine yardım: https://help.wallpaperengine.io/en/functionality/applicationrules.html (fullscreen pause)

## Windows resmi dokümanlar (Ekim 2026'da kontrol edildi)

- WPF/.NET desktop SDK: https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props-desktop
- Windows masaüstü WinRT API desteği ve TFM: https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/winrt-apis-desktop-apps
- Win32 AppBar / notification & fullscreen: https://learn.microsoft.com/en-us/windows/desktop/shell/application-desktop-toolbars
- Windows taskbar API: https://learn.microsoft.com/en-us/windows/win32/shell/taskbar
- WebView2 Security: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security
- WPF son yenilikler: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/

Sürüm/özellik uyumluluğu hedef Windows makinede yeniden doğrulanmalıdır. Kaynak URL'leri birer araştırma noktasıdır; her API tüm modern Windows sürümlerinde aynı davranışı garanti etmez.
