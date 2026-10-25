# Güvenlik / Security

## Açık bildirme · Reporting a vulnerability

Lütfen güvenlik açıklarını herkese açık issue olarak değil, GitHub'ın **Security → Report a vulnerability** (private vulnerability reporting) bölümünden bildir.
Please report vulnerabilities privately through GitHub's **Security → Report a vulnerability**, not as a public issue.

## Noxelyra ne yapar, ne yapmaz · Security model

- **Explorer'a dokunmaz.** Explorer kapatılmaz/değiştirilmez, sistem DLL'i yamalanmaz, başka süreçlere DLL enjekte edilmez, kabuk (Winlogon Shell) kayıt değeri değiştirilmez.
- **Normal yetkide çalışır.** Tek istisna Hafifletme'deki servis anahtarlarıdır: her seferinde UAC soran, kısa ömürlü ayrı bir yardımcı süreç yalnız `DiagTrack`, `SysMain` ve `WSearch` servislerini ve `services.json` kaydını kabul eder. Defender, Windows Update ve kabuk servisleri izin listesinde yoktur.
- **Her değişiklik geri alınabilir.** Görev çubuğu, arka plan, Windows görünümü, başlangıç uygulamaları ve servisler değiştirilmeden önce eski değerleri kaydedilir; çıkışta, bakımda veya çökme sonrası ayrı bir kurtarma yardımcısı geri yükler.
- **Yalnız kullanıcı (HKCU) kişiselleştirme değerleri** yazılır; yazılabilecek anahtarlar koddaki izin listesiyle sınırlıdır.
- **Telemetri yok.** Ağ istekleri yalnız şunlardır: Setup ile kurulu kopyanın bu deponun GitHub sürümlerini denetleyip yeni paketi indirmesi (Velopack; paketler SHA-256 ile doğrulanır; Kontrol merkezi → Güncellemeler'den kapatılabilir) ve kullanıcı bir hava durumu widget'ı ekleyip şehir yazdıysa Open-Meteo sorgusu (yalnız `geocoding-api.open-meteo.com` ve `api.open-meteo.com`, HTTPS, yönlendirme ve çerez yok, yanıt 256 KB ile sınırlı; yalnız yazılan şehir adı gönderilir). Kullanıcı hakkında başka bilgi gönderilmez.
- Web widget'ları yalnız yerel paketlerden, kısıtlı bir izin ve origin politikasıyla çalışır.
- **Klavye kancası** (Win+D, Win tuşu başlatıcısı, Alt+Tab, döşeme kısayolları) isteğe bağlıdır ve varsayılan kapalıdır; belgelenmiş düşük seviye klavye kancasıyla yalnız açık özelliklerin tuşlarını işler, tuş kaydetmez, diske veya ağa tuş bilgisi yazmaz. **Ctrl+Shift+Ü** acil çıkışı her zaman Noxelyra'yı kapatıp Windows'u geri yükler.
- **Ada kartı (Alt+0)** açıkken yalnız ←/→/↑/↓/Boşluk/Tab/Esc geçici olarak karta bağlanır (RegisterHotKey) ve kart kapanınca ya da 12 sn sonra bırakılır; tuş kaydedilmez. "Uygulamayı kapat" iki onay ister ve Windows kabuk/oturum süreçlerini reddeder.
- **Rice dosyaları** (`.noxrice`) içe aktarılırken sıkı denetlenir: yalnız `rice.json` ve sabit adlı bir görsel/klip; klasör yolu, fazladan dosya, bilinmeyen alan ve beyan edilenden büyük içerik reddedilir; çalıştırılabilir içerik kabul edilmez. Dışa aktarmada not metinleri, hava durumu şehri, kısayollar ve Windows ayarları dosyaya girmez.
- Kullanıcının seçtiği görseller, videolar ve açılış klipleri yerel veri klasörüne kopyalanır; ağ ve ağ paylaşımı yolları reddedilir.

## Desteklenen sürüm · Supported versions

Yalnız en son sürüm güvenlik düzeltmesi alır. Only the latest release receives fixes.
