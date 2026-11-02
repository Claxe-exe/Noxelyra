using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;

/// <summary>
/// Clock text whose changed digits slide up. On the bar the seconds change without animation: the bar is a layered window and a
/// roll every second would repaint all of it for no gain. Without motion it behaves like plain text.
/// </summary>
internal sealed class RollingText : StackPanel
{
    private const int Duration = 340;
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(RollingText), new PropertyMetadata("", (d, e) => ((RollingText)d).Change((string)e.OldValue, (string)e.NewValue)));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    private readonly Func<bool> animated;
    private readonly bool rollSeconds;
    private readonly FontWeight weight;
    private readonly double? size;
    internal RollingText(Func<bool> animated, bool rollSeconds, FontWeight weight, double? size = null)
    {
        this.animated = animated; this.rollSeconds = rollSeconds; this.weight = weight; this.size = size;
        Orientation = Orientation.Horizontal; VerticalAlignment = VerticalAlignment.Center;
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == TextProperty) System.Windows.Automation.AutomationProperties.SetName(this, Text);
    }
    private TextBlock Cell(char c)
    {
        var cell = new TextBlock { Text = c.ToString(), FontWeight = weight, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        if (size is { } value) cell.FontSize = value;
        System.Windows.Documents.Typography.SetNumeralAlignment(cell, FontNumeralAlignment.Tabular);
        return cell;
    }
    private void Change(string before, string after)
    {
        if (Children.Count != after.Length || before.Length != after.Length)
        {
            Children.Clear();
            foreach (var c in after) { var slot = new Grid { ClipToBounds = true }; slot.Children.Add(Cell(c)); Children.Add(slot); }
            return;
        }
        IReadOnlySet<int> rolling = animated() ? ClockRoll.Rolling(before, after, rollSeconds) : new HashSet<int>();
        for (var i = 0; i < after.Length; i++)
        {
            if (before[i] == after[i]) continue;
            var slot = (Grid)Children[i];
            // A roll still running when the next one starts simply ends now.
            while (slot.Children.Count > 1) slot.Children.RemoveAt(0);
            var old = (TextBlock)slot.Children[0]; var fresh = Cell(after[i]);
            if (!rolling.Contains(i) || slot.ActualHeight <= 0) { slot.Children.Clear(); slot.Children.Add(fresh); continue; }
            var height = slot.ActualHeight; var rise = new TranslateTransform(0, height); var away = new TranslateTransform();
            fresh.RenderTransform = rise; old.RenderTransform = away; slot.Children.Add(fresh);
            Motion.To(rise, TranslateTransform.YProperty, 0, Duration);
            Motion.To(old, OpacityProperty, 0, Duration / 2);
            Motion.To(away, TranslateTransform.YProperty, -height, Duration, () => slot.Children.Remove(old));
        }
    }
}

/// <summary>A short burst of accent rays where the empty desktop was clicked.</summary>
internal static class ClickSpark
{
    private const int Rays = 8, Duration = 380;
    internal static void Burst(Canvas layer, Point at)
    {
        for (var i = 0; i < Rays; i++)
        {
            var angle = i * Math.Tau / Rays; double dx = Math.Cos(angle), dy = Math.Sin(angle);
            var ray = new Line { X1 = at.X + dx * 5, Y1 = at.Y + dy * 5, X2 = at.X + dx * 11, Y2 = at.Y + dy * 11, StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, IsHitTestVisible = false };
            ray.SetResourceReference(Shape.StrokeProperty, "Accent"); layer.Children.Add(ray);
            Motion.To(ray, Line.X1Property, at.X + dx * 20, Duration); Motion.To(ray, Line.Y1Property, at.Y + dy * 20, Duration);
            Motion.To(ray, Line.X2Property, at.X + dx * 25, Duration); Motion.To(ray, Line.Y2Property, at.Y + dy * 25, Duration);
            Motion.To(ray, UIElement.OpacityProperty, 0, Duration, () => layer.Children.Remove(ray));
        }
    }
}

/// <summary>A window title that arrives letter by letter once, then becomes ordinary text again so kerning and screen readers stay right.</summary>
internal static class SplitText
{
    private const int Step = 24, Duration = 300;
    internal static FrameworkElement Make(string text, double size, FontWeight weight, DesktopViewModel model, Thickness margin = default)
    {
        var real = new TextBlock { Text = text, FontSize = size, FontWeight = weight };
        var stage = new Grid { Margin = margin, HorizontalAlignment = HorizontalAlignment.Left }; stage.Children.Add(real);
        var played = false;
        stage.Loaded += (_, _) =>
        {
            if (played || !Motion.Enabled(model)) return; played = true;
            real.Opacity = 0;
            var letters = new StackPanel { Orientation = Orientation.Horizontal, IsHitTestVisible = false }; stage.Children.Add(letters);
            for (var i = 0; i < text.Length; i++)
            {
                var index = i;
                if (char.IsWhiteSpace(text[i])) { letters.Children.Add(new Border { Width = size * .28 }); continue; }
                var rise = new TranslateTransform(0, size * .4);
                var letter = new TextBlock { Text = text[i].ToString(), FontSize = size, FontWeight = weight, Opacity = 0, RenderTransform = rise }; letters.Children.Add(letter);
                Motion.To(rise, TranslateTransform.YProperty, 0, Duration, delay: i * Step);
                Motion.To(letter, UIElement.OpacityProperty, 1, Duration, () => { if (index == text.Length - 1) { stage.Children.Remove(letters); real.Opacity = 1; } }, i * Step);
            }
        };
        return stage;
    }
}
