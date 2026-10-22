# Noxelyra 0.11 — masaüstünde kalan katman

22 Ekim 2026. Kullanıcı kalan işleri onayladı ve kapsamı “sadece masaüstünde çalışacak, işime yarayacağı zaman öne gelecek, masaüstünde duracak” olarak netleştirdi. Oyun/tam ekran kabulü bu turun hedefi değildir.

## Davranış

- Yeni **Windows masaüstüne dönünce Noxelyra alanını göster** tercihi kapalı varsayılan. Etkinleştirilince alan görev çubuğunda ayrı uygulama olarak gösterilmez, normal uygulamaların altına yerleşir ve masaüstü odağı algılandığında yeniden gösterilir. Otomatik gösterim klavye odağını ele geçirmez; açık uygulamanın üzerine zorla çıkmaz. Kullanıcı bu modu açıkça ilk kez açarken alan etkinleştirilir.
- **Alt+K** dock'u öne çağırır; tekrar basış gizlemez. **Esc** masaüstü alanındayken Windows masaüstüne geçici dönüş sağlar. İzleme aynı masaüstü olayında hemen yeniden açmaz; başka uygulama ziyaretinden sonraki masaüstü geçişiyle yeniden izin verilir. Alt+F4 de alanı geçici gizler; uygulamanın tamamından çıkmak bildirim alanı menüsündedir.
- Explorer ve Windows'un kendi ikonları değiştirilmez. Noxelyra'nın yüzeyi bunların üzerinde duran kendi normal penceresidir. Sistem shell registry değişikliği, Explorer restart/kill, DLL injection veya WorkerW parenting yoktur. Katman çökünce kendi penceresi kaybolur ve Windows masaüstü kullanılabilir kalır.
- Masaüstü/uygulama odağı için [SetWinEventHook](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook) OUTOFCONTEXT kullanılır; DLL başka sürece eşlenmez. Shell sınırı [GetShellWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getshellwindow) ve salt okunur pencere sınıfı/PID kontrolleriyle belirlenir. Mevcut desktop sınıf adları yalnız tanıma içindir; hiyerarşi değiştirilmez. Hook kurulamazsa görünür durum mesajı ve elle açma yolu kalır.
- Uyku/uyanma PowerModeChanged olayları bağlıdır. Suspend sırasında katman açma ve hareket/medya etkinliği bekletilir; resume ekranları tekrar okur. Bilgisayar test için fiziksel uykuya sokulmadı.

## Gerçek Wallpaper Engine düzeltmesi

Bu makinede WE **2.8.0.42** kurulu ve çalışıyor. Önceki stdout pipe yaklaşımı başarı koduyla **boş** yanıt döndürüyordu. Aynı komut yalnız kendimize ait gizli test konsolunda çalıştırıldığında gerçek scene.pkg yolu gözlendi. Bu bulgu üzerine kısa ömürlü bir Noxelyra sorgu yardımcısı eklendi:

1. Rastgele GUID ile yalnız bu çağrının geçici klasörü hazırlanır.
2. Yardımcı kendi konsolunu [AllocConsole](https://learn.microsoft.com/en-us/windows/console/allocconsole) ile oluşturur ve gizler; kullanıcının konsoluna bağlanmaz.
3. Resmi getWallpaper komutu çalıştırılır ve yalnız bu konsolun çıktısı okunur.
4. Yol doğrulanıp kanonikleştirilir; boyut, süre ve yerel yol sınırları uygulanır. Bilinmeyen/boş yanıt değişiklik yapılmasına izin vermez.
5. Sonuç parent'a yerel dosyayla iletilir. Yardımcı, konsolu ve çağrının bilinen geçici dosyası temizlenir. İptalde yalnız kendi başlattığımız sorgu yardımcısı durdurulabilir; WE/Explorer süreçleri öldürülmez.

[Resmi WE komutları](https://help.wallpaperengine.io/en/functionality/cli.html) üzerinden gerçek kısa video uygulaması ve eski scene.pkg dosyasına geri dönüş doğrulandı. Kullanıcının sahnesi testten sonra geri geldi.

WE journal sürüm 2 artık PID + başlangıç zamanı sahibini taşır; eski sürüm 1 kayıtları normal kurtarmada okunabilir. Süreçler arası dosya kilidi, atomik/durable journal ve değişiklik öncesi guardian hazır olma kapısı vardır. Mevcut yerel guardian, görev çubuğuyla birlikte sahip olunan WE kaydını da kurtarır. Yeni oturumun kaydı eski guardian tarafından geri alınmaz. Harici kullanıcı dosya seçimi korunur; pause/play durumu sahiplenilmez.

## Kanıt ve kapsam

`scripts/Verify-DesktopAcceptance.ps1`: **12/12** odaklı masaüstü/WE kontrolü. `artifacts/desktop-acceptance.json` ayrıntılı sonuçtur.

- Kapalı varsayılan; kilit/güvenli/bilinmeyen durumda açmama politikası.
- Gerçek native foreground hook kayıt/serbest bırakma.
- Alanın görev çubuğundan çıkarılması ve ayara yazılması.
- Gerçek bir uygulama HWND'sinin altına native pencere sırası yerleşimi.
- Esc bastırması ve sonraki masaüstü kararında odağı değiştirmeden yeniden gösterme.
- Gerçek Shell MinimizeAll / UndoMinimizeALL turunda katmanın görünür kalması. Bu turda başka bir uygulama foreground kaldı; sonuç fiziksel Win+D tuşunun veya shell foreground olayının uçtan uca gözlendiği diye sunulmaz.
- Suspend/resume **olay işleyicileri**; fiziksel uyku değil.
- Katman üstünde iki Alt+K mesajı ve izleme modunu kapatabilme.
- Gerçek WE'den mevcut yerel dosyayı okuma.
- Gerçek WE video uygulama ve önceki sahneye dönüş.
- Yalnız izole crash fixture sürecini sonlandırdıktan sonra guardian'ın gerçek WE sahnesini geri getirmesi.

`artifacts/desktop-acceptance-shell.json`: ana ve crash kurtarma kayıtları temiz, session.lock yok. Explorer PID 4088'in başlangıcı kabul koşusundan önce ve süreç hâlâ aynı; Explorer yeniden başlatılmadı. Windows monitor API'si iki ekran bildiriyor; fiziksel karma DPI veya kablo sök/tak yapıldığı iddia edilmez.

Temiz Windows VM/installer sertifikası, fiziksel uyku ve uzun süreli ağır yük bu yerel masaüstü turuyla tamamlanmış sayılmaz. Kaynak lisansı ve yayıncı kod imzalama sertifikası hâlâ sahibinin kararıdır. Bu sürüm mevcut Windows shell'ini değiştiren bir shell replacement değildir.

## Çalıştırma

`Noxelyra.cmd` → `artifacts/publish-v0.11/Noxelyra.exe`. Yeni mode kontrol merkezindedir; ayrıca `--desktop-follow` ilk başlatmada kullanıcının masaüstü izleme tercihini açar. Diğer riskli tercihler kendiliğinden açılmaz. Dağıtım `artifacts/Noxelyra-0.11.0-win-x64.zip`.

Son paket kontrolü: v0.11 EXE üzerinde aynı 12/12 kabul geçti. Windows PowerShell ile izole kur/kaldır smoke kontrolü de geçti ve kişisel sentinel verisi korundu. Canlı kullanıcı profilinde DesktopSpace.Enabled ve FollowDesktop true olarak uygulama üzerinden kaydedildi; mevcut bütünleşik panel ve Windows görev çubuğu tercihleri korundu. Yeni mod şu an açık.

