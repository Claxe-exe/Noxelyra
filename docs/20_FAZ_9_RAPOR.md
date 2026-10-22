# Faz 9 — v0.9 adayı ve Alt+K

22 Ekim 2026: kullanıcı Faz 9'u ve global Alt+K dock erişimini onayladı.

## Yapıldı

- Alt+K dock'u ekranın mevcut kenarında, açık uygulamaların önüne çağırır. Gizlenmiş/daralmış dock açılır. Kaydedilmiş konum ve kalıcı topmost tercihi değişmez. Esc, tekrar Alt+K veya başka pencereye geçiş normal davranışı geri getirir; etkileşimsiz 15 saniye süre sınırı da vardır. Etkin klavye/fare kullanımı süreyi uzatır.
- RegisterHotKey + MOD_NOREPEAT; başka uygulamanın kısayolu sahiplenilmez. Çakışma durumu etkinlik mesajında, alternatif bildirim alanı menüsündedir. Ayrıca EXE `--dock` komutu mevcut örneği çağırır. Secure desktop/exclusive fullscreen desteği iddia edilmez.
- Kullanıcı kapsamlı kurulum: `%LOCALAPPDATA%/Programs/ClaxeDesktop`, Başlat kısayolu, yerel manifest bütünlük denetimi. Yönetici, yeni bağımlılık, kayıt defteri değişikliği, gizli indirme yok. Yeni sürüm için kaldırıp yeniden kurulur; kişisel veriler korunur.
- Kaldırma önce uygulamaya current-user named pipe üzerinden temiz çıkış gönderir, mutex ile kapanmasını bekler ve görev çubuğu/arka plan kurtarmasını işler. Başarısız kurtarmada dosyalar korunur. Kurulum kökü ve reparse kontrolleri recursive silmeden hemen önce tekrar yapılır; yalnız doğru target ve sahiplik açıklamalı Başlat/Startup kısayolları kaldırılır.
- `docs/DEPENDENCIES.md`: runtime/SDK, lisans ve varlık envanteri. WebView2 özgün LICENSE/NOTICE pakette doğrulandı. Referans görseller ve kişisel asset'ler paket dışında.

## Derleme ve test

- `dotnet build src/ClaxeDesktop.App -c Release --nologo`: başarılı, **0 hata / 0 uyarı**.
- `scripts/Package-Release.ps1`: Windows x64 framework-dependent publish ve SHA256 manifestli ZIP oluşturuldu. Betik UTF-8 BOM ile Windows PowerShell'de Türkçe okunabilir.
- `scripts/Verify-Release.ps1`: kurulan paket üzerinde **12/12**. Sonuç `artifacts/phase9-results.json`.
- Kapsam: Alt+K gerçek kayıt/çakışma + WM_HOTKEY yolu, görünür/açık/topmost dock, timer kalıcılığı, Esc, ikinci basış/konum, fullscreen geri dönüşü (test sinyali), dirty session/ayar yedeği, kurulum/lisanslar, bozuk paket reddi, yanlış silme hedefi reddi, kurtarma hatasında kaldırmayı durdurma, başarılı kaldırma/veri koruma/temiz marker/Explorer PID korunması.
- Kurulum testi gerçek kullanıcı Başlat/Startup kısayollarını değiştirmedi; özel test kökü workspace `artifacts` altındaydı. Gerçek görev çubuğu değişikliği bu kapsamda tekrar yapılmadı; önceki Faz 6 kanıtı korunuyor. Fiziksel Alt+K basışı yerine kayıt + pencere mesajı doğrulandı.
- Görsel QA: `artifacts/phase9-summoned-dock.png` incelendi; referanstaki koyu plum yüzey ve mor kontur korunuyor. İzole test ayarları uygulama kısayolu içermediğinden görüntü yalnız tutamaç ve + düğmesidir; gerçek masaüstü içeriği gösterildiği iddia edilmez.

## Çalıştırma

- Workspace: `Claxe Desktop.cmd` artık `artifacts/publish-v0.9/ClaxeDesktop.App.exe` açar. Eski sürüm açıksa önce bildirim alanından Claxe'dan çık. Önceki sürümün başlangıç kısayolu yeni exe'de kapat/aç ile yenilenir.
- Dağıtım: `artifacts/ClaxeDesktop-0.9.0-win-x64.zip`; tamamını çıkar, PowerShell'de `Install.ps1` çalıştır veya `app/ClaxeDesktop.App.exe` aç. Kurulum ağdan runtime indirmez; .NET 10 Desktop Runtime x64 gerekir. Web widget için WebView2 Runtime ayrıca gerekir.
- Kaldırma: kurulum kökündeki `Uninstall.ps1`. Veriler `%LOCALAPPDATA%/ClaxeDesktop` altında korunur. Kopuk monitörün bekleyen wallpaper kurtarması varsa önce ekranı bağlayıp tekrar dene; kurtarma kaydı otomatik silinmez.

## Açık kabul işleri

| Kabul | Durum |
| --- | --- |
| Yerel Windows build, paket kurulumu/kaldırılması, odaklı test | Geçti |
| Temiz Windows 11 VM'de runtime kurulumu, ilk başlatma, kur/kaldır, gerçek shell restore | Bekliyor |
| Fiziksel Alt+K, özel tam ekran oyun, uyku/uyanma, karma DPI, ağır/uzun yük, RDP | Bekliyor |
| Ekran okuyucu, yüksek kontrast ve azaltılmış hareket ile tam manuel erişilebilirlik turu | Bekliyor; mevcut klavye/Esc yolu odaklı sınandı |
| Kaynak lisansı seçimi, dijital imza ve imzalı setup.exe / Windows Apps kaydı | Yapılmadı |
| Video wallpaper/Wallpaper Engine sahipliği ve Windows masaüstü ikonlarını devralan yüzey | Önceki fazlardan kalan sınır; bu sürümde yok |

Bu nedenle Faz 9'un uygulama/paket kısmı teslimdir; bütün kabul kapıları tamamlandı veya v1 hazır denmez. Sonraki iş temiz Windows VM ve fiziksel kullanım kabulüdür.
