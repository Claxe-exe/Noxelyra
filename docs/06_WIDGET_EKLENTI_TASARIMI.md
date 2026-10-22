# Widget sistemi: yerel, HTML/CSS/JS ve izinler

## Modeller

- **Native widget:** C#/WPF, imzalı/projeyle gelen zaman, tarih, RAM, CPU, medya vb.
- **Local Web Widget:** kullanıcı diskte HTML/CSS/JS dosyası ekler; WebView2 host tekil widget kimliği ve katı yetki politikasıyla çalışır.
- **Marketplace:** sonraki sürümde değerlendirilecek; otomatik kod indirip çalıştırma **yok**.

## Manifest örneği

```json
{
  "manifestVersion": 1,
  "id": "local.sample.clock",
  "name": "Simple Clock",
  "entry": "index.html",
  "defaultSizeDip": { "width": 220, "height": 140 },
  "minSizeDip": { "width": 160, "height": 100 },
  "permissions": [],
  "networkOrigins": [],
  "refreshHintSeconds": 60
}
```

## Güvenlik politikası

- Yerel HTML **uygulama yetkisi değildir**; native host API'ye doğrudan erişim verme.
- WebView2 ve host standart (non-admin) kullanıcı yetkisiyle çalışmalı.
- `AddHostObjectToScript` ile sınırsız object köprüsü kurma; dar eylem broker'ı üzerinden JSON mesajları, origin ve tip doğrulama.
- Harici navigasyon varsayılan engelli; internet origin'i izin listesi ve kullanıcı kararıyla.
- Yerel disk yolları, `file://` ve `app://` benzeri yönlendirmeler izole; widget root dışına `../` ile erişim yok.
- Script injection ve `postMessage` komutlarını tipli şema ve sender doğrulamasıyla sınırla.
- Kamera/mikrofon/konum/clipboard/system-control izinleri ayrı ve varsayılan kapalı.
- WebView2 process sınırları ve sınırlamaları belgelenir; tek başına tam güvenlik sandbox'u olarak pazarlanmaz.
- CPU/fps/bellek limitleri yazılım seviyesinde throttling; aşırı kullanımda user-visible suspend.
- Widget manifest importu schema validation + asset boyutu/format limitleri; zip slip/symlink/path traversal koruması.
- Her widget hata ekranı ve yeniden başlatma/disable butonu; bir widget çökünce bütün dock kapanmaz.

## API taslak kapsamı

`theme.getTokens`, `time.getLocal`, `system.getLoad` (izinli), `media.getSession` (izinli), `ui.requestResize`, `storage.get/set` (widget'a özel namespace). Önce sadece en düşük izin gerektiren API'leri uygula; network fetch için açık kullanıcı seçimi ve izin istenir.

## Widget editörü

Drag/resize, snap, docking, responsive breakpoints, z-order, lock, auto hide, duplicate, group/ungroup, grid and guides, presets, versioned save, undo/redo, safe area. Kullanıcı genişletilebilir; tüm option'lar `settings.catalog.json` ile aynı validation yaklaşımını paylaşır.
