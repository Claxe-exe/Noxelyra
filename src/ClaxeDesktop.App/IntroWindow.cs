using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;

/// <summary>
/// Boot and shutdown sequences in the spirit of a TV powering on: a line of light, four palette dots that hop in turn
/// and fold into the crescent, then the name with a passing shine. Shutdown plays the same story backwards.
/// Click or any key skips. Drawn shapes and the theme palette only; no media files.
/// </summary>
internal sealed class IntroWindow : Window
{
    /// <summary>Verification and safe runs never animate boot or shutdown.</summary>
    internal static bool Suppressed { get; set; }
    private readonly TaskCompletionSource done = new();
    private readonly Grid stage = new();
    private readonly Rectangle beam;
    private readonly List<Ellipse> dots = [];
    private readonly Grid emblem = new() { Width = 180, Height = 180, HorizontalAlignment = HorizontalAlignment.Center, RenderTransformOrigin = new(.5, .5) };
    private readonly System.Windows.Shapes.Path crescent;
    private readonly Ellipse glow, halo;
    private readonly List<TextBlock> letters;
    private readonly Rectangle shine;
    private readonly StackPanel word = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 28, 0, 0) };
    private readonly IEasingFunction ease = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 5 };
    private bool leaving;
    private IntroWindow(Settings settings)
    {
        var palette = ThemePresets.Resolve(settings);
        Color Hex(string value) => (Color)ColorConverter.ConvertFromString(value);
        var accent = Hex(palette.Accent);
        WindowStyle = WindowStyle.None; AllowsTransparency = true; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true;
        Background = Brushes.Transparent; Title = "Noxelyra"; WindowStartupLocation = WindowStartupLocation.Manual;
        Left = 0; Top = 0; Width = SystemParameters.PrimaryScreenWidth; Height = SystemParameters.PrimaryScreenHeight;
        Content = stage; stage.Opacity = 0;
        stage.Background = new RadialGradientBrush(Hex(palette.Surface), Hex(palette.Canvas)) { RadiusX = .9, RadiusY = .9, Center = new(.5, .45), GradientOrigin = new(.5, .45) };

        var sky = new Canvas { IsHitTestVisible = false }; stage.Children.Add(sky);
        var random = new Random(1709);
        for (int i = 0; i < 90; i++)
        {
            var size = random.NextDouble() * 2.2 + .6;
            var star = new Ellipse { Width = size, Height = size, Fill = new SolidColorBrush(Color.FromArgb(220, 220, 210, 245)), Opacity = .2 };
            Canvas.SetLeft(star, random.NextDouble() * Width); Canvas.SetTop(star, random.NextDouble() * Height * .85); sky.Children.Add(star);
            star.BeginAnimation(OpacityProperty, new DoubleAnimation(.1, .8, TimeSpan.FromMilliseconds(900 + random.Next(1400))) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromMilliseconds(random.Next(800)) });
        }

        // The "power on" line of light across the middle of the screen.
        beam = new Rectangle { Height = 2, Width = 0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Fill = new LinearGradientBrush(Color.FromArgb(0, accent.R, accent.G, accent.B), Color.FromArgb(0, accent.R, accent.G, accent.B), 0) { GradientStops = { new(Color.FromArgb(0, 255, 255, 255), 0), new(Colors.White, .5), new(Color.FromArgb(0, 255, 255, 255), 1) } }, Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = accent, BlurRadius = 18, ShadowDepth = 0, Opacity = .9 } };
        stage.Children.Add(beam);

        var center = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; stage.Children.Add(center);
        center.Children.Add(emblem);
        halo = new Ellipse { Width = 180, Height = 180, Stroke = new SolidColorBrush(accent), StrokeThickness = 1.5, Opacity = 0, RenderTransformOrigin = new(.5, .5), RenderTransform = new ScaleTransform(.6, .6) }; emblem.Children.Add(halo);
        glow = new Ellipse { Width = 150, Height = 150, Fill = new RadialGradientBrush(Color.FromArgb(110, accent.R, accent.G, accent.B), Color.FromArgb(0, accent.R, accent.G, accent.B)), Opacity = 0 }; emblem.Children.Add(glow);
        crescent = new System.Windows.Shapes.Path { Fill = new SolidColorBrush(accent), Data = new CombinedGeometry(GeometryCombineMode.Exclude, new EllipseGeometry(new Point(90, 90), 54, 54), new EllipseGeometry(new Point(112, 74), 46, 46)), Opacity = 0, RenderTransformOrigin = new(.5, .5), RenderTransform = new TransformGroup { Children = { new ScaleTransform(.4, .4), new RotateTransform(-25) } } };
        emblem.Children.Add(crescent);
        // Four palette dots, in the moon's place, that hop one after another.
        var colors = new[] { palette.Accent, palette.Secondary, WindowsLook.Shade(palette.Accent, .45), WindowsLook.Shade(palette.Secondary, .45) };
        var row = new Canvas { Width = 180, Height = 180, IsHitTestVisible = false }; emblem.Children.Add(row);
        for (int i = 0; i < 4; i++)
        {
            var dot = new Ellipse { Width = 18, Height = 18, Fill = new SolidColorBrush(Hex(colors[i])), Opacity = 0, RenderTransform = new TranslateTransform(0, 0) };
            Canvas.SetLeft(dot, 90 - 9 + (i - 1.5) * 34); Canvas.SetTop(dot, 81); row.Children.Add(dot); dots.Add(dot);
        }

        center.Children.Add(word);
        letters = "NOXELYRA".Select(c => new TextBlock { Text = c.ToString(), FontSize = 40, FontWeight = FontWeights.Light, Foreground = Brushes.White, Margin = new(9, 0, 9, 0), Opacity = 0, RenderTransform = new TranslateTransform(0, 14) }).ToList();
        foreach (var letter in letters) word.Children.Add(letter);
        // A soft band of light that sweeps across the name once.
        shine = new Rectangle { Width = 90, Fill = new LinearGradientBrush(Color.FromArgb(0, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 0) { GradientStops = { new(Color.FromArgb(0, 255, 255, 255), 0), new(Color.FromArgb(150, 255, 255, 255), .5), new(Color.FromArgb(0, 255, 255, 255), 1) } }, HorizontalAlignment = HorizontalAlignment.Left, RenderTransform = new TranslateTransform(-120, 0), Opacity = 0, IsHitTestVisible = false };
        var wordHost = new Grid { ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Center }; center.Children.Remove(word); wordHost.Children.Add(word); wordHost.Children.Add(shine); center.Children.Add(wordHost);

        MouseDown += (_, _) => Finish(220);
        KeyDown += (_, _) => Finish(220);
    }
    private DoubleAnimation A(double to, int ms, int at, double? from = null, IEasingFunction? easing = null) => new(to, TimeSpan.FromMilliseconds(ms)) { From = from, BeginTime = TimeSpan.FromMilliseconds(at), EasingFunction = easing ?? ease };
    private void Boot()
    {
        stage.BeginAnimation(OpacityProperty, A(1, 260, 0));
        beam.BeginAnimation(WidthProperty, A(Width * .55, 650, 120));
        beam.BeginAnimation(OpacityProperty, new DoubleAnimationUsingKeyFrames { KeyFrames = { new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))), new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700))), new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1050)), ease) } });
        var hop = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        for (int i = 0; i < dots.Count; i++)
        {
            var start = 700 + i * 110;
            dots[i].BeginAnimation(OpacityProperty, new DoubleAnimationUsingKeyFrames { BeginTime = TimeSpan.FromMilliseconds(start), KeyFrames = { new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))), new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1450 - i * 110))), new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1700 - i * 110))) } });
            // Two hops, each dot a beat behind the previous one.
            dots[i].RenderTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimationUsingKeyFrames { BeginTime = TimeSpan.FromMilliseconds(start + 200), KeyFrames = { new EasingDoubleKeyFrame(-20, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180)), hop), new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(360)), new QuadraticEase { EasingMode = EasingMode.EaseIn }), new EasingDoubleKeyFrame(-12, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(520)), hop), new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(680)), new QuadraticEase { EasingMode = EasingMode.EaseIn }) } });
            // Then all four slide into the centre where the moon appears.
            var offset = -(i - 1.5) * 34;
            dots[i].RenderTransform.BeginAnimation(TranslateTransform.XProperty, A(offset, 380, 1850));
        }
        var group = (TransformGroup)crescent.RenderTransform;
        crescent.BeginAnimation(OpacityProperty, A(1, 500, 2000));
        group.Children[0].BeginAnimation(ScaleTransform.ScaleXProperty, A(1, 900, 2000)); group.Children[0].BeginAnimation(ScaleTransform.ScaleYProperty, A(1, 900, 2000));
        group.Children[1].BeginAnimation(RotateTransform.AngleProperty, A(0, 1100, 2000));
        glow.BeginAnimation(OpacityProperty, A(1, 800, 2100));
        halo.BeginAnimation(OpacityProperty, new DoubleAnimationUsingKeyFrames { BeginTime = TimeSpan.FromMilliseconds(2100), KeyFrames = { new EasingDoubleKeyFrame(.7, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250))), new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1300)), ease) } });
        halo.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, A(1.9, 1300, 2100)); halo.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, A(1.9, 1300, 2100));
        for (int i = 0; i < letters.Count; i++)
        {
            letters[i].BeginAnimation(OpacityProperty, A(1, 450, 2300 + i * 60));
            letters[i].RenderTransform.BeginAnimation(TranslateTransform.YProperty, A(0, 550, 2300 + i * 60));
        }
        shine.BeginAnimation(OpacityProperty, A(1, 150, 2950));
        shine.RenderTransform.BeginAnimation(TranslateTransform.XProperty, A(word.ActualWidth + 120, 900, 2950, -120, new SineEase { EasingMode = EasingMode.EaseInOut }));
        After(4000, () => Finish(450));
    }
    private void PowerOff()
    {
        // Start from the finished emblem and walk the story backwards.
        stage.Opacity = 0; crescent.Opacity = 1; glow.Opacity = 1; ((TransformGroup)crescent.RenderTransform).Children[0].SetValue(ScaleTransform.ScaleXProperty, 1.0); ((TransformGroup)crescent.RenderTransform).Children[0].SetValue(ScaleTransform.ScaleYProperty, 1.0); ((RotateTransform)((TransformGroup)crescent.RenderTransform).Children[1]).Angle = 0;
        foreach (var letter in letters) { letter.Opacity = 1; ((TranslateTransform)letter.RenderTransform).Y = 0; }
        stage.BeginAnimation(OpacityProperty, A(1, 300, 0));
        for (int i = 0; i < letters.Count; i++) letters[letters.Count - 1 - i].BeginAnimation(OpacityProperty, A(0, 260, 350 + i * 45));
        var group = (TransformGroup)crescent.RenderTransform;
        group.Children[0].BeginAnimation(ScaleTransform.ScaleXProperty, A(.4, 420, 800)); group.Children[0].BeginAnimation(ScaleTransform.ScaleYProperty, A(.4, 420, 800));
        crescent.BeginAnimation(OpacityProperty, A(0, 300, 900)); glow.BeginAnimation(OpacityProperty, A(0, 400, 800));
        for (int i = 0; i < dots.Count; i++)
        {
            var offset = -(i - 1.5) * 34; ((TranslateTransform)dots[i].RenderTransform).X = offset;
            dots[i].BeginAnimation(OpacityProperty, new DoubleAnimationUsingKeyFrames { BeginTime = TimeSpan.FromMilliseconds(950), KeyFrames = { new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))), new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(500))), new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700))) } });
            dots[i].RenderTransform.BeginAnimation(TranslateTransform.XProperty, A(0, 380, 1000));
        }
        beam.BeginAnimation(WidthProperty, new DoubleAnimationUsingKeyFrames { BeginTime = TimeSpan.FromMilliseconds(1550), KeyFrames = { new EasingDoubleKeyFrame(Width * .55, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250)), ease), new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(600)), new QuadraticEase { EasingMode = EasingMode.EaseIn }) } });
        beam.BeginAnimation(OpacityProperty, A(1, 120, 1550));
        After(2250, () => Finish(250));
    }
    private void After(int milliseconds, Action action)
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); action(); }; timer.Start();
    }
    private void Finish(int milliseconds)
    {
        if (leaving) return; leaving = true;
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
        fade.Completed += (_, _) => { Close(); done.TrySetResult(); };
        stage.BeginAnimation(OpacityProperty, fade);
    }
    /// <summary>The user's own clip (MP4/GIF, at most ten seconds), then either a fade or the TV power-on reveal.</summary>
    private void PlayCustom(string path, string transition, int fadeMs)
    {
        foreach (UIElement child in stage.Children) child.Visibility = Visibility.Collapsed;
        stage.Background = Brushes.Black;
        var layer = new Grid(); stage.Children.Add(layer);
        stage.BeginAnimation(OpacityProperty, A(1, 220, 0));
        var ended = false; IntroMedia.GifPlayer? gif = null; MediaElement? video = null;
        void End()
        {
            if (ended || leaving) return; ended = true;
            gif?.Stop(); video?.Pause();
            switch (transition)
            {
                // Kararma: the picture sinks to black at the chosen pace, then the black lifts off the desktop.
                case "fade":
                    layer.BeginAnimation(OpacityProperty, A(0, fadeMs, 0, easing: new SineEase { EasingMode = EasingMode.EaseInOut }));
                    After(fadeMs + 250, () => { video?.Close(); Finish(Math.Clamp(fadeMs / 2, 400, 1500)); });
                    break;
                // Kararma + TV: sink to black, a short dark beat, then the TV power-on reveal.
                case "fade-tv":
                    layer.BeginAnimation(OpacityProperty, A(0, fadeMs, 0, easing: new SineEase { EasingMode = EasingMode.EaseInOut }));
                    After(fadeMs + 350, () => Reveal(layer, () => video?.Close(), false));
                    break;
                default: Reveal(layer, () => video?.Close(), true); break;
            }
        }
        void Fallback() { if (ended) return; ended = true; stage.Children.Remove(layer); foreach (UIElement child in stage.Children) child.Visibility = Visibility.Visible; Boot(); }
        try
        {
            if (IntroMedia.IsGif(path))
            {
                gif = new IntroMedia.GifPlayer(IntroMedia.LoadGif(path));
                layer.Children.Add(new Image { Source = gif.Surface, Stretch = Stretch.Uniform });
                gif.Ended += End; gif.Play();
            }
            else
            {
                video = new MediaElement { LoadedBehavior = System.Windows.Controls.MediaState.Manual, UnloadedBehavior = System.Windows.Controls.MediaState.Close, Stretch = Stretch.Uniform, Source = new Uri(path) };
                video.MediaEnded += (_, _) => End(); video.MediaFailed += (_, _) => Fallback();
                layer.Children.Add(video); video.Play();
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or UnauthorizedAccessException or FileFormatException) { Fallback(); return; }
        After((int)(DesktopSpaceOptions.IntroMaxSeconds * 1000) + 300, End);
    }
    // CRT-style power on: the picture collapses to a bright line, then the screen opens from that line to reveal the desktop.
    private void Reveal(Grid layer, Action release, bool collapsePicture)
    {
        // Straight from the picture the image first shrinks into the line; after a fade the screen is already dark.
        if (collapsePicture)
        {
            layer.RenderTransformOrigin = new(.5, .5); layer.RenderTransform = new ScaleTransform(1, 1);
            layer.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, A(.004, 420, 0, easing: new QuadraticEase { EasingMode = EasingMode.EaseIn }));
            layer.BeginAnimation(OpacityProperty, A(0, 200, 380));
        }
        var half = Height / 2;
        var top = new Rectangle { Fill = Brushes.Black, Height = half, VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform() };
        var bottom = new Rectangle { Fill = Brushes.Black, Height = half, VerticalAlignment = VerticalAlignment.Bottom, RenderTransform = new TranslateTransform() };
        beam.Visibility = Visibility.Visible; beam.Width = 0; beam.Opacity = 1;
        stage.Children.Add(top); stage.Children.Add(bottom); stage.Children.Remove(beam); stage.Children.Add(beam);
        stage.Background = Brushes.Transparent;
        var open = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var start = collapsePicture ? 420 : 0;
        beam.BeginAnimation(WidthProperty, A(Width, 650, start));
        beam.BeginAnimation(OpacityProperty, new DoubleAnimationUsingKeyFrames { BeginTime = TimeSpan.FromMilliseconds(start), KeyFrames = { new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700))), new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1500)), ease) } });
        top.RenderTransform.BeginAnimation(TranslateTransform.YProperty, A(-half, 1150, start + 650, easing: open));
        bottom.RenderTransform.BeginAnimation(TranslateTransform.YProperty, A(half, 1150, start + 650, easing: open));
        After(start + 1900, () => { release(); leaving = true; Close(); done.TrySetResult(); });
    }
    private static bool Allowed(Settings settings) => !Suppressed && settings.IntroSequence && settings.AnimationsEnabled && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
    /// <summary>The managed copy of the user's clip, or null when unset, missing or outside Noxelyra's data folder.</summary>
    internal static string? CustomClip(Settings settings, string dataDirectory)
    {
        if (settings.DesktopSpace.IntroMediaPath is not { } path) return null;
        var root = System.IO.Path.GetFullPath(System.IO.Path.Combine(dataDirectory, "intro-assets")) + System.IO.Path.DirectorySeparatorChar;
        return System.IO.Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0 ? path : null;
    }
    private static async Task PlayAsync(Settings settings, bool boot, string? clip, bool force = false)
    {
        if (!force && !Allowed(settings)) return;
        var window = new IntroWindow(settings);
        window.Loaded += (_, _) => { if (!boot) window.PowerOff(); else if (clip is not null) window.PlayCustom(clip, settings.DesktopSpace.IntroTransition, settings.DesktopSpace.IntroFadeMs); else window.Boot(); };
        window.Show(); window.Activate();
        await Task.WhenAny(window.done.Task, Task.Delay(boot ? (clip is null ? 5000 : 13000 + settings.DesktopSpace.IntroFadeMs + 3000) : 3000));
        if (!window.done.Task.IsCompleted) window.Close();
    }
    internal static Task PlayAsync(Settings settings, string dataDirectory) => PlayAsync(settings, true, CustomClip(settings, dataDirectory));
    /// <summary>Plays the boot sequence now from the control center, even when it is switched off for startup.</summary>
    internal static Task PreviewAsync(Settings settings, string dataDirectory) => PlayAsync(settings, true, CustomClip(settings, dataDirectory), force: true);
    /// <summary>Shutdown counterpart; Noxelyra tears down underneath while it plays.</summary>
    internal static Task PlayOutroAsync(Settings settings) => PlayAsync(settings, false, null);
}
