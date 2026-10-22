using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;

namespace ClaxeDesktop.App;
public class FloatingWindow : Window
{
    public string Kind { get; }
    public event Action<FloatingWindow>? Moved;
    public bool IsDragging { get; private set; }
    protected Border Frame { get; }
    private int gridSize = 12;
    protected virtual bool CanMove => true;
    /// <summary>True when Windows draws real acrylic glass behind this surface.</summary>
    protected bool Backdrop { get; private set; }
    private DesktopViewModel? model;
    public FloatingWindow(string kind, bool glass = false)
    {
        Kind = kind; Title = "Noxelyra · " + kind; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false;
        Frame = new Border { CornerRadius = new(16), BorderThickness = new(1), BorderBrush = (Brush)FindResource("Line"), Padding = new(16) };
        Content = Frame;
        if (glass) Glass.Prepare(this, BackdropKind.Acrylic, true, ok => { Backdrop = ok; if (model is not null) ApplyAppearance(model); });
        Loaded += (_, _) => Enter();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Keyboard.ClearFocus();
            if (CanMove && Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey is Key.Left or Key.Right or Key.Up or Key.Down)
            {
                var hwnd = new WindowInteropHelper(this).Handle; var p = WindowPlacement.Position(hwnd);
                var step = (int)(gridSize * VisualTreeHelper.GetDpi(this).DpiScaleX);
                WindowPlacement.Move(hwnd, p.X + (e.SystemKey == Key.Left ? -step : e.SystemKey == Key.Right ? step : 0), p.Y + (e.SystemKey == Key.Up ? -step : e.SystemKey == Key.Down ? step : 0));
                Moved?.Invoke(this); e.Handled = true;
            }
        };
    }
    protected void MakeDragHandle(UIElement element)
    {
        element.MouseLeftButtonDown += (_, e) => { if (CanMove && e.ClickCount == 1) { IsDragging = true; try { DragMove(); Moved?.Invoke(this); } finally { IsDragging = false; } e.Handled = true; } };
        if (element is FrameworkElement fe) { fe.Focusable = true; fe.Cursor = Cursors.SizeAll; fe.ToolTip = "Sürükle veya odaklandıktan sonra Alt + yön tuşlarıyla taşı"; }
    }
    public virtual void ApplyAppearance(DesktopViewModel vm)
    {
        model = vm;
        gridSize = vm.Settings.GridSize;
        var palette = ThemePresets.Resolve(vm.Settings);
        var color = (Color)ColorConverter.ConvertFromString(palette.Surface);
        color.A = vm.Settings.Material == "flat" ? (byte)255 : (byte)(vm.SurfaceOpacity * 255);
        // Over real acrylic the surface is only a tint; Windows rounds the corners and draws the outline.
        if (Backdrop && vm.Settings.Material != "flat") color.A = (byte)(vm.SurfaceOpacity * 120);
        Frame.Background = SystemParameters.HighContrast ? SystemColors.WindowBrush : new SolidColorBrush(color);
        Frame.CornerRadius = new(Backdrop ? 0 : vm.Settings.CornerRadius); Frame.BorderThickness = new(Backdrop ? 0 : vm.Settings.BorderWidth);
        Frame.SetResourceReference(Border.BorderBrushProperty, vm.Settings.Material == "neon" ? "Accent" : "Line");
        if (Backdrop) SystemBackdrop.Border(new WindowInteropHelper(this).Handle, vm.Settings.Material == "neon" ? palette.Accent : palette.Line);
        Frame.Effect = !Backdrop && vm.Settings.Material == "neon" && !SystemParameters.HighContrast ? new DropShadowEffect { Color = (Color)ColorConverter.ConvertFromString(palette.Accent), BlurRadius = 12, ShadowDepth = 0, Opacity = .32 } : null;
    }
    /// <summary>Soft rise-and-fade when a surface appears; skipped when motion is off or reduced.</summary>
    protected void Enter(double rise = 10)
    {
        if (model is null || !Motion.Enabled(model)) return;
        var shift = new TranslateTransform(0, rise); Frame.RenderTransform = shift; Frame.Opacity = 0;
        Motion.To(Frame, OpacityProperty, 1, 260); Motion.To(shift, TranslateTransform.YProperty, 0, 340);
    }
    public virtual void Place(Placement? placement, IReadOnlyList<MonitorInfo> monitors, bool snap)
    {
        var main = monitors.FirstOrDefault(m => m.Id == placement?.MonitorId) ?? monitors.First(m => m.Primary);
        var x = Kind == "Dock" ? (main.Width / main.Scale - Width) / 2 : 24;
        var y = Kind == "Dock" ? main.Height / main.Scale - Height - 24 : Kind == "Widget" ? 106 : 18;
        var resolved = LayoutEngine.Resolve(placement, monitors, Width, Height, x, y, snap, gridSize);
        var hwnd = new WindowInteropHelper(this).Handle;
        WindowPlacement.Move(hwnd, resolved.Monitor.X + (int)(resolved.X * resolved.Monitor.Scale), resolved.Monitor.Y + (int)(resolved.Y * resolved.Monitor.Scale));
    }
    public Placement Capture(IReadOnlyList<MonitorInfo> monitors)
    {
        var p = WindowPlacement.Position(new WindowInteropHelper(this).Handle);
        var monitor = monitors.FirstOrDefault(m => p.X >= m.X && p.X < m.X + m.Width && p.Y >= m.Y && p.Y < m.Y + m.Height) ?? monitors.First(m => m.Primary);
        return new(monitor.Id, (p.X - monitor.X) / monitor.Scale, (p.Y - monitor.Y) / monitor.Scale);
    }
}
