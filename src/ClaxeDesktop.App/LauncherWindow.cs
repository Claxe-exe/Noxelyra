using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;
internal sealed class LauncherWindow : Window
{
    private readonly DesktopController controller;
    private readonly TextBox search=new(){FontSize=23,Margin=new(0,16,0,12)};
    private readonly ListBox results=new(){MaxHeight=490};
    private readonly TextBlock status=new(){Opacity=.65,Margin=new(0,12,0,0)};
    private readonly DispatcherTimer debounce=new(){Interval=TimeSpan.FromMilliseconds(150)};
    private readonly ShellIconService icons=new();
    private readonly CancellationTokenSource lifetime=new();
    private IReadOnlyList<Shortcut> apps=[];
    private bool grid,closed;
    private int generation;
    internal int ApplicationCount=>apps.Count;
    internal IReadOnlyList<Shortcut> InstalledApplications=>apps;
    internal TextBox Search=>search;
    internal ListBox Results=>results;
    public LauncherWindow(DesktopController controller)
    {
        this.controller=controller;Title="Noxelyra · Uygulamalar";Width=760;Height=720;MinHeight=480;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty,"Canvas");SetResourceReference(ForegroundProperty,"Text");
        var root=new DockPanel{Margin=new(28)};Content=root;
        var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
        top.Children.Add(new TextBlock{Text="C  NOXELYRA",FontSize=25,FontWeight=FontWeights.Bold});
        top.Children.Add(new TextBlock{Text="Windows uygulamaların ve masaüstün",Opacity=.6,Margin=new(0,8,0,0)});top.Children.Add(search);
        System.Windows.Automation.AutomationProperties.SetName(search,"Uygulama veya Noxelyra eylemi ara");
        var actions=new WrapPanel();top.Children.Add(actions);
        AddAction(actions,"Kontrol merkezi",()=>{controller.ShowControls();Hide();});AddAction(actions,"Widget'lar",()=>{controller.Widgets.ShowEditor();Hide();});AddAction(actions,"Ayarlar",()=>{controller.ShowSettings();Hide();});
        AddAction(actions,"Liste / ızgara",()=>{grid=!grid;Render();});AddAction(actions,"Yenile",()=>_=LoadAsync());
        AddAction(actions,"Windows uygulamaları",()=>controller.OpenSystem("apps"));
        DockPanel.SetDock(status,Dock.Bottom);root.Children.Add(status);root.Children.Add(results);
        search.TextChanged+=(_,_)=>{debounce.Stop();debounce.Start();};debounce.Tick+=(_,_)=>{debounce.Stop();Render();};
        results.MouseDoubleClick+=(_,_)=>RunSelected();
        search.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Down){results.Focus();if(results.Items.Count>0)results.SelectedIndex=0;e.Handled=true;}else if(e.Key==Key.Enter){RunSelected();e.Handled=true;}};
        results.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){RunSelected();e.Handled=true;}};
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){Hide();e.Handled=true;}};
        Loaded+=async(_,_)=>{search.Focus();await LoadAsync();};Activated+=(_,_)=>search.Focus();
        Closed+=(_,_)=>{closed=true;lifetime.Cancel();debounce.Stop();icons.Dispose();lifetime.Dispose();};
    }
    private static void AddAction(Panel parent,string label,Action action){var button=new Button{Content=label,Padding=new(10,7,10,7)};button.Click+=(_,_)=>action();parent.Children.Add(button);}
    private async Task LoadAsync()
    {
        status.Text="Windows Başlat menüsü okunuyor…";
        try{var found=await Task.Run(()=>new WindowsApplicationIndex().Read(lifetime.Token),lifetime.Token);if(!closed){apps=found;Render();}}
        catch(OperationCanceledException){}
        catch(Exception ex){if(!closed)status.Text="Uygulama dizini okunamadı. Sabitlenmiş kısayollar kullanılabilir.";controller.Log.Write("application-index-failed",ex);}
    }
    internal void Render()
    {
        if(closed)return;var current=++generation;results.Items.Clear();
        var factory=new FrameworkElementFactory(grid?typeof(UniformGrid):typeof(StackPanel));if(grid)factory.SetValue(UniformGrid.ColumnsProperty,4);results.ItemsPanel=new ItemsPanelTemplate(factory);
        var all=controller.ViewModel.Shortcuts.Concat(apps).DistinctBy(a=>a.Target,StringComparer.OrdinalIgnoreCase);
        var query=search.Text.Trim();var chosen=all.Where(a=>CultureInfo.GetCultureInfo("tr-TR").CompareInfo.IndexOf(a.Name,query,CompareOptions.IgnoreCase|CompareOptions.IgnoreNonSpace)>=0).Take(60).ToArray();
        foreach(var app in chosen)
        {
            var row=new StackPanel{Orientation=grid?Orientation.Vertical:Orientation.Horizontal,Margin=new(8)};
            var icon=new Image{Width=28,Height=28,Margin=new(0,0,12,0)};row.Children.Add(icon);row.Children.Add(new TextBlock{Text=app.Name,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center,MaxWidth=grid?130:570});
            var item=new ListBoxItem{Content=row,Tag=app,ToolTip=app.Name,MinHeight=grid?100:50,HorizontalContentAlignment=HorizontalAlignment.Stretch};results.Items.Add(item);
            _=SetIconAsync(icon,app.Target,current);
        }
        if(results.Items.Count>0)results.SelectedIndex=0;
        status.Text=chosen.Length==0?"Eşleşme yok. Windows uygulamaları düğmesinden tüm uygulamaları açabilirsin.":$"{apps.Count} Başlat menüsü kısayolu · {chosen.Length} sonuç · Enter ile aç · Esc ile kapat";
    }
    private async Task SetIconAsync(Image image,string path,int current){var asset=await icons.GetAsync(path);if(!closed&&generation==current)image.Source=asset.Icon;}
    internal void RunSelected(){if(results.SelectedItem is ListBoxItem{Tag:Shortcut app}){controller.Launch(app);Hide();}}
}

