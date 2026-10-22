using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;
internal sealed class ProfilePreviewConverter:IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)
    {
        if(value is not DesktopProfile p)return DependencyProperty.UnsetValue;var palette=ThemePresets.Resolve(p.Appearance.ApplyTo(new()));
        Brush Color(string text)=>new SolidColorBrush((Color)ColorConverter.ConvertFromString(text));
        var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen())
        {
            dc.PushClip(new RectangleGeometry(new Rect(0,0,210,118)));dc.DrawRoundedRectangle(Color(palette.Canvas),new Pen(Color(palette.Line),1),new(0,0,210,118),8,8);
            void Box(double x,double y,double w,double h){dc.DrawRoundedRectangle(Color(palette.Surface),new Pen(Color(palette.Accent),.7),new(Math.Clamp(x/1920*210,0,208),Math.Clamp(y/1080*118,0,116),Math.Max(3,w/1920*210),Math.Max(3,h/1080*118)),3,3);}
            Box(p.Panel?.X??24,p.Panel?.Y??18,p.PanelWidth,p.PanelHeight);
            foreach(var widget in p.NativeWidgets.Where(w=>w.Visible&&(w.Placement is null||w.Placement.MonitorId=="screen-0")).OrderBy(w=>w.Layer))Box(widget.Placement?.X??24,widget.Placement?.Y??106,widget.Width,widget.Height);
            Box(p.Dock?.X??720,p.DockEdge=="top"?80:p.Dock?.Y??970,480,p.IconSize+26);dc.Pop();
        }
        var bitmap=new RenderTargetBitmap(210,118,96,96,PixelFormats.Pbgra32);bitmap.Render(drawing);bitmap.Freeze();return bitmap;
    }
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
