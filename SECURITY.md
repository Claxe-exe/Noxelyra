# Güvenlik / Security

## Açık bildirme · Reporting a vulnerability

Lütfen güvenlik açıklarını herkese açık issue olarak değil, GitHub'ın **Security → Report a vulnerability** (private vulnerability reporting) bölümünden bildir.
Please report vulnerabilities privately through GitHub's **Security → Report a vulnerability**, not as a public issue.

## Noxelyra ne yapar, ne yapmaz · Security model

- **Explorer'a dokunmaz.** Explorer kapatılmaz/değiştirilmez, sistem DLL'i yamalanmaz, başka süreçlere DLL enjekte edilmez, kabuk (Winlogon Shell) kayıt değeri değiştirilmez.
- **Normal yetkide çalışır.** Tek istisna Hafifletme'deki servis anahtarlarıdır: her seferinde UAC soran, kısa ömürlü ayrı bir yardımcı süreç yalnız `DiagTrack`, `SysMain` ve `WSearch` servislerini ve `services.json` kaydını kabul eder. Defender, Windows Update ve kabuk servisleri izin listesinde yoktur.
- **Her değişiklik geri alınabilir.** Görev çubuğu, arka plan, Windows görünümü, başlangıç uygulamaları ve servisler değiştirilmeden önce eski değerleri kaydedilir; çıkışta, bakımda veya çökme sonrası ayrı bir kurtarma yardımcısı geri yükler.
- **Yalnız kullanıcı (HKCU) kişiselleştirme değerleri** yazılır; yazılabilecek anahtarlar koddaki izin listesiyle sınırlıdır.
- **Ağ isteği yok, telemetri yok.** Web widget'ları yalnız yerel paketlerden, kısıtlı bir izin ve origin politikasıyla çalışır.
- **Win+D kısayolu** isteğe bağlıdır ve belgelenmiş düşük seviye klavye kancasıyla yalnız Win+D'yi işler; tuş kaydetmez, başka tuşları tüketmez.
- Kullanıcının seçtiği görseller, videolar ve açılış klipleri yerel veri klasörüne kopyalanır; ağ ve ağ paylaşımı yolları reddedilir.

## Desteklenen sürüm · Supported versions

Yalnız en son sürüm güvenlik düzeltmesi alır. Only the latest release receives fixes.
