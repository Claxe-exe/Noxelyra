# Güvenlik, geri alma, lisans ve deneysel özellikler

## Shell güvenliği

- **Ön koşul:** Windows Explorer/taskbar önceki çalışır durumda kalacak; app kendi toolbar/overlay'lerini yönetir.
- `explorer.exe` öldürülmez, kabuk değiştirilmez, `Winlogon/Shell` registry anahtarlarına dokunulmaz; gizli DLL injection veya kernel driver yok.
- Görev çubuğu görünüm değişikliğinde önceki ayarı kaydet, sadece bilinen ve güvenilir sistem yöntemiyle ayarla, çıkış/crash/restart sonrası restore et. Windows'un davranışı sürüme göre desteklenmiyorsa `Unsupported` deyip kullanıcı kontrollü ayarlar sayfasını aç.
- **Acil kaçış:** kullanıcıya erişilebilir `Claxe'dan çık / Windows arayüzünü geri getir` eylemi; tray/kısayol fallback, safe mode launch argument (`--safe-mode`), global panic hotkey ancak diğer hotkey'leri çiğneme.
- **Crash recovery:** ilk açılışta önceki kirli kapanış işareti varsa shell taskbar override'larını restore et, animasyon ve deneysel modları devre dışı bırak, güvenli mod öner.
- Kişisel config bozulursa `.bak`/atomic write kurtarma; çökme loop'u varsa minimal varsayılan layout.
- Installer: minimum privilege, default auto-run **kapalı** ve ilk kurulum sorusu; uninstall restore state; kullanıcı verilerini sakla/sil seçeneği.
- Deneysel özellikler kategori altında ayrı; açık user consent, rollback/snapshot ve belgelenmiş sınırlamalar.

## Mahremiyet

- Varsayılan **telemetri yok**; crash report ancak açık kullanıcı onayıyla; loglarda e-posta, kişisel dosya içeriği veya parolalar yer almaz.
- Uygulama/foreground algılama yereldir ve telemetri değildir; minimize veri, history üretme opsiyonel.
- Widget izinleri ayrı; izinler kolayca görüntülenip iptal edilir; arka planda gizli network isteği yok.

## Anime karakterleri ve varlıklar

Kullanıcı kişisel Claxe temalarında anime karakterlerinin görünmesini istiyor. **Bunun kendisi sistem geliştirmeyi durdurma nedeni değil.** Uygulama kişisel dosyaları yükleyebilir; kullanıcıya yerel resim kullanımı seçeneği sunulur. Ancak kullanıcı hak sahibi değilse bu resimleri GitHub dağıtımının içine otomatik ekleme, hak varmış gibi davranma veya farklı lisans uydurma. Kaynağı/izin durumu bilinmeyen fotoğrafı kullanıcının kendi bilgisayarında göstermeyi repo dağıtımıyla karıştırma. İlk GitHub paketinde özgün veya dağıtım izni net tasarım varlıkları kullan. Anime-ilhamlı **özgün maskot** istenirse ayrı onaylı tasarım oluşturulabilir; son chibi denemelerinden hiçbiri otomatik kanon sayılmaz.

## Lisansın mühendislik engeline dönüşmemesi

- Ücretli API/tema/asset şart değilse açık kaynak/Microsoft destekli ücretsiz alternatifleri araştır ve öner.
- Bir kütüphanenin lisansını mutlaka doğrula; kod veya içerik hakkı konusunda **uydurma lisans** yazma.
- İhlal, güvenlik bypass veya yasa dışı entegrasyonu önerme; kullanıcının amacını karşılayan yasal seçenekleri önceliklendir.
- Bilinmeyen hak durumu varsa sadece **dış bağlantı/yerel import** ve dağıtım hariç tutma yolu seç.

## Resilience durumları

- Explorer restart / graphics reset / monitor unplug / Windows lock / sleep / RDP / DPI change / appbar conflict / fullscreen game / corrupted settings / WebView2 crash / uninstall.
- Kullanıcı onayı gerektiren adımlar: autostart, sistem taskbar etkileşimi, deneysel shell hooks, untrusted widget network izni.
