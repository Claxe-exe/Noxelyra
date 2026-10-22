using System.Globalization;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Controls.Primitives;
using ClaxeDesktop.Core;
using Microsoft.Win32;
namespace ClaxeDesktop.App;
internal sealed class AdvancedWindow:Window
{
    private readonly DesktopController controller;
    private readonly TabControl tabs=new();
    private readonly ListBox profiles=new(){Height=205},rules=new(){Height=170},windows=new(){Height=230,SelectionMode=SelectionMode.Extended};
    private readonly List<(Button Button,int Capacity)> layoutButtons=[];
    private readonly TextBox profileName=new(){Text="Kişisel düzen"},ruleName=new(),app=new(),start=new(),end=new(),count=new(),priority=new(){Text="50"};
    private readonly ComboBox targetProfile=new(),monitor=new();
    private readonly CheckBox automatic=new(){Content="Profil otomasyonunu aç"},ruleEnabled=new(){Content="Bu kural açık"},layoutShortcut=new(){Content="Ctrl+Alt+L ile bu pencereyi aç"},mascotEnabled=new(){Content="Yerel maskotu göster"};
    private readonly Slider size=new(){Minimum=80,Maximum=240,Value=140,TickFrequency=10,IsSnapToTickEnabled=true};
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new(0,10,0,0)};
    private readonly List<Control> edits=[];
    private bool refreshing,closed,working;
    public AdvancedWindow(DesktopController controller)
    {
        this.controller=controller;Motion.Attach(this,controller.ViewModel,()=>controller.IsExiting);Title="Noxelyra · Profiller ve pencere düzenleri";Width=900;Height=850;MinWidth=720;MinHeight=600;WindowStartupLocation=WindowStartupLocation.CenterScreen;SetResourceReference(BackgroundProperty,"Canvas");SetResourceReference(ForegroundProperty,"Text");
        var root=new DockPanel{Margin=new(24)};Content=root;var heading=new TextBlock{Text="Çalışma alanını yönet",FontSize=27,FontWeight=FontWeights.SemiBold,Margin=new(0,0,0,16)};DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);DockPanel.SetDock(status,Dock.Bottom);root.Children.Add(status);root.Children.Add(tabs);
        ProfilesTab();RulesTab();WindowsTab();MascotTab();
        controller.AdvancedChanged+=Refresh;Closed+=(_,_)=>{closed=true;controller.AdvancedChanged-=Refresh;};
        PreviewKeyDown+=async(_,e)=>{if(tabs.SelectedIndex!=2||Keyboard.Modifiers!=ModifierKeys.Alt)return;var template=e.SystemKey switch{Key.D1=>"halves",Key.D2=>"thirds",Key.D3=>"grid",_=>null};if(template is not null){e.Handled=true;await Execute(()=>Arrange(template));}};
        Refresh();Loaded+=async(_,_)=>await RefreshWindows();
    }
    internal void SelectTab(int index)=>tabs.SelectedIndex=Math.Clamp(index,0,3);
    private StackPanel Tab(string title){var panel=new StackPanel{Margin=new(18)};tabs.Items.Add(new TabItem{Header=title,Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}});return panel;}
    private void Label(Panel panel,string text){panel.Children.Add(new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,Margin=new(0,10,0,5)});}
    private void Input(Panel panel,string label,Control field){Label(panel,label);panel.Children.Add(field);edits.Add(field);}
    private Button Button(Panel parent,string text,Func<Task> action,bool mutating=true){var b=new Button{Content=text,HorizontalAlignment=HorizontalAlignment.Left};b.Click+=async(_,_)=>{b.IsEnabled=false;await Execute(action);b.IsEnabled=!mutating||!controller.AdvancedReadOnly;RefreshWindowButtons();};parent.Children.Add(b);if(mutating)edits.Add(b);return b;}
    private async Task Execute(Func<Task> action){if(working){status.Text="Önceki işlemin tamamlanmasını bekle.";return;}working=true;foreach(var edit in edits)edit.IsEnabled=false;try{await action();if(!closed)status.Text=controller.ViewModel.Status;}catch(Exception ex)when(ex is IOException or InvalidOperationException or UnauthorizedAccessException or ArgumentException or Win32Exception or NotSupportedException){status.Text=ex is Win32Exception?"Windows bu pencereye erişim vermedi. Yetki düzeyi veya uygulama sınırı olabilir.":ex.Message;}finally{working=false;if(!closed)Refresh();}}
    private DesktopProfile SelectedProfile()=>profiles.SelectedItem as DesktopProfile??throw new InvalidOperationException("Önce profil seç.");
    private void ProfilesTab()
    {
        var p=Tab("Profiller");Label(p,"Tema + panel/dock + yerel widget düzeni. Görev çubuğu, başlangıç, wallpaper, web izinleri ve maskot tercihin değişmez.");p.Children.Add(profiles);
        var grid=new FrameworkElementFactory(typeof(UniformGrid));grid.SetValue(UniformGrid.ColumnsProperty,3);profiles.ItemsPanel=new ItemsPanelTemplate(grid);
        var card=new FrameworkElementFactory(typeof(StackPanel));var preview=new FrameworkElementFactory(typeof(Image));preview.SetValue(Image.HeightProperty,118d);preview.SetBinding(Image.SourceProperty,new Binding("."){Converter=new ProfilePreviewConverter()});card.AppendChild(preview);var name=new FrameworkElementFactory(typeof(TextBlock));name.SetBinding(TextBlock.TextProperty,new Binding("Name"));name.SetValue(TextBlock.FontSizeProperty,14d);name.SetValue(FrameworkElement.MarginProperty,new Thickness(0,10,0,0));card.AppendChild(name);profiles.ItemTemplate=new DataTemplate(typeof(DesktopProfile)){VisualTree=card};profiles.ToolTip="Şematik önizleme: ana ekrandaki yerel widget ve panel/dock düzeni";
        Button(p,"Seçili profili uygula · 30 dk manuel öncelik",async()=>{await controller.ApplyProfileAsync(SelectedProfile().Id);});
        Input(p,"Yeni profil adı",profileName);Button(p,"Mevcut görünüm ve düzeni kaydet",()=>controller.SaveProfileAsync(profileName.Text.Trim()));
        Button(p,"Seçili profili dışa aktar",async()=>{var profile=SelectedProfile();var picker=new SaveFileDialog{Title="Not metinleri de profile dahil edilir",Filter="Noxelyra profili|*.claxeprofile.json",FileName="Noxelyra.claxeprofile.json"};if(picker.ShowDialog(this)==true)await File.WriteAllTextAsync(picker.FileName,ProfileFiles.Export(profile));},false);
        Button(p,"Profil içe aktar",async()=>{var picker=new OpenFileDialog{Filter="Noxelyra profili|*.claxeprofile.json;*.json",CheckFileExists=true};if(picker.ShowDialog(this)!=true)return;if(new FileInfo(picker.FileName).Length>1048576)throw new InvalidDataException("Profil en fazla 1 MB olabilir.");var profile=ProfileFiles.Import(await File.ReadAllTextAsync(picker.FileName)) with{Id=Guid.NewGuid().ToString("N")};await controller.SaveBookAsync(controller.Profiles with{Profiles=[..controller.Profiles.Profiles,profile]});controller.ViewModel.Status="Profil içe aktarıldı; uygulamak için listeden seç.";});
        Button(p,"Seçili profili ve kurallarını sil",async()=>{var profile=SelectedProfile();if(controller.ViewModel.Settings.ActiveProfileId==profile.Id)throw new InvalidOperationException("Etkin profili silmeden önce başka bir profil uygula.");if(MessageBox.Show(this,"Profil ve bağlı kuralları silinsin mi?","Profil sil",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;await controller.SaveBookAsync(controller.Profiles with{Profiles=controller.Profiles.Profiles.Where(x=>x.Id!=profile.Id).ToList(),Rules=controller.Profiles.Rules.Where(r=>r.ProfileId!=profile.Id).ToList()});});
        Label(p,"Dışa aktarma ekran yuvalarını kullanır; kısayol/wallpaper dosyaları ve web paketleri taşınmaz. Not metinleri dahildir. Eksik ekran ana ekrana uyarlanır; kayıtlı profil korunur.");
    }
    private void RulesTab()
    {
        var p=Tab("Kurallar");p.Children.Add(automatic);edits.Add(automatic);automatic.Click+=async(_,_)=>{if(!refreshing)await Execute(()=>controller.SaveBookAsync(controller.Profiles with{AutomationEnabled=automatic.IsChecked==true}));};
        Label(p,"Koşullar birlikte sağlanır. Yüksek öncelik kazanır; eşitse listedeki ilk kural. 3 sn kararlılık, 30 sn geçiş aralığı; manuel seçim 30 dk korunur. Düzenleme pencereleri açıkken otomasyon bekler.");p.Children.Add(rules);
        rules.SelectionChanged+=(_,_)=>{if(refreshing||rules.SelectedItem is not ProfileRule r)return;ruleName.Text=r.Name;app.Text=r.Application??"";start.Text=r.StartMinute is{} s?$"{s/60:00}:{s%60:00}":"";end.Text=r.EndMinute is{} e?$"{e/60:00}:{e%60:00}":"";count.Text=r.MonitorCount?.ToString()??"";priority.Text=r.Priority.ToString();targetProfile.SelectedItem=controller.Profiles.Profiles.FirstOrDefault(x=>x.Id==r.ProfileId);ruleEnabled.IsChecked=r.Enabled;};
        Input(p,"Kural adı",ruleName);Input(p,"Hedef profil",targetProfile);Input(p,"Öndeki uygulama dosya adı (ör. game.exe), boşsa koşul yok",app);
        Input(p,"Başlangıç HH:mm (isteğe bağlı)",start);Input(p,"Bitiş HH:mm (gece yarısını aşabilir)",end);Input(p,"Bağlı ekran sayısı (isteğe bağlı)",count);Input(p,"Öncelik 0–100",priority);p.Children.Add(ruleEnabled);edits.Add(ruleEnabled);
        Button(p,"Yeni kural olarak kaydet",()=>SaveRule(false));Button(p,"Seçili kuralı güncelle",()=>SaveRule(true));
        Button(p,"Seçili kuralı sil",async()=>{if(rules.SelectedItem is not ProfileRule r)throw new InvalidOperationException("Kural seç.");await controller.SaveBookAsync(controller.Profiles with{Rules=controller.Profiles.Rules.Where(x=>x.Id!=r.Id).ToList()});});
        Button(p,"Manuel önceliği kaldır / otomasyona dön",controller.ResumeAutomationAsync);
    }
    private static int? Minute(string text){if(string.IsNullOrWhiteSpace(text))return null;if(!TimeSpan.TryParseExact(text.Trim(),"hh\\:mm",CultureInfo.InvariantCulture,out var time)||time.TotalHours>=24)throw new InvalidDataException("Saat HH:mm biçiminde olmalı.");return (int)time.TotalMinutes;}
    private async Task SaveRule(bool replace)
    {
        var selected=rules.SelectedItem as ProfileRule;if(replace&&selected is null)throw new InvalidOperationException("Güncellenecek kuralı seç.");if(targetProfile.SelectedItem is not DesktopProfile profile)throw new InvalidOperationException("Hedef profil seç.");
        if(!int.TryParse(priority.Text,out var weight))throw new InvalidDataException("Öncelik sayı olmalı.");int? monitors=null;if(!string.IsNullOrWhiteSpace(count.Text)){if(!int.TryParse(count.Text,out var value))throw new InvalidDataException("Ekran sayısı sayı olmalı.");monitors=value;}
        var rule=new ProfileRule{Id=replace?selected!.Id:Guid.NewGuid().ToString("N"),Name=ruleName.Text.Trim(),ProfileId=profile.Id,Enabled=ruleEnabled.IsChecked==true,Priority=weight,Application=string.IsNullOrWhiteSpace(app.Text)?null:app.Text.Trim(),StartMinute=Minute(start.Text),EndMinute=Minute(end.Text),MonitorCount=monitors};rule.Validate();
        var list=controller.Profiles.Rules.ToList();if(replace)list[list.FindIndex(r=>r.Id==rule.Id)]=rule;else list.Add(rule);await controller.SaveBookAsync(controller.Profiles with{Rules=list});controller.ViewModel.Status="Kural kaydedildi. Otomasyon açık ve düzenleyiciler kapalıyken çalışır.";
    }
    private void WindowsTab()
    {
        var p=Tab("Pencere düzenleri");Label(p,"Ctrl/Shift ile 1–4 pencere seç. Seçilenler liste sırasıyla kullanılabilir ekran alanına yerleşir. Kapalı, sabit boyutlu veya erişilemeyen pencereler açık hata verir.");p.Children.Add(windows);windows.DisplayMemberPath="Title";monitor.ItemsSource=controller.ViewModel.Monitors;monitor.SelectedItem=controller.ViewModel.Monitors.First(m=>m.Primary);Input(p,"Hedef ekran",monitor);
        Button(p,"Açık pencereleri yenile",RefreshWindows,false);var templates=new WrapPanel();p.Children.Add(templates);layoutButtons.Add((Button(templates,"İkili · Alt+1",()=>Arrange("halves")),2));layoutButtons.Add((Button(templates,"Üç sütun · Alt+2",()=>Arrange("thirds")),3));layoutButtons.Add((Button(templates,"2×2 · Alt+3",()=>Arrange("grid")),4));windows.SelectionChanged+=(_,_)=>RefreshWindowButtons();Button(p,"Son yerleşimden önceki konumlara dön",()=>{controller.RestoreWindowLayout();return Task.CompletedTask;});
        p.Children.Add(layoutShortcut);edits.Add(layoutShortcut);layoutShortcut.Click+=async(_,_)=>await Execute(async()=>{await controller.ConfigureLayoutShortcutAsync(layoutShortcut.IsChecked==true);layoutShortcut.IsChecked=controller.ViewModel.Settings.LayoutShortcut;});
        Label(p,"Ctrl+Alt+L yalnız düzenleyiciyi açar; arka plandaki uygulamaları kendiliğinden taşımaz. Geri alma bu Noxelyra oturumundaki son başarılı yerleşim içindir.");
    }
    private async Task RefreshWindows(){var found=await controller.LayoutWindowsAsync();if(!closed){windows.ItemsSource=found;if(found.Count==0)status.Text="Düzenlenebilir açık pencere bulunamadı.";RefreshWindowButtons();}}
    private void RefreshWindowButtons(){foreach(var entry in layoutButtons)entry.Button.IsEnabled=!working&&!controller.AdvancedReadOnly&&windows.SelectedItems.Count>0&&windows.SelectedItems.Count<=entry.Capacity;}
    private async Task Arrange(string template){if(monitor.SelectedItem is not MonitorInfo screen)throw new InvalidOperationException("Ekran seç.");await controller.ArrangeWindowsAsync(windows.Items.Cast<AppWindow>().Where(w=>windows.SelectedItems.Contains(w)).ToArray(),screen,template);}
    private void MascotTab()
    {
        var p=Tab("Yerel maskot");Label(p,"Kendi yerel PNG/JPG görselini kullan. Seçtiğin görsel bu PC'ye kopyalanır; dağıtım lisansı verilmez. Özellik kapalı başlar ve tam ekranda/kilitte gizlenir.");
        p.Children.Add(mascotEnabled);edits.Add(mascotEnabled);mascotEnabled.Click+=async(_,_)=>await Execute(async()=>{await controller.ConfigureMascotAsync(mascotEnabled.IsChecked==true);mascotEnabled.IsChecked=controller.ViewModel.Settings.Mascot.Enabled;});
        Button(p,"Özgün Nox ay maskotunu kullan",controller.UseOriginalMascotAsync);
        Button(p,"Ana görseli seç ve aç",async()=>{var file=ImagePicker();if(file is not null)await controller.ConfigureMascotAsync(true,imageFile:file);});
        Button(p,"Tıklama için alternatif ifade görseli seç",async()=>{if(controller.ViewModel.Settings.Mascot.ImagePath is null)throw new InvalidOperationException("Önce ana görseli seç.");var file=ImagePicker();if(file is not null)await controller.ConfigureMascotAsync(controller.ViewModel.Settings.Mascot.Enabled,happyFile:file);});
        Input(p,"Boyut (80–240 DIP)",size);Button(p,"Boyutu uygula",()=>controller.ConfigureMascotAsync(controller.ViewModel.Settings.Mascot.Enabled,size:(int)size.Value));
        Label(p,"Üst tutamaktan sürükle. Tıklayınca seçtiğin alternatif ifade kısa süre gösterilir. Hafif hareket genel animasyon tercihine uyar; pilde, azaltılmış harekette ve gizliyken durur. GIF/video animasyonu desteklenmez.");
    }
    private string? ImagePicker(){var picker=new OpenFileDialog{Filter="Görsel|*.png;*.jpg;*.jpeg",CheckFileExists=true};return picker.ShowDialog(this)==true?picker.FileName:null;}
    private void Refresh()
    {
        refreshing=true;var id=(profiles.SelectedItem as DesktopProfile)?.Id??controller.ViewModel.Settings.ActiveProfileId;var ruleId=(rules.SelectedItem as ProfileRule)?.Id;var targetId=(targetProfile.SelectedItem as DesktopProfile)?.Id;profiles.ItemsSource=controller.Profiles.Profiles;profiles.SelectedItem=controller.Profiles.Profiles.FirstOrDefault(p=>p.Id==id)??controller.Profiles.Profiles.FirstOrDefault();rules.ItemsSource=controller.Profiles.Rules;rules.SelectedItem=controller.Profiles.Rules.FirstOrDefault(r=>r.Id==ruleId);targetProfile.ItemsSource=controller.Profiles.Profiles;targetProfile.SelectedItem=controller.Profiles.Profiles.FirstOrDefault(p=>p.Id==((rules.SelectedItem as ProfileRule)?.ProfileId??targetId))??controller.Profiles.Profiles.FirstOrDefault();automatic.IsChecked=controller.Profiles.AutomationEnabled;layoutShortcut.IsChecked=controller.ViewModel.Settings.LayoutShortcut;mascotEnabled.IsChecked=controller.ViewModel.Settings.Mascot.Enabled;size.Value=controller.ViewModel.Settings.Mascot.Size;foreach(var field in edits)field.IsEnabled=!working&&!controller.AdvancedReadOnly;refreshing=false;RefreshWindowButtons();
    }
}




