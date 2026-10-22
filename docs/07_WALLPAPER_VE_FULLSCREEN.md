# Wallpaper modları, fullscreen algılama ve performans

## Modlar — tam olarak birbirinden ayrı

1. **External — Wallpaper Engine:** Kullanıcının ayrıca kurduğu Wallpaper Engine duvar kağıdını kendi uygulaması kontrol eder; Claxe paneller ve widget'lar overlay yapar. Claxe üçüncü taraf programı zorla kapatmaz/kendi görüntüsü gibi göstermez. Uygulama kontrolü için resmi entegrasyon yolu yoksa sadece kendi alanından sorumlu olur. Kullanıcı Wallpaper Engine'deki fullscreen/pause kuralını orada ayarlar.
2. **Internal static:** kullanıcı dosyası, JPEG/PNG/WebP gibi desteklenen formatlar, fill/fit/crop/focal point ve monitör başına yapılandırma.
3. **Internal animated/video:** ayrı renderer (geç faz), desteklenen video/animation formatları belirgin; FPS/battery cap; fullscreen pause.
4. **System wallpaper passthrough:** varsayılan Windows wallpaper'a dokunmadan Claxe UI.

Mod geçişi mümkünse çift animasyon üretmez; harici wallpaper motorunun davranışı üzerinde garantisiz otomasyon iddia etmez. Güvensiz shell/WorkerW injection yerine desteklenen/izole çizim yöntemleri araştırılır; yerleşim fizibilitesi test edilmeden 'tam desktop wallpaper replacement' tamamlandı denmez.

## Fullscreen kuralı

- Girdi: foreground HWND, görünürlük, pencere bounds, aktif monitor bounds ve process bilgisi; appbar fullscreen event yardımcı sinyal.
- Borderless fullscreen dahil kullanıcı eşiğiyle tespit; normal maximize uygulamalar farklı politika alabilir.
- Gereksiz UI overlay oyun üstüne atlamaz. Fullscreen algılanan **ekranda** kendi animasyon ve widget polling durur; diğer monitor için kullanıcı seçimine göre sürdür/duraklat.
- Sahte fullscreen, alt-tab ve video mode toggle'ında thrashing önlemek için debounce/cooldown.
- Masaüstüne dönünce iş güvenli şekilde rehydrate olur, toplu frame patlaması olmaz.
- Pause çeşitleri: `none`, `reduce`, `pauseAnimation`, `suspendWidgets`, `stopRenderer`. Hangi varlıkta uygulanabildiğini göster.
- Pil modu, laptop güç durumu, RDP, ekran kilidi, sleep/resume, modül görünmezliği ek olaylar.
- Per-app allow/deny/always-pause kullanıcı politikası.

## Önerilen performans ölçütleri (hedef, garanti değil)

- Boş masaüstü: UI thread idle; uzunca boşta işlemci kullanımı düşük hedeflenir.
- Kendi video wallpaper durduğunda aktif decode/render döngüsü kapanmalı, sadece pencere gizlenmemeli.
- 2 monitör: birinde fullscreen oyun varken doğru monitör davranışı; 144Hz + 75Hz karışık hızda düzenli UI.
- 4K wallpaper yüklerken bellek/texture artışı tespit edilir; thumbnail cache ve image scaling.
- Güç profili opsiyonları: Eco/Balanced/Visual; varsayılan Balanced.
- Kullanıcı ayarları: FPS cap, animation quality, blur, caching, media audio muted, refresh period.

## Not

Wallpaper Engine'in resmi yardımında fullscreen uygulama için `Stop (free memory)` davranışı anlatılır. Bu Claxe'ın üçüncü taraf uygulama içinden her şeyi otomatik yönetebileceği anlamına gelmez; kullanıcıya doğru ayarın nerede olduğunu göstermek yeterlidir.
