# Açık konular — teknik fizibilite çalışması

Bu kararların yok sayılması değil, doğrulanmadan yapılmış vaatlere dönüşmesinin önlenmesidir.

1. **Windows görev çubuğunu tam gizleme:** Windows 11 sürümüne ve taskbar davranışına göre API kapsamı. Önce güvenli autohide ve geri alma denemesi; destek dışı durumda kullanıcı kontrollü Windows ayarı aç.
2. **Native desktop wallpaper katmanı:** WorkerW/Progman gibi belgelenmemiş yolları varsayılan yapma; kendi renderer'ın OS desktop background yerine nasıl güvenli davranacağını önce prototiple doğrula.
3. **Wallpaper Engine mode:** kendi uygulamasıyla eş zamanlı render çakışmasını önle; resmi API yoksa başkasının state'ini zorla kontrol etme.
4. **Windows bildirim merkezi:** Claxe'ın kendi bildirimleri kolaydır; diğer bütün uygulamaların bildirimlerini okuyup yönetme izin/API açısından farklıdır, garanti etme.
5. **Wi-Fi/Bluetooth toggle:** her makinede API/donanım/yetki bulunmayabilir; Windows Settings fallback.
6. **Global window titlebar theming:** başka uygulamaların başlık çubukları üzerinde tam kontrol yok; riskli injection yok.
7. **Virtual desktops:** desteğin resmi/kararlı olduğu API'leri bulmadan gereksinimi genişletme.
8. **Açık kaynak yayımlama:** kullanıcının kişisel anime wallpapera izin vermek başka, bunları GitHub binary / repo varlığı olarak dağıtmak başka. Assets manifestte hak durumunu işaretle.
9. **Final maskot:** tasarım henüz onaylanmadı, projede zorunlu sabit karakter yok.
10. **Refresh / 144Hz + 75Hz:** UI kaynak tüketimini monitör bazında ölç; doğrulanmamış fps vaatleri verme.

Bu listeyi her faz sonrasında test sonuçlarıyla güncelle.
