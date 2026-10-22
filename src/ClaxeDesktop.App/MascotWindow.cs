using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;
internal sealed class MascotWindow:FloatingWindow
{
    private readonly Image image=new(){Stretch=Stretch.Uniform};private readonly TranslateTransform motion=new();
    private BitmapSource? idle,happy;private bool moving;private DateTime happyUntil;
    public MascotWindow(Action hide):base("Mascot")
    {
        Title="Noxelyra · Yerel maskot";var dock=new DockPanel();var handle=new TextBlock{Text="⠿",FontSize=17,HorizontalAlignment=HorizontalAlignment.Center};MakeDragHandle(handle);DockPanel.SetDock(handle,Dock.Top);dock.Children.Add(handle);image.RenderTransform=motion;image.Margin=new(4,8,4,8);dock.Children.Add(image);Content=dock;
        image.MouseLeftButtonDown+=(_,_)=>{happyUntil=DateTime.UtcNow.AddSeconds(2);if(happy is not null)image.Source=happy;};
        var menu=new ContextMenu();var close=new MenuItem{Header="Maskotu kapat"};close.Click+=(_,_)=>hide();menu.Items.Add(close);ContextMenu=menu;ToolTip="Üst tutamaktan taşı · Tıkla: alternatif görsel · Sağ tık: kapat";
        Closed+=(_,_)=>Animate(false);
    }
    private static BitmapSource Read(string path){var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=300;bitmap.UriSource=new Uri(path);bitmap.EndInit();bitmap.Freeze();return bitmap;}
    public void Load(MascotOptions value){Width=value.Size;Height=value.Size+30;idle=Read(value.ImagePath!);happy=value.HappyImagePath is null?null:Read(value.HappyImagePath);image.Source=idle;}
    public void Activity(bool visible,bool animate){if(visible){if(!IsVisible)Show();image.Source=DateTime.UtcNow<happyUntil&&happy is not null?happy:idle;}else if(IsVisible)Hide();Animate(visible&&animate);}
    private void Animate(bool enabled){if(moving==enabled)return;moving=enabled;motion.BeginAnimation(TranslateTransform.YProperty,enabled?new DoubleAnimation(-3,3,TimeSpan.FromSeconds(2)){AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever}:null);if(!enabled)motion.Y=0;}
    public override void Place(Placement? p,IReadOnlyList<MonitorInfo> monitors,bool snap){var m=monitors.FirstOrDefault(x=>x.Id==p?.MonitorId)??monitors.First(x=>x.Primary);base.Place(p??new(m.Id,Math.Max(0,m.Width/m.Scale-Width-30),Math.Max(0,m.Height/m.Scale-Height-100)),monitors,snap);}
}
