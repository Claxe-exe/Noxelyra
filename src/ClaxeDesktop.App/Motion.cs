using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace ClaxeDesktop.App;

/// <summary>One motion vocabulary for every surface: decelerating ease, short durations, and a single off switch.</summary>
internal static class Motion
{
    private static readonly IEasingFunction Ease = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 5 };
    /// <summary>Respects the app setting, Windows "animation effects" and high contrast.</summary>
    internal static bool Enabled(DesktopViewModel model) => model.Settings.AnimationsEnabled && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
    internal static void To(IAnimatable target, DependencyProperty property, double value, int milliseconds, Action? completed = null, int delay = 0)
    {
        var animation = new DoubleAnimation(value, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = Ease, BeginTime = TimeSpan.FromMilliseconds(delay) };
        if (completed is not null) animation.Completed += (_, _) => completed();
        target.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }
    /// <summary>App windows open with a short fade, rise and settle, and close faster than they open (about 60%).</summary>
    internal static void Attach(Window window, DesktopViewModel model, Func<bool> exiting)
    {
        var allowClose = false; var leaving = false;
        window.Loaded += (_, _) => Arrive(window, model);
        window.IsVisibleChanged += (_, e) => { if ((bool)e.NewValue && window.IsLoaded) Arrive(window, model); };
        window.Closing += (_, e) =>
        {
            if (allowClose || e.Cancel || exiting() || !Enabled(model) || window.Content is not FrameworkElement) return;
            e.Cancel = true; if (leaving) return; leaving = true;
            Depart(window, () => { allowClose = true; window.Close(); });
        };
    }
    /// <summary>Animated replacement for Hide() on windows that are reused (launcher).</summary>
    internal static void Leave(Window window, DesktopViewModel model)
    {
        if (!Enabled(model) || !window.IsVisible) { window.Hide(); return; }
        Depart(window, window.Hide);
    }
    private static (FrameworkElement Root, ScaleTransform Scale, TranslateTransform Shift)? Parts(Window window)
    {
        if (window.Content is not FrameworkElement root) return null;
        if (root.RenderTransform is not TransformGroup { Children: [ScaleTransform scale, TranslateTransform shift] })
        { scale = new(1, 1); shift = new(); root.RenderTransform = new TransformGroup { Children = { scale, shift } }; root.RenderTransformOrigin = new(.5, .5); }
        else { scale = (ScaleTransform)((TransformGroup)root.RenderTransform).Children[0]; shift = (TranslateTransform)((TransformGroup)root.RenderTransform).Children[1]; }
        return (root, scale, shift);
    }
    private static void Arrive(Window window, DesktopViewModel model)
    {
        if (Parts(window) is not var (root, scale, shift)) return;
        if (!Enabled(model)) { root.BeginAnimation(UIElement.OpacityProperty, null); root.Opacity = 1; return; }
        root.Opacity = 0; scale.ScaleX = scale.ScaleY = .97; shift.Y = 10;
        To(root, UIElement.OpacityProperty, 1, 240); To(scale, ScaleTransform.ScaleXProperty, 1, 320); To(scale, ScaleTransform.ScaleYProperty, 1, 320); To(shift, TranslateTransform.YProperty, 0, 320);
    }
    private static void Depart(Window window, Action done)
    {
        if (Parts(window) is not var (root, scale, shift)) { done(); return; }
        To(scale, ScaleTransform.ScaleXProperty, .98, 150); To(scale, ScaleTransform.ScaleYProperty, .98, 150); To(shift, TranslateTransform.YProperty, 6, 150);
        To(root, UIElement.OpacityProperty, 0, 150, () => { done(); root.BeginAnimation(UIElement.OpacityProperty, null); root.Opacity = 1; scale.BeginAnimation(ScaleTransform.ScaleXProperty, null); scale.BeginAnimation(ScaleTransform.ScaleYProperty, null); shift.BeginAnimation(TranslateTransform.YProperty, null); });
    }
}
