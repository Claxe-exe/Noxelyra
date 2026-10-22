<p align="center"><img src="docs/images/intro-logo.png" alt="Noxelyra açılış animasyonu: yıldızlı gece, mor hilal ve NOXELYRA yazısı" width="760"></p>

<h1 align="center">Noxelyra</h1>
<p align="center"><b>Windows 11 için Linux rice ruhunda, yumuşak ve kişiselleştirilebilir bir masaüstü katmanı.</b><br>
<sub>A soft, Linux-rice-style desktop layer for Windows 11 — bar, dock, widgets, its own desktop and a lighten center, without replacing Explorer.</sub></p>

<p align="center">v0.26 · .NET 10 WPF · MIT</p>

---

## Neler var

| | |
|---|---|
| **Üst bar** | Hyprland/Waybar tarzı cam adacıklar: başlatıcı, odaktaki pencere, saat, medya kontrolleri, ses (tekerlekle), CPU/RAM, palet menüsü. |
| **Dock** | Sürükle-bırak ile sabitleme, sayfalar (1-2-3, tekerlek), macOS benzeri dalga büyütme, çalışan uygulama göstergesi. |
| **Kendi masaüstü** | Gerçek masaüstü dosyaların; widget ve dock'un etrafından dizilen simgeler, kendi arka plan resmin veya videon. **Win+D / Win+M** Noxelyra masaüstünü gösterir. |
| **Temalar** | Midnight, Tempest, Sakura, Catppuccin Mocha, Tokyo Night, Nord, Rosé Pine. Windows'u koyu moda ve palet rengine eşleyebilir. |
| **Açılış / kapanış** | TV açılışı gibi ışık çizgisi, zıplayan noktalar ve hilal; ya da kendi 10 saniyelik MP4/GIF klibin, kararma ve TV geçişiyle. |
| **Hafifletme** | Gerçek RAM kullanımı, açılışta başlayan programlar, Windows ekstraları; isteğe bağlı (UAC'li) telemetri / SysMain / arama servisleri. Hepsi geri alınabilir. |
| **Widget'lar** | Saat, takvim, sistem, not ve izinleri kısıtlı yerel HTML/CSS/JS widget'ları. |

<p align="center"><img src="docs/images/intro-dots.png" width="380" alt="Açılıştaki dört renkli nokta"> <img src="docs/images/nox.png" width="140" alt="Nox maskotu"></p>

## Kurulum

1. [Releases](../../releases) sayfasından son `Noxelyra-x.y.z-win-x64.zip` dosyasını indir ve SHA-256 değerini `.sha256` dosyasıyla karşılaştır.
2. Zip'i bir klasöre çıkar, PowerShell'de `Install.ps1` çalıştır. Yönetici izni istemez; `%LOCALAPPDATA%\Programs\Noxelyra` altına kurar. Taşınabilir kullanım için `app\Noxelyra.exe` yeterli.
3. Kaldırmak için kurulum kökündeki `Uninstall.ps1`. Önce Windows'un görev çubuğu, arka planı ve diğer değişiklikleri geri yüklenir; kişisel ayarların korunur.

**Gerekenler:** Windows 11 (22H2+ önerilir), [.NET 10 Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/10.0), web widget'ları için WebView2 Runtime.

## Güvenlik

Noxelyra Explorer'ı kapatmaz ya da değiştirmez, DLL enjekte etmez, ağ isteği ve telemetri göndermez. Windows'ta değiştirdiği her şeyin eski halini kaydeder ve çıkışta, kaldırmada veya çökme sonrası geri getirir. Ayrıntı: [SECURITY.md](SECURITY.md).

## Geliştirme

```powershell
dotnet build ClaxeDesktop.sln -c Release
dotnet run --project tests/ClaxeDesktop.Core.Tests -c Release -- --desktop   # masaüstü, tema, hafifletme kontrolleri
dotnet run --project tests/ClaxeDesktop.Core.Tests -c Release -- --widgets   # widget kontrolleri
./scripts/Verify-Noxelyra.ps1    # izole veri klasöründe 12 uygulama kontrolü
./scripts/Package-Release.ps1    # artifacts/ altına zip + SHA-256
```

Proje yapısı: `ClaxeDesktop.Core` (modeller, ayarlar, kurallar), `ClaxeDesktop.Platform.Win` (Windows API sarmalayıcıları), `ClaxeDesktop.WebWidgets` (WebView2 sandbox), `ClaxeDesktop.App` (WPF arayüz). Ad alanları geriye uyumluluk için `ClaxeDesktop` olarak kalır. Sürüm geçmişi [CHANGELOG.md](CHANGELOG.md), mimari kararlar [DECISIONS.md](DECISIONS.md), ayrıntılı tasarım ve faz raporları `docs/` altında.

## Bilinen sınırlar

- Başka uygulamaların başlık çubukları ve Windows bildirimleri yeniden tasarlanamaz (enjeksiyon gerektirir).
- Windows sanal masaüstleri için kararlı bir API olmadığından barda çalışma alanı modülü yok.
- "Panel için çalışma alanı ayır" modunda bar bazı sistemlerde 48 px aşağıda kalabiliyor; yüzen bar etkilenmez.
- Temiz VM, karma DPI ve fiziksel uyku testleri henüz tamamlanmadı.

## Lisans

[MIT](LICENSE). Üçüncü taraf bileşenler: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
