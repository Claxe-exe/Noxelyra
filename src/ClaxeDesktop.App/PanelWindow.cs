using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ClaxeDesktop.App;
public sealed class PanelWindow : FloatingWindow
{
    private readonly TextBlock clock;
    internal bool Integrated { get; set; }
    protected override bool CanMove => !Integrated;
    public PanelWindow(DesktopViewModel vm, Action settings, Action exit, Action? launcher = null, Action? controls = null) : base("Panel")
    {
        DataContext = vm; Width = vm.PanelWidth; Height = vm.PanelHeight; Frame.Padding = new(10, 3, 10, 3);
        var row = new DockPanel { LastChildFill = true };
        var brand = new Button { Content = "N   NOXELYRA", FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Accent"), Padding = new(12, 3, 12, 3), ToolTip = "Uygulama ara" }; brand.Click += (_, _) => (launcher ?? settings)();
        brand.SetResourceReference(Button.ForegroundProperty, "Accent");
        DockPanel.SetDock(brand, Dock.Left); row.Children.Add(brand);
        var close = new Button { Content = "⏻", Padding = new(10, 3, 10, 3), ToolTip = "Noxelyra'dan çık" }; close.Click += (_, _) => exit(); DockPanel.SetDock(close, Dock.Right); row.Children.Add(close);
        var time = new TextBlock { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new(18, 0, 12, 0) }; time.SetBinding(TextBlock.TextProperty, new Binding("Time")); DockPanel.SetDock(time, Dock.Right); row.Children.Add(time);
        clock = time;
        if(controls is not null) { var control = new Button { Content = "☷", Padding = new(12,3,12,3), ToolTip = "Ses, medya ve masaüstü kontrolleri" };control.Click+=(_,_)=>controls();DockPanel.SetDock(control,Dock.Right);row.Children.Add(control); }
        var metrics = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("Muted") };
        metrics.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        var binding = new MultiBinding { StringFormat = "CPU {0}     RAM {1}" }; binding.Bindings.Add(new Binding("CpuText")); binding.Bindings.Add(new Binding("MemoryText")); metrics.SetBinding(TextBlock.TextProperty, binding); DockPanel.SetDock(metrics, Dock.Right); row.Children.Add(metrics);
        var center = new TextBlock { Text = "MIDNIGHT NOXELYRA    ·    ⠿", TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("Muted") }; MakeDragHandle(center); row.Children.Add(center); Frame.Child = row;
        center.SetBinding(TextBlock.TextProperty, new Binding("ThemeName") { StringFormat = "{0}    ·    ⠿" }); center.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        var menu = new ContextMenu(); var config = new MenuItem { Header = "Panel ve dock ayarları" }; config.Click += (_, _) => settings(); menu.Items.Add(config); var quit = new MenuItem { Header = "Noxelyra'dan çık" }; quit.Click += (_, _) => exit(); menu.Items.Add(quit); ContextMenu = menu;
    }
    public void UpdateOrder(bool left) => DockPanel.SetDock(clock, left ? Dock.Left : Dock.Right);
}

