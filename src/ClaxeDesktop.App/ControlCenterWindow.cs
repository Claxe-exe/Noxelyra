using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
using Microsoft.Win32;
namespace ClaxeDesktop.App;
internal sealed class ControlCenterWindow : Window
{
    private readonly DesktopController controller;
    private readonly WindowsAudioService audio=new();
    private readonly MediaService media=new();
    private readonly Slider volume=new(){Minimum=0,Maximum=100,TickFrequency=1,Margin=new(0,8,0,8)};
    private readonly CheckBox mute=new(){Content="Sesi kapat"};
    private readonly TextBlock audioStatus=new(){TextWrapping=TextWrapping.Wrap};
    private readonly TextBlock mediaTitle=new(){TextWrapping=TextWrapping.Wrap,FontSize=16};
    private readonly CheckBox integrated=new(){Content="Panel için Windows çalışma alanında yer ayır"};
    private readonly CheckBox hotkeys=new(){Content="Ctrl+Alt+Space: uygulamalar · Ctrl+Alt+Q: kontroller"};
    private readonly ComboBox displays=new(){Margin=new(0,10,0,8)};
    private readonly ComboBox rotationInterval=new(){ItemsSource=new[]{30,60,120,300},SelectedItem=120,Margin=new(0,6,0,8)};
    private readonly TextBlock wallpaperStatus=new(){TextWrapping=TextWrapping.Wrap,Opacity=.65};
    private readonly Button play=new(){Content="Oynat"},next=new(){Content="Sonraki"},previous=new(){Content="Önceki"};
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(2)};
    private bool refreshing,polling,playing,closed;
    internal Slider Volume=>volume;
    internal CheckBox Integrated=>integrated;
    public ControlCenterWindow(DesktopController controller)
    {
        this.controller=controller;Title="Noxelyra · Kontrol merkezi";Width=720;Height=850;MinWidth=580;MinHeight=580;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty,"Canvas");SetResourceReference(ForegroundProperty,"Text");
        var root=new StackPanel{Margin=new(28)};Content=new ScrollViewer{Content=root,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        root.Children.Add(new TextBlock{Text="Masaüstün, tek yerde.",FontSize=28,FontWeight=FontWeights.SemiBold});
        var status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new(0,8,0,16)};status.DataContext=controller.ViewModel;status.SetBinding(TextBlock.TextProperty,"Status");root.Children.Add(status);
        var desktop=Section(root,"Windows ile bütünleşme");desktop.Children.Add(integrated);desktop.Children.Add(hotkeys);
        desktop.Children.Add(new TextBlock{Text="Bu tercihler hemen kaydedilir. Windows görev çubuğunu aşağıdaki seçenekten Noxelyra oturumu boyunca yönetebilirsin.",TextWrapping=TextWrapping.Wrap,Opacity=.65});
        integrated.IsChecked=controller.ViewModel.Settings.IntegratedDesktop;hotkeys.IsChecked=controller.ViewModel.Settings.GlobalShortcuts;integrated.IsEnabled=hotkeys.IsEnabled=!controller.IsSafeMode&&!controller.ReadOnly;
        integrated.Click+=Configure;hotkeys.Click+=Configure;
        var taskbar=new CheckBox{Content="Noxelyra açıkken Windows görev çubuğunu otomatik gizle",IsChecked=!controller.IsSafeMode&&controller.ViewModel.Settings.HideWindowsTaskbar,IsEnabled=!controller.IsSafeMode&&!controller.ReadOnly};desktop.Children.Add(taskbar);
        taskbar.Click+=async(_,_)=>{taskbar.IsEnabled=false;await controller.ConfigureWindowsTaskbarAsync(taskbar.IsChecked==true);taskbar.IsChecked=controller.ViewModel.Settings.HideWindowsTaskbar;taskbar.IsEnabled=!controller.IsSafeMode&&!controller.ReadOnly;};
        desktop.Children.Add(new TextBlock{Text="Görev çubuğu ekran kenarına gelince açılır. Çıkışta eski tercih geri gelir; çökmede ayrı yerel yardımcı geri yüklemeyi dener.",TextWrapping=TextWrapping.Wrap,Opacity=.65});
        var actions=new WrapPanel();desktop.Children.Add(actions);
        Button(actions,"Uygulamalar",()=>controller.ShowLauncher());Button(actions,"Widget atölyesi",()=>controller.Widgets.ShowEditor());Button(actions,"Rafı göster / gizle",controller.Widgets.ToggleShelf);Button(actions,"Görünüm ayarları",controller.ShowSettings);Button(actions,"Hafifletme",controller.ShowLighten);
        Button(actions,"Profiller ve kurallar",()=>controller.ShowAdvanced());Button(actions,"Pencere düzenleri",()=>controller.ShowAdvanced(2));Button(actions,"Yerel maskot",()=>controller.ShowAdvanced(3));
        var look=Section(root,"Windows görünümü");
        var lookStatus=new TextBlock{Text="Windows'un kendi pencereleri, menüleri ve Ayarlar uygulaması. Değişiklikler senin Windows tercihindir; Noxelyra kapanınca da kalır.",TextWrapping=TextWrapping.Wrap,Opacity=.7};
        var dark=new CheckBox{Content="Windows'u koyu moda al",IsChecked=controller.WindowsDark,IsEnabled=!controller.IsSafeMode&&!controller.ReadOnly};
        var transparency=new CheckBox{Content="Windows saydamlık efektleri",IsChecked=controller.WindowsTransparency,IsEnabled=dark.IsEnabled};
        look.Children.Add(dark);look.Children.Add(transparency);look.Children.Add(lookStatus);
        var lookActions=new WrapPanel();look.Children.Add(lookActions);
        Button? restoreLook=null;
        void Refresh(string message){lookStatus.Text=message;dark.IsChecked=controller.WindowsDark;transparency.IsChecked=controller.WindowsTransparency;restoreLook!.IsEnabled=dark.IsEnabled&&controller.WindowsLookChanged;}
        dark.Click+=(_,_)=>Refresh(controller.SetWindowsDark(dark.IsChecked==true));
        transparency.Click+=(_,_)=>Refresh(controller.SetWindowsTransparency(transparency.IsChecked==true));
        var accent=Button(lookActions,"Vurgu rengini palete eşle · deneysel",()=>Refresh(controller.MatchWindowsAccent()));accent.IsEnabled=dark.IsEnabled;
        accent.ToolTip="Belgelenmiş bir API değildir. Windows Ayarları'nda 'vurgu rengini otomatik seç' açıksa Windows rengi yeniden değiştirebilir.";
        restoreLook=Button(lookActions,"Windows görünümünü eski haline getir",()=>Refresh(controller.RestoreWindowsLook()));restoreLook.IsEnabled=dark.IsEnabled&&controller.WindowsLookChanged;
        var space=Section(root,"Noxelyra masaüstü alanı");
        space.Children.Add(new TextBlock{Text="Gerçek masaüstü dosyaların, sürüklenebilir ikonlar, özgün gece görünümü ve yerel video. Esc ile Windows masaüstüne dönersin. Bu bağımsız alan Explorer ikon ayarlarını değiştirmez.",TextWrapping=TextWrapping.Wrap,Opacity=.75});
        var spaceStartup=new CheckBox{Content="Noxelyra açılınca masaüstü alanını aç",IsChecked=controller.ViewModel.Settings.DesktopSpace.Enabled,IsEnabled=!controller.IsSafeMode&&!controller.ReadOnly};space.Children.Add(spaceStartup);
        spaceStartup.Click+=async(_,_)=>{await controller.ConfigureDesktopSpaceAsync(spaceStartup.IsChecked==true);spaceStartup.IsChecked=controller.ViewModel.Settings.DesktopSpace.Enabled;};
        var followDesktop = new CheckBox { Content = "Windows masaüstüne dönünce Noxelyra alanını göster", IsChecked = controller.ViewModel.Settings.DesktopSpace.FollowDesktop, IsEnabled = !controller.IsSafeMode && !controller.ReadOnly }; space.Children.Add(followDesktop);
        followDesktop.Click += async (_, _) => { await controller.ConfigureDesktopFollowAsync(followDesktop.IsChecked == true); followDesktop.IsChecked = controller.ViewModel.Settings.DesktopSpace.FollowDesktop; spaceStartup.IsChecked = controller.ViewModel.Settings.DesktopSpace.Enabled; };
        var superD = new CheckBox { Content = "Win+D Noxelyra masaüstünü göstersin (tekrar basınca pencereler geri gelir)", IsChecked = controller.ViewModel.Settings.DesktopSpace.CaptureShowDesktop, IsEnabled = !controller.IsSafeMode && !controller.ReadOnly }; space.Children.Add(superD);
        superD.Click += async (_, _) => { await controller.SetCaptureShowDesktopAsync(superD.IsChecked == true); superD.IsChecked = controller.ViewModel.Settings.DesktopSpace.CaptureShowDesktop; spaceStartup.IsChecked = controller.ViewModel.Settings.DesktopSpace.Enabled; };
        space.Children.Add(new TextBlock { Text = "İsteğe bağlı masaüstü katmanı: uygulamalar önde kalır, Esc geçici olarak Windows masaüstünü gösterir. Explorer çalışmaya devam eder.", TextWrapping = TextWrapping.Wrap, Opacity = .7 });
        var spaceActions=new WrapPanel();space.Children.Add(spaceActions);
        Button(spaceActions,"Masaüstü alanını aç",controller.ShowDesktopSpace);Button(spaceActions,"Özgün Nox maskotunu aç",async()=>await controller.UseOriginalMascotAsync());Button(spaceActions,"Özgün gece görselini Windows'a uygula",async()=>await controller.UseOriginalWallpaperAsync());
        var engine=Section(root,"Wallpaper Engine · isteğe bağlı");
        engine.Children.Add(new TextBlock{Text="Kurulu Wallpaper Engine'in resmi komutlarını kullanır. Önce Wallpaper Engine'i kendin aç. Otomatik pause/play veya önceki oynatma durumunu geri getirme iddiası yoktur; aşağıdaki komutlar açık seçiminle tüm WE arka planlarına gider.",TextWrapping=TextWrapping.Wrap,Opacity=.75});
        var engineStatus=new TextBlock{Text=controller.ViewModel.Settings.DesktopSpace.EnginePath??"Wallpaper Engine seçilmedi.",TextWrapping=TextWrapping.Wrap};engine.Children.Add(engineStatus);
        var engineActions=new WrapPanel();engine.Children.Add(engineActions);
        Button(engineActions,"Wallpaper Engine uygulamasını seç",async()=>{var picker=new OpenFileDialog{Filter="Wallpaper Engine|wallpaper32.exe;wallpaper64.exe",CheckFileExists=true};if(picker.ShowDialog(this)==true){await controller.SetWallpaperEnginePathAsync(picker.FileName);engineStatus.Text=controller.ViewModel.Settings.DesktopSpace.EnginePath??controller.ViewModel.Status;}});
        Button(engineActions,"Seçili videoyu WE ile Windows'a uygula",async()=>engineStatus.Text=await controller.ApplyExternalVideoAsync());
        Button(engineActions,"Önceki WE arka planını geri getir",async()=>engineStatus.Text=await controller.RestoreExternalWallpaperAsync());
        foreach(var command in new[]{("Geçerli arka planı oku","getWallpaper"),("WE arka planlarını duraklat","pause"),("WE arka planlarını sürdür","play")})Button(engineActions,command.Item1,async()=>engineStatus.Text=await controller.SendWallpaperEngineAsync(command.Item2));
        var sound=Section(root,"Ses ve medya");sound.Children.Add(audioStatus);sound.Children.Add(volume);sound.Children.Add(mute);
        System.Windows.Automation.AutomationProperties.SetName(volume,"Windows varsayılan çıkış ses düzeyi");
        volume.ValueChanged+=(_,_)=>{if(!refreshing)TryAudio(()=>audio.SetLevel((float)(volume.Value/100)));};mute.Click+=(_,_)=>TryAudio(()=>audio.SetMute(mute.IsChecked==true));
        sound.Children.Add(mediaTitle);var mediaButtons=new WrapPanel();mediaButtons.Children.Add(previous);mediaButtons.Children.Add(play);mediaButtons.Children.Add(next);sound.Children.Add(mediaButtons);
        play.Click+=async(_,_)=>await MediaAction(playing?"pause":"play");previous.Click+=async(_,_)=>await MediaAction("previous");next.Click+=async(_,_)=>await MediaAction("next");
        var system=Section(root,"Windows kontrolleri");var links=new WrapPanel();system.Children.Add(links);
        foreach(var item in new[]{("Wi-Fi / ağ","network"),("Bluetooth","bluetooth"),("Ses aygıtları","sound"),("Ekranlar","display"),("Bildirimler","notifications"),("Görev çubuğu","taskbar")}) Button(links,item.Item1,()=>controller.OpenSystem(item.Item2));
        system.Children.Add(new TextBlock{Text="Kablosuz ağ, aygıtlar ve ekran tercihlerini Windows ayarlarından yönet.",TextWrapping=TextWrapping.Wrap,Opacity=.65});
        var wallpaper=Section(root,"Masaüstü arka planı");wallpaper.Children.Add(displays);wallpaper.Children.Add(wallpaperStatus);
        var wallpaperActions=new WrapPanel();wallpaper.Children.Add(wallpaperActions);
        var choose=Button(wallpaperActions,"Resim veya görsel listesi seç",ChooseWallpaper);choose.IsEnabled=false;
        wallpaper.Children.Add(new TextBlock{Text="Birden fazla görsel seçersen sırayla değişir. Geçiş aralığı (saniye):",TextWrapping=TextWrapping.Wrap});wallpaper.Children.Add(rotationInterval);
        var imageMode=new CheckBox{Content="Windows arka planını Resim moduna ayarladım"};wallpaper.Children.Insert(1,imageMode);imageMode.IsEnabled=!controller.IsSafeMode&&!controller.ReadOnly;imageMode.Click+=(_,_)=>choose.IsEnabled=imageMode.IsEnabled&&imageMode.IsChecked==true;
        Button(wallpaperActions,"Önceki arka planı geri getir",async()=>await controller.DisableWallpaperAsync());Button(wallpaperActions,"Windows arka plan ayarları",()=>controller.OpenSystem("wallpaper"));
        wallpaper.Children.Add(new TextBlock{Text="Resim modunda ekran başına görsel. Çıkışta önceki görsel geri gelir; çökme sonrası sonraki açılışta kurtarılır. Spotlight, slayt gösterisi ve Wallpaper Engine kullanıyorsan önce kendi yönetimlerini kapat.",TextWrapping=TextWrapping.Wrap,Opacity=.65});
        try{displays.ItemsSource=controller.WallpaperPlatform.Displays();displays.SelectedIndex=0;wallpaperStatus.Text="Yalnız seçtiğin yerel görsel kullanılır. Windows slayt gösterisi açıksa önce Resim moduna geç.";}catch(Exception ex){choose.IsEnabled=false;wallpaperStatus.Text="Windows arka plan servisine erişilemiyor; Windows ayarlarını kullanabilirsin.";controller.Log.Write("wallpaper-detection-failed",ex);}
        var notices=Section(root,"Noxelyra etkinliği");var activity=new ItemsControl{ItemsSource=controller.Activity};notices.Children.Add(activity);
        var footer=new WrapPanel();root.Children.Add(footer);Button(footer,"Windows ile başlatma",controller.ShowStartupSettings);Button(footer,"Noxelyra'dan çık / Windows'a dön",controller.Exit);
        Loaded+=async(_,_)=>await RefreshAsync();timer.Tick+=async(_,_)=>await RefreshAsync();timer.Start();Closed+=(_,_)=>{closed=true;timer.Stop();};
    }
    private async void Configure(object sender,RoutedEventArgs e)
    {
        integrated.IsEnabled=hotkeys.IsEnabled=false;
        await controller.ConfigureDesktopAsync(integrated.IsChecked==true,hotkeys.IsChecked==true);
        integrated.IsChecked=controller.ViewModel.Settings.IntegratedDesktop;hotkeys.IsChecked=controller.ViewModel.Settings.GlobalShortcuts;
        integrated.IsEnabled=hotkeys.IsEnabled=!controller.IsSafeMode&&!controller.ReadOnly;
    }
    private async void ChooseWallpaper()
    {
        if(displays.SelectedItem is not WallpaperDisplay display)return;
        var picker=new OpenFileDialog{Title="Masaüstü için 1–20 yerel görsel seç",Filter="Resimler|*.png;*.jpg;*.jpeg;*.bmp",CheckFileExists=true,Multiselect=true};
        if(picker.ShowDialog(this)==true)await controller.ApplyWallpaperSetAsync(display.Id,picker.FileNames,rotationInterval.SelectedItem is int seconds?seconds:120);
    }
    private void TryAudio(Action action)
    {
        try{action();}catch(Exception ex)when(ex is COMException or InvalidOperationException or ArgumentException){audioStatus.Text="Ses değiştirilemedi; çıkış aygıtını Windows ayarlarından kontrol et.";controller.Log.Write("audio-change-failed",ex);}
    }
    internal async Task RefreshAsync()
    {
        if(polling||closed||!IsVisible)return;polling=true;
        try
        {
            refreshing=true;
            try{var state=audio.Read();if(!volume.IsMouseCaptureWithin&&!volume.IsKeyboardFocusWithin)volume.Value=Math.Round(state.Level*100);mute.IsChecked=state.Muted;volume.IsEnabled=mute.IsEnabled=true;audioStatus.Text=$"Varsayılan Windows çıkışı · %{state.Level*100:0}";}
            catch(Exception ex)when(ex is COMException or InvalidOperationException){volume.IsEnabled=mute.IsEnabled=false;audioStatus.Text="Ses aygıtına erişilemiyor. Ses aygıtları düğmesi Windows ayarlarını açar.";}
            finally{refreshing=false;}
            try
            {
                var state=await media.ReadAsync();if(closed)return;
                mediaTitle.Text=state is null?"Etkin medya oturumu yok.":state.Title+"\n"+state.Artist;
                playing=state?.Playing==true;play.Content=playing?"Duraklat":"Oynat";play.IsEnabled=state is not null&&(playing?state.CanPause:state.CanPlay);next.IsEnabled=state?.CanNext==true;previous.IsEnabled=state?.CanPrevious==true;
            }
            catch(Exception ex)when(ex is COMException or InvalidOperationException or UnauthorizedAccessException){mediaTitle.Text="Bu Windows oturumunda medya kontrolüne erişilemiyor.";play.IsEnabled=next.IsEnabled=previous.IsEnabled=false;}
        }
        finally{polling=false;}
    }
    private async Task MediaAction(string action){try{if(!await media.InvokeAsync(action))controller.ViewModel.Status="Medya uygulaması komutu kabul etmedi.";await RefreshAsync();}catch(Exception ex)when(ex is COMException or InvalidOperationException){controller.ViewModel.Status="Medya oturumu değişti; yeniden deneyebilirsin.";}}
    private static StackPanel Section(Panel parent,string title)
    {
        var section=new StackPanel();section.Children.Add(new TextBlock{Text=title,FontSize=18,FontWeight=FontWeights.SemiBold,Margin=new(0,0,0,10)});
        var border=new Border{CornerRadius=new(14),Padding=new(18),Margin=new(0,0,0,14),Child=section};border.SetResourceReference(Border.BackgroundProperty,"Surface");border.SetResourceReference(Border.BorderBrushProperty,"Line");border.BorderThickness=new(1);parent.Children.Add(border);return section;
    }
    private static Button Button(Panel parent,string label,Action action){var button=new Button{Content=label,Padding=new(10,7,10,7)};button.Click+=(_,_)=>action();parent.Children.Add(button);return button;}
}

