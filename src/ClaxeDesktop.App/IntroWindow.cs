using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;

/// <summary>
/// About three seconds of night sky, a rising crescent and the name letter by letter, then the desktop fades in.
/// Click, Esc or any key skips it. Only drawn shapes and the theme palette; no media files.
/// </summary>
internal sealed class IntroWindow : Window
{
    private readonly TaskCompletionSource done = new();
    private readonly Grid stage = new();
    private bool leaving;
    private IntroWindow(Settings settings)
    {
        var palette = ThemePresets.Resolve(settings);
        Color Hex(string value) => (Color)ColorConverter.ConvertFromString(value);
        var accent = Hex(palette.Accent); var canvas = Hex(palette.Canvas); var surface = Hex(palette.Surface);
        WindowStyle = WindowStyle.None; AllowsTransparency = true; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true;
        Background = Brushes.Transparent; Title = "Noxelyra"; WindowStartupLocation = WindowStartupLocation.Manual;
        var area = SystemParameters.PrimaryScreenWidth; Left = 0; Top = 0; Width = area; Height = SystemParameters.PrimaryScreenHeight;
        Content = stage; stage.Opacity = 0;
        stage.Background = new RadialGradientBrush(surface, canvas) { RadiusX = .9, RadiusY = .9, Center = new(.5, .45), GradientOrigin = new(.5, .45) };

        // Twinkling stars, each on its own slow loop.
        var sky = new Canvas { IsHitTestVisible = false }; stage.Children.Add(sky);
        var random = new Random(1709);
        for (int i = 0; i < 90; i++)
        {
            var size = random.NextDouble() * 2.2 + .6;
            var star = new Ellipse { Width = size, Height = size, Fill = new SolidColorBrush(Color.FromArgb(220, 220, 210, 245)), Opacity = .2 };
            Canvas.SetLeft(star, random.NextDouble() * Width); Canvas.SetTop(star, random.NextDouble() * Height * .85); sky.Children.Add(star);
            star.BeginAnimation(OpacityProperty, new DoubleAnimation(.15, .9, TimeSpan.FromMilliseconds(900 + random.Next(1400))) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromMilliseconds(random.Next(800)) });
        }

        var center = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; stage.Children.Add(center);
        var emblem = new Grid { Width = 180, Height = 180, HorizontalAlignment = HorizontalAlignment.Center, RenderTransformOrigin = new(.5, .5) }; center.Children.Add(emblem);
        // Expanding halo behind the moon.
        var halo = new Ellipse { Width = 180, Height = 180, Stroke = new SolidColorBrush(accent), StrokeThickness = 1.5, Opacity = 0, RenderTransformOrigin = new(.5, .5), RenderTransform = new ScaleTransform(.6, .6) };
        emblem.Children.Add(halo);
        var glow = new Ellipse { Width = 150, Height = 150, Fill = new RadialGradientBrush(Color.FromArgb(110, accent.R, accent.G, accent.B), Color.FromArgb(0, accent.R, accent.G, accent.B)), Opacity = 0 };
        emblem.Children.Add(glow);
        // Crescent: a lit disc minus an offset disc, so it stays crisp at any scale.
        var crescent = new System.Windows.Shapes.Path { Fill = new SolidColorBrush(accent), Data = new CombinedGeometry(GeometryCombineMode.Exclude, new EllipseGeometry(new Point(90, 90), 54, 54), new EllipseGeometry(new Point(112, 74), 46, 46)), Opacity = 0, RenderTransformOrigin = new(.5, .5), RenderTransform = new TransformGroup { Children = { new ScaleTransform(.7, .7), new RotateTransform(-25) } } };
        emblem.Children.Add(crescent);

        var word = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 26, 0, 0) }; center.Children.Add(word);
        var letters = "NOXELYRA".Select(c => new TextBlock { Text = c.ToString(), FontSize = 40, FontWeight = FontWeights.Light, Foreground = Brushes.White, Margin = new(9, 0, 9, 0), Opacity = 0, RenderTransform = new TranslateTransform(0, 14) }).ToList();
        foreach (var letter in letters) word.Children.Add(letter);
        var line = new Border { Height = 1, Width = 0, Background = new SolidColorBrush(accent), Margin = new(0, 18, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, Opacity = .8 }; center.Children.Add(line);
        var tagline = new TextBlock { Text = "masaüstün uyanıyor", FontSize = 15, Foreground = new SolidColorBrush(Color.FromArgb(200, 220, 214, 236)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 16, 0, 0), Opacity = 0 }; center.Children.Add(tagline);

        Loaded += (_, _) =>
        {
            var ease = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 5 };
            DoubleAnimation A(double to, int ms, int at, double? from = null) => new(to, TimeSpan.FromMilliseconds(ms)) { From = from, BeginTime = TimeSpan.FromMilliseconds(at), EasingFunction = ease };
            stage.BeginAnimation(OpacityProperty, A(1, 450, 0));
            crescent.BeginAnimation(OpacityProperty, A(1, 700, 250));
            var group = (TransformGroup)crescent.RenderTransform;
            group.Children[0].BeginAnimation(ScaleTransform.ScaleXProperty, A(1, 1100, 250)); group.Children[0].BeginAnimation(ScaleTransform.ScaleYProperty, A(1, 1100, 250));
            group.Children[1].BeginAnimation(RotateTransform.AngleProperty, A(0, 1400, 250));
            glow.BeginAnimation(OpacityProperty, A(1, 900, 450));
            halo.BeginAnimation(OpacityProperty, new DoubleAnimationUsingKeyFrames { BeginTime = TimeSpan.FromMilliseconds(500), KeyFrames = { new EasingDoubleKeyFrame(.7, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300))), new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1500)), ease) } });
            halo.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, A(1.9, 1500, 500)); halo.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, A(1.9, 1500, 500));
            for (int i = 0; i < letters.Count; i++)
            {
                letters[i].BeginAnimation(OpacityProperty, A(1, 500, 850 + i * 70));
                letters[i].RenderTransform.BeginAnimation(TranslateTransform.YProperty, A(0, 600, 850 + i * 70));
            }
            line.BeginAnimation(WidthProperty, A(260, 800, 1500));
            tagline.BeginAnimation(OpacityProperty, A(1, 600, 1750));
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2900) };
            timer.Tick += (_, _) => { timer.Stop(); Leave(450); }; timer.Start();
        };
        MouseDown += (_, _) => Leave(220);
        KeyDown += (_, _) => Leave(220);
    }
    private void Leave(int milliseconds)
    {
        if (leaving) return; leaving = true;
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
        fade.Completed += (_, _) => { Close(); done.TrySetResult(); };
        stage.RenderTransformOrigin = new(.5, .5); stage.RenderTransform = new ScaleTransform(1, 1);
        stage.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.04, TimeSpan.FromMilliseconds(milliseconds)));
        stage.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.04, TimeSpan.FromMilliseconds(milliseconds)));
        stage.BeginAnimation(OpacityProperty, fade);
    }
    /// <summary>Plays the intro unless the user turned it off or motion is reduced; never blocks longer than about 4 seconds.</summary>
    internal static async Task PlayAsync(Settings settings)
    {
        if (!settings.IntroSequence || !settings.AnimationsEnabled || !SystemParameters.ClientAreaAnimation || SystemParameters.HighContrast) return;
        var intro = new IntroWindow(settings); intro.Show(); intro.Activate();
        await Task.WhenAny(intro.done.Task, Task.Delay(4500));
        if (!intro.done.Task.IsCompleted) intro.Close();
    }
}
