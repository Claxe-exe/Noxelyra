using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

/// <summary>The month calendar under the bar's clock: click the clock to open it, click anywhere else or press Esc to close.</summary>
internal sealed class CalendarFlyout : Window
{
    private const string IconFont = "Segoe Fluent Icons, Segoe MDL2 Assets";
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private readonly UniformGrid days = new() { Columns = 7, Rows = 6 };
    private readonly TextBlock title = new() { FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly FrameworkElement anchor;
    private DateOnly month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    internal CalendarFlyout(DesktopController controller, FrameworkElement anchor)
    {
        this.anchor = anchor;
        AltTab.Hide(this); Title = "Noxelyra · Takvim"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true; SizeToContent = SizeToContent.WidthAndHeight;
        SetResourceReference(BackgroundProperty, "Surface"); SetResourceReference(ForegroundProperty, "Text");
        SourceInitialized += (_, _) => { var handle = new WindowInteropHelper(this).Handle; SystemBackdrop.Round(handle); SystemBackdrop.Border(handle, ThemePresets.Resolve(controller.ViewModel.Settings).Line); };
        var root = new StackPanel { Margin = new(16), Width = 308 }; Content = root;
        var today = DateTime.Today;
        root.Children.Add(new TextBlock { Text = today.ToString("dddd", Turkish), FontSize = 12 });
        root.Children.Add(new TextBlock { Text = today.ToString("d MMMM yyyy", Turkish), FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new(0, 0, 0, 12) });
        var head = new DockPanel { Margin = new(0, 0, 0, 8) };
        var next = Arrow("\uE76C", "Sonraki ay", 1); DockPanel.SetDock(next, Dock.Right); head.Children.Add(next);
        var previous = Arrow("\uE76B", "Önceki ay", -1); DockPanel.SetDock(previous, Dock.Right); head.Children.Add(previous);
        var back = new Button { Content = "Bugün", Padding = new(10, 3, 10, 3), FontSize = 11, Margin = new(0, 0, 6, 0) }; back.Click += (_, _) => { month = new(DateTime.Today.Year, DateTime.Today.Month, 1); Render(); }; DockPanel.SetDock(back, Dock.Right); head.Children.Add(back);
        head.Children.Add(title); root.Children.Add(head);
        var names = new UniformGrid { Columns = 7, Margin = new(0, 0, 0, 4) };
        foreach (var name in new[] { "Pt", "Sa", "Ça", "Pe", "Cu", "Ct", "Pa" }) { var label = new TextBlock { Text = name, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center }; label.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); names.Children.Add(label); }
        root.Children.Add(names); root.Children.Add(days);
        var foot = new DockPanel { Margin = new(0, 10, 0, 0) };
        var settings = new Button { Content = "Tarih ve saat ayarları", Padding = new(10, 4, 10, 4), FontSize = 11 };
        settings.Click += (_, _) => { Close(); try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:dateandtime") { UseShellExecute = true }); } catch (System.ComponentModel.Win32Exception ex) { controller.Log.Write("date-settings-failed", ex); } };
        var card = new Button { Content = "Ada kartı  ·  Alt+0", Padding = new(10, 4, 10, 4), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right };
        card.Click += (_, _) => { Close(); controller.ShowIslandCard(anchor); };
        DockPanel.SetDock(settings, Dock.Left); foot.Children.Add(settings); foot.Children.Add(card); root.Children.Add(foot);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            else if (e.Key == Key.PageUp) { Step(-1); e.Handled = true; }
            else if (e.Key == Key.PageDown) { Step(1); e.Handled = true; }
        };
        PreviewMouseWheel += (_, e) => { Step(e.Delta > 0 ? -1 : 1); e.Handled = true; };
        Deactivated += (_, _) => { if (IsVisible) Close(); };
        Render();
    }
    private Button Arrow(string glyph, string name, int step)
    {
        var button = new Button { Content = new TextBlock { Text = glyph, FontFamily = new FontFamily(IconFont), FontSize = 11 }, Padding = new(9, 5, 9, 5), Margin = new(2, 0, 0, 0), ToolTip = name };
        System.Windows.Automation.AutomationProperties.SetName(button, name); button.Click += (_, _) => Step(step); return button;
    }
    private void Step(int months) { month = month.AddMonths(months); Render(); }
    private void Render()
    {
        title.Text = month.ToDateTime(TimeOnly.MinValue).ToString("MMMM yyyy", Turkish);
        days.Children.Clear(); var today = DateOnly.FromDateTime(DateTime.Today);
        foreach (var day in MonthGrid.For(month.Year, month.Month))
        {
            var number = new TextBlock { Text = day.Day.ToString(Turkish), FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var cell = new Border { Width = 34, Height = 34, CornerRadius = new(17), Child = number, Margin = new(4, 1, 4, 1) };
            if (day == today) { cell.SetResourceReference(Border.BackgroundProperty, "Accent"); number.SetResourceReference(TextBlock.ForegroundProperty, "Canvas"); number.FontWeight = FontWeights.SemiBold; }
            else number.SetResourceReference(TextBlock.ForegroundProperty, day.Month == month.Month ? "Text" : "Muted");
            if (day.Month != month.Month) number.Opacity = .55;
            days.Children.Add(cell);
        }
    }
    /// <summary>Opens centred under the clock and inside the monitor.</summary>
    internal void Open()
    {
        Show(); UpdateLayout();
        var handle = new WindowInteropHelper(this).Handle;
        if (WindowOps.Read(handle) is { } self)
        {
            var scale = VisualTreeHelper.GetDpi(anchor).DpiScaleX; var corner = anchor.PointToScreen(new Point(0, 0));
            var screen = WindowOps.MonitorBounds(WindowOps.Read(new WindowInteropHelper(GetWindow(anchor)).Handle)?.Monitor ?? self.Monitor);
            var x = corner.X + anchor.ActualWidth * scale / 2 - self.Bounds.Width / 2; var y = corner.Y + anchor.ActualHeight * scale + 16 * scale;
            WindowPlacement.Move(handle, (int)Math.Clamp(x, screen.X + 8, Math.Max(screen.X + 8, screen.X + screen.Width - self.Bounds.Width - 8)), (int)Math.Clamp(y, screen.Y, Math.Max(screen.Y, screen.Y + screen.Height - self.Bounds.Height - 8)));
        }
        Activate();
    }
}
