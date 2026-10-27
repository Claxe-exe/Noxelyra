using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

/// <summary>
/// The dynamic island opened up: page 1 shows the track (±10 s), weather and CPU/GPU/RAM; page 2 holds volume and microphone,
/// monitor brightness, a screenshot, "close the frozen app" and pinned apps. Both pages keep one size; the arrow switches them.
/// From the bar it opens under the island like a menu. With the island shortcut (Alt+0) it drops in at the top of the screen
/// without taking focus, so a game keeps running; while it is up, ←/→ seek, ↑/↓ volume, Space plays/pauses, Tab flips the page
/// and Esc closes it, and it hides itself after a few idle seconds so those keys go back to the game.
/// </summary>
internal sealed class IslandCard : Window
{
    private const string IconFont = "Segoe Fluent Icons, Segoe MDL2 Assets";
    private const double CardWidth = 468, CardHeight = 236, Shadow = 16, PageWidth = 404, PageHeight = 186;
    private static readonly int[] GameKeys = [0x25, 0x27, 0x26, 0x28, 0x20, 0x09, 0x1B];
    private readonly DesktopController controller;
    private readonly ShellIconService icons;
    private readonly MediaService media = new();
    private readonly WindowsAudioService audio = new();
    private readonly Border card = new() { Width = CardWidth, Height = CardHeight, CornerRadius = new(28), BorderThickness = new(1), Margin = new(Shadow) };
    private readonly ScaleTransform grow = new(1, 1);
    private readonly TranslateTransform slide = new();
    private readonly TextBlock arrowGlyph = Glyph("\uE76C", 14), hint = new() { FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
    private readonly Ellipse dot1 = Dot(), dot2 = Dot();
    private readonly DispatcherTimer poll = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer idle = new() { Interval = TimeSpan.FromSeconds(12) };
    private readonly DispatcherTimer brightnessSave = new() { Interval = TimeSpan.FromMilliseconds(250) };
    // Page 1
    private readonly Border art = new() { Width = 56, Height = 56, CornerRadius = new(14), Margin = new(0, 0, 12, 0) };
    private readonly TextBlock artGlyph = Glyph("\uE8D6", 22), title = Line(14, true), artist = Line(12), position = Small(), duration = Small(), playGlyph = Glyph("\uE768", 20);
    private readonly ColumnDefinition done = new() { Width = new(0, GridUnitType.Star) }, remaining = new() { Width = new(1, GridUnitType.Star) };
    private readonly FrameworkElement progress, nowPlaying, nothingPlaying;
    private readonly Button back10, previous, play, next, forward10;
    private readonly TextBlock weatherGlyph = new() { FontFamily = new FontFamily("Segoe UI Symbol"), FontSize = 24, Margin = new(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
    // HH:mm next to the weather, always 24-hour and without seconds.
    private readonly TextBlock clockText = new() { FontSize = 22, FontWeight = FontWeights.Light, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 2, 2, 0) };
    private readonly TextBlock weatherTemp = new() { FontSize = 22, FontWeight = FontWeights.Light, VerticalAlignment = VerticalAlignment.Center }, weatherLine = Small();
    private readonly StackPanel weatherShown = new(), weatherAsk = new();
    private readonly TextBox cityInput = new() { MaxLength = OpenMeteo.MaxCityLength, FontSize = 12, Padding = new(6, 3, 6, 3) };
    private readonly Dictionary<string, (ColumnDefinition Fill, ColumnDefinition Free, TextBlock Value)> stats = [];
    private readonly TextBlock timerText = new() { FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button timerButton, timerStop;
    // Page 2
    private readonly Slider volume = new() { Minimum = 0, Maximum = 100, Margin = new(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, IsSnapToTickEnabled = true, TickFrequency = 1 };
    private readonly Slider brightness = new() { Minimum = 0, Maximum = 100, Margin = new(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, IsSnapToTickEnabled = true, TickFrequency = 1 };
    private readonly TextBlock volumeText = Figure(), brightnessText = Figure(), volumeGlyph = Glyph("\uE767", 16), micGlyph = Glyph("\uE720", 16), brightnessNote = Small();
    private readonly Button muteButton, micButton, killButton;
    private readonly WrapPanel apps = new();
    private readonly FrameworkElement brightnessRow;
    private int page;
    private bool summoned, leaving, refreshing, polling;
    private DateTime hiddenAt, trackRead, weatherDue, killArmedUntil;
    private MediaState? track;
    private ImageSource? artShown;
    // App volume (the playing app only) and the live player-window preview.
    private readonly Slider appVolume = new() { Minimum = 0, Maximum = 100, Margin = new(6, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, IsSnapToTickEnabled = true, TickFrequency = 1 };
    private readonly TextBlock appLabel = new() { FontSize = 11, Width = 84, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly DispatcherTimer appVolumeSave = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly Border liveBadge = new() { Width = 20, Height = 20, CornerRadius = new(10), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new(0, 0, 8, -4), Visibility = Visibility.Collapsed, IsHitTestVisible = false };
    // The track's video (or blurred cover) behind page 1.
    private readonly Image blurArt = new() { Stretch = Stretch.UniformToFill, Opacity = .5, Effect = new System.Windows.Media.Effects.BlurEffect { Radius = 28 }, Visibility = Visibility.Collapsed };
    private readonly Border shade = new() { Visibility = Visibility.Collapsed, IsHitTestVisible = false };
    // A thin solid rim over the video edge, so the video's corners always sit inside the card's rounded shape.
    private const double VideoInset = 6;
    private readonly Border videoRim = new() { BorderThickness = new(VideoInset + 1), CornerRadius = new(27), Visibility = Visibility.Collapsed, IsHitTestVisible = false };
    private SolidColorBrush opaqueBrush = new(Colors.Black), glassBrush = new(Colors.Black);
    private VideoBackdrop? backdrop;
    private (int Left, int Top, int Right, int Bottom)? videoCrop;
    private string? videoKey;
    private (double Width, double Height) videoWindowSize;
    private CancellationTokenSource? videoScan;
    private DateTime videoScanned;
    private readonly Button musicLink;
    private void MusicLinkText() => musicLink.Content = MusicApp.Installed ? "Noxelyra Music'i aç" : "Noxelyra Music'i kur";
    // Video areas already found, per player window and size, so a reopened card shows the video at once.
    private readonly Dictionary<(nint Window, double Width, double Height), (int Left, int Top, int Right, int Bottom)> knownCrops = [];
    private bool videoOn = true;
    private nint playerWindow;
    private PerformanceSampler? sampler;
    private nint hwnd, brightnessMonitor, killWindow;
    private string? killName;
    private int killProcess;
    private CancellationTokenSource? weatherCancel;
    private readonly List<int> registered = [];
    internal bool Summoned => summoned && IsVisible;
    internal int Page => page;
    internal bool RecentlyClosed => DateTime.UtcNow - hiddenAt < TimeSpan.FromMilliseconds(350);
    internal int RegisteredKeys => registered.Count;
    public IslandCard(DesktopController controller, ShellIconService icons)
    {
        this.controller = controller; this.icons = icons;
        Title = "Noxelyra · Dinamik ada"; WindowStyle = WindowStyle.None; AllowsTransparency = true; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; Topmost = true; Background = Brushes.Transparent; Width = CardWidth + Shadow * 2; Height = CardHeight + Shadow * 2;
        SetResourceReference(ForegroundProperty, "Text");
        card.SetResourceReference(Border.BorderBrushProperty, "Line");
        card.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 30, ShadowDepth = 6, Opacity = .5, Color = Colors.Black };
        card.RenderTransformOrigin = new(.5, 0); card.RenderTransform = grow; Content = card;

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new() { Width = new(PageWidth) }); layout.ColumnDefinitions.Add(new());
        layout.RowDefinitions.Add(new() { Height = new(PageHeight) }); layout.RowDefinitions.Add(new());
        // Background layer (blurred cover or the video showing through) under the pages, clipped to the card's corners.
        var host = new Grid { Clip = new RectangleGeometry(new Rect(0, 0, CardWidth - 2, CardHeight - 2), 27, 27) };
        shade.Background = new LinearGradientBrush(Color.FromArgb(0xC8, 0, 0, 0), Color.FromArgb(0x50, 0, 0, 0), 0);
        host.Children.Add(blurArt); host.Children.Add(shade); host.Children.Add(videoRim);
        layout.Margin = new(16, 16, 16, 8); host.Children.Add(layout); card.Child = host;
        // A Canvas, not a StackPanel: a panel narrower than its content clips it, which would hide page 2 after the slide.
        var strip = new Canvas { RenderTransform = slide };
        var viewport = new Border { ClipToBounds = true, Child = strip }; layout.Children.Add(viewport);

        // ---- Page 1: track, weather, stats ----
        var one = new Grid { Width = PageWidth, Height = PageHeight };
        one.ColumnDefinitions.Add(new() { Width = new(236) }); one.ColumnDefinitions.Add(new() { Width = new(16) }); one.ColumnDefinitions.Add(new());
        var music = new Grid(); one.Children.Add(music);
        var playingPanel = new StackPanel();
        var head = new DockPanel();
        art.SetResourceReference(Border.BackgroundProperty, "Card"); artGlyph.HorizontalAlignment = HorizontalAlignment.Center; art.Child = artGlyph;
        // The cover doubles as the switch for the live player-window view; the small badge says one is available.
        liveBadge.SetResourceReference(Border.BackgroundProperty, "Accent"); var badgeGlyph = Glyph("\uE714", 10); badgeGlyph.HorizontalAlignment = HorizontalAlignment.Center; badgeGlyph.SetResourceReference(TextBlock.ForegroundProperty, "Canvas"); liveBadge.Child = badgeGlyph;
        var cover = new Grid { Cursor = Cursors.Hand, ToolTip = "Arka planda video: aç / kapat" }; cover.Children.Add(art); cover.Children.Add(liveBadge);
        cover.MouseLeftButtonUp += (_, _) => ToggleVideo();
        DockPanel.SetDock(cover, Dock.Left); head.Children.Add(cover);
        var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; artist.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); artist.Margin = new(0, 2, 0, 0);
        names.Children.Add(title); names.Children.Add(artist); head.Children.Add(names);
        playingPanel.Children.Add(head);
        var bar = new Grid { Height = 4, Margin = new(0, 14, 0, 0) }; bar.ColumnDefinitions.Add(done); bar.ColumnDefinitions.Add(remaining);
        var rail = new Border { CornerRadius = new(2) }; rail.SetResourceReference(Border.BackgroundProperty, "Line"); Grid.SetColumnSpan(rail, 2); bar.Children.Add(rail);
        var fill = new Border { CornerRadius = new(2) }; fill.SetResourceReference(Border.BackgroundProperty, "Accent"); bar.Children.Add(fill);
        var times = new DockPanel { Margin = new(0, 4, 0, 0) }; DockPanel.SetDock(duration, Dock.Right); times.Children.Add(duration); times.Children.Add(position);
        var progressPanel = new StackPanel(); progressPanel.Children.Add(bar); progressPanel.Children.Add(times); progress = progressPanel; playingPanel.Children.Add(progressPanel);
        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 6, 0, 0) };
        back10 = TextButton("−10", "10 saniye geri", () => _ = SeekAsync(-10)); controls.Children.Add(back10);
        previous = IconButton("\uE892", "Önceki parça", () => _ = MediaAsync("previous"), 16); controls.Children.Add(previous);
        play = IconButton("", "Oynat / duraklat", () => _ = MediaAsync(track?.Playing == true ? "pause" : "play"), 20); play.Content = playGlyph; controls.Children.Add(play);
        next = IconButton("\uE893", "Sonraki parça", () => _ = MediaAsync("next"), 16); controls.Children.Add(next);
        forward10 = TextButton("+10", "10 saniye ileri", () => _ = SeekAsync(10)); controls.Children.Add(forward10);
        playingPanel.Children.Add(controls);
        var appRow = new DockPanel { Margin = new(0, 2, 0, 0) };
        var appGlyph = Glyph("\uE767", 12); appGlyph.Margin = new(6, 0, 2, 0); DockPanel.SetDock(appGlyph, Dock.Left); appRow.Children.Add(appGlyph);
        appLabel.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); DockPanel.SetDock(appLabel, Dock.Right); appRow.Children.Add(appLabel);
        appRow.Children.Add(appVolume); playingPanel.Children.Add(appRow);
        System.Windows.Automation.AutomationProperties.SetName(appVolume, "Çalan uygulamanın sesi");
        appVolume.ValueChanged += (_, _) => { appVolume.ToolTip = $"%{appVolume.Value:0}"; if (!refreshing) { Touch(); appVolumeSave.Stop(); appVolumeSave.Start(); } };
        appVolumeSave.Tick += (_, _) => { appVolumeSave.Stop(); var id = track?.AppId; var level = (float)(appVolume.Value / 100); _ = Task.Run(() => { try { AppAudio.Set(id, level); } catch (COMException) { } }); };
        playingPanel.Visibility = Visibility.Collapsed; nowPlaying = playingPanel; music.Children.Add(playingPanel);
        var quiet = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var bigTime = new TextBlock { FontSize = 44, FontWeight = FontWeights.Light, DataContext = controller.ViewModel }; bigTime.SetBinding(TextBlock.TextProperty, new Binding(nameof(DesktopViewModel.Time)));
        System.Windows.Documents.Typography.SetNumeralAlignment(bigTime, FontNumeralAlignment.Tabular);
        var bigDate = Small(); bigDate.DataContext = controller.ViewModel; bigDate.SetBinding(TextBlock.TextProperty, new Binding(nameof(DesktopViewModel.Date)));
        var silence = Small(); silence.Text = "Şu an çalan bir şey yok"; silence.Margin = new(0, 8, 0, 0);
        quiet.Children.Add(bigDate); quiet.Children.Add(bigTime); quiet.Children.Add(silence);
        // Nothing playing: one tap opens (or installs) Noxelyra Music, whose video can play behind this card.
        musicLink = new Button { FontSize = 11, Padding = new(10, 4, 10, 4), Margin = new(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        musicLink.Click += async (_, _) => { Touch(); musicLink.IsEnabled = false; controller.ViewModel.Status = MusicApp.Installed ? MusicApp.Open() : await MusicApp.InstallAsync(s => controller.ViewModel.Status = s, CancellationToken.None); musicLink.IsEnabled = true; MusicLinkText(); };
        quiet.Children.Add(musicLink); MusicLinkText();
        nothingPlaying = quiet; music.Children.Add(quiet);

        var side = new StackPanel(); Grid.SetColumn(side, 2); one.Children.Add(side);
        var weatherTop = new StackPanel { Orientation = Orientation.Horizontal }; weatherGlyph.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
        weatherTop.Children.Add(weatherGlyph); weatherTop.Children.Add(weatherTemp);
        weatherLine.TextTrimming = TextTrimming.CharacterEllipsis;
        weatherShown.Children.Add(weatherTop); weatherShown.Children.Add(weatherLine); weatherShown.Visibility = Visibility.Collapsed;
        var askTitle = new TextBlock { Text = "Hava durumu", FontSize = 12, FontWeight = FontWeights.SemiBold };
        var askRow = new DockPanel { Margin = new(0, 4, 0, 0) };
        var askGo = new Button { Content = "Göster", FontSize = 11, Padding = new(6, 2, 6, 2), Margin = new(4, 0, 0, 0) }; DockPanel.SetDock(askGo, Dock.Right);
        askRow.Children.Add(askGo); askRow.Children.Add(cityInput);
        var askNote = Small(); askNote.Text = "Şehir, Open-Meteo'ya gider."; askNote.FontSize = 10; askNote.TextWrapping = TextWrapping.Wrap;
        weatherAsk.Children.Add(askTitle); weatherAsk.Children.Add(askRow); weatherAsk.Children.Add(askNote);
        askGo.Click += async (_, _) => await SaveCityAsync();
        cityInput.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await SaveCityAsync(); } };
        System.Windows.Automation.AutomationProperties.SetName(cityInput, "Hava durumu şehri");
        var weatherBox = new Grid { Height = 64 }; weatherBox.Children.Add(weatherShown); weatherBox.Children.Add(weatherAsk); weatherBox.Children.Add(clockText); side.Children.Add(weatherBox);
        System.Windows.Documents.Typography.SetNumeralAlignment(clockText, FontNumeralAlignment.Tabular); clockText.Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        foreach (var (key, label) in new[] { ("cpu", "CPU"), ("gpu", "GPU"), ("ram", "RAM") }) side.Children.Add(StatRow(key, label));
        var timerRow = new DockPanel { Margin = new(0, 8, 0, 0) };
        timerStop = IconButton("\uE711", "Zamanlayıcıyı durdur", controller.StopTimer, 11); timerStop.Padding = new(6, 2, 6, 2); DockPanel.SetDock(timerStop, Dock.Right); timerRow.Children.Add(timerStop);
        timerButton = new Button { Padding = new(8, 3, 8, 3), FontSize = 11, HorizontalContentAlignment = HorizontalAlignment.Left };
        var timerContent = new StackPanel { Orientation = Orientation.Horizontal }; var clockGlyph = Glyph("\uE916", 12); clockGlyph.Margin = new(0, 0, 6, 0); timerContent.Children.Add(clockGlyph); timerContent.Children.Add(timerText);
        timerButton.Content = timerContent; timerButton.Click += (_, _) => controller.ShowTimerMenu(timerButton); timerRow.Children.Add(timerButton);
        side.Children.Add(timerRow);
        strip.Children.Add(one);

        // ---- Page 2: PC controls ----
        var two = new StackPanel { Width = PageWidth, Height = PageHeight };
        var sound = new DockPanel { Margin = new(0, 4, 0, 0) };
        muteButton = IconButton("", "Sesi kapat / aç", ToggleMute, 16); muteButton.Content = volumeGlyph; DockPanel.SetDock(muteButton, Dock.Left); sound.Children.Add(muteButton);
        micButton = IconButton("", "Mikrofonu kapat / aç", ToggleMicrophone, 16); micButton.Content = micGlyph; DockPanel.SetDock(micButton, Dock.Right); sound.Children.Add(micButton);
        volumeText.Width = 40; volumeText.TextAlignment = TextAlignment.Right; DockPanel.SetDock(volumeText, Dock.Right); sound.Children.Add(volumeText);
        sound.Children.Add(volume); two.Children.Add(sound);
        System.Windows.Automation.AutomationProperties.SetName(volume, "Ses düzeyi");
        volume.ValueChanged += (_, _) => { volumeText.Text = $"%{volume.Value:0}"; if (!refreshing) TryAudio(() => audio.SetLevel((float)(volume.Value / 100))); };
        var light = new DockPanel { Margin = new(0, 12, 0, 0) };
        var sun = Glyph("\uE706", 16); sun.Margin = new(10, 0, 10, 0); DockPanel.SetDock(sun, Dock.Left); light.Children.Add(sun);
        brightnessText.Width = 40; brightnessText.TextAlignment = TextAlignment.Right; brightnessText.Margin = new(0, 0, 38, 0); DockPanel.SetDock(brightnessText, Dock.Right); light.Children.Add(brightnessText);
        var brightnessHost = new Grid(); brightnessHost.Children.Add(brightness); brightnessNote.VerticalAlignment = VerticalAlignment.Center; brightnessNote.Margin = new(8, 0, 0, 0); brightnessNote.TextTrimming = TextTrimming.CharacterEllipsis; brightnessHost.Children.Add(brightnessNote);
        light.Children.Add(brightnessHost); brightnessRow = light; two.Children.Add(light);
        System.Windows.Automation.AutomationProperties.SetName(brightness, "Monitör parlaklığı");
        brightness.ValueChanged += (_, _) => { brightnessText.Text = $"%{brightness.Value:0}"; if (!refreshing) { brightnessSave.Stop(); brightnessSave.Start(); } };
        brightnessSave.Tick += (_, _) => { brightnessSave.Stop(); var monitor = brightnessMonitor; var value = (int)brightness.Value; _ = Task.Run(() => MonitorBrightness.Set(monitor, value)); };
        var actions = new UniformGrid { Columns = 2, Margin = new(0, 16, 0, 0) };
        var shot = ActionButton("\uE722", "Ekran görüntüsü", TakeScreenshot); actions.Children.Add(shot);
        killButton = ActionButton("\uE711", "Uygulamayı kapat", () => _ = KillAsync()); actions.Children.Add(killButton);
        two.Children.Add(actions);
        apps.Margin = new(0, 14, 0, 0); two.Children.Add(apps);
        Canvas.SetLeft(two, PageWidth); strip.Children.Add(two);

        // ---- Arrow and page dots ----
        var arrow = new Button { Content = arrowGlyph, Background = Brushes.Transparent, BorderThickness = new(0), Padding = new(6, 30, 6, 30), Margin = new(8, 0, -6, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Diğer sayfa (Tab)" };
        System.Windows.Automation.AutomationProperties.SetName(arrow, "Diğer sayfa");
        arrow.Click += (_, _) => Flip(page == 0 ? 1 : 0); Grid.SetColumn(arrow, 1); layout.Children.Add(arrow);
        var footer = new Grid { Margin = new(0, 4, 0, 0) }; Grid.SetRow(footer, 1); Grid.SetColumnSpan(footer, 2); layout.Children.Add(footer);
        hint.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); footer.Children.Add(hint);
        var dots = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        dot1.Margin = new(3); dot2.Margin = new(3); dots.Children.Add(dot1); dots.Children.Add(dot2); footer.Children.Add(dots);

        SourceInitialized += (_, _) => { hwnd = new WindowInteropHelper(this).Handle; HwndSource.FromHwnd(hwnd)?.AddHook(Hook); };
        Deactivated += (_, _) => { if (!summoned && IsVisible) Leave(); };
        PreviewKeyDown += (_, e) => { if (!cityInput.IsKeyboardFocused && KeyOf(e.Key) is { } vk) { e.Handled = true; Press(vk); } };
        MouseMove += (_, _) => Touch();
        poll.Tick += async (_, _) => await RefreshAsync();
        idle.Tick += (_, _) => Leave();
        controller.TimerChanged += TimerChanged;
        Closed += (_, _) => { Stop(); controller.TimerChanged -= TimerChanged; };
    }

    // ---- Showing and hiding ----
    /// <summary>From the bar: a normal (focusable) card under the island that closes when focus moves elsewhere.</summary>
    internal void ShowUnder(FrameworkElement anchor)
    {
        ResetMotion();
        summoned = false; leaving = false; ShowActivated = true; Prepare(controller.LastAppWindow);
        var point = anchor.PointToScreen(new Point(anchor.ActualWidth / 2, anchor.ActualHeight));
        var monitor = MonitorAt((int)point.X, (int)point.Y);
        Show(); SetNoActivate(false);
        WindowPlacement.Move(hwnd, (int)(point.X - Width * monitor.Scale / 2), (int)(point.Y + (6 - Shadow) * monitor.Scale));
        Activate(); Arrive(); _ = ReadBrightnessAsync();
    }
    /// <summary>The island shortcut: drops in at the top of the screen that shows the focused window, without taking focus from it.</summary>
    internal void Summon()
    {
        ResetMotion();
        var foreground = WindowOps.Foreground;
        summoned = true; leaving = false; ShowActivated = false; Prepare(WindowOps.ProcessId(foreground) == Environment.ProcessId ? controller.LastAppWindow : foreground);
        var bounds = WindowOps.Read(foreground)?.Bounds;
        var monitor = bounds is { } b ? MonitorAt((int)(b.X + b.Width / 2), (int)(b.Y + b.Height / 2)) : controller.MonitorUnderPointer();
        Show(); SetNoActivate(true);
        WindowPlacement.Move(hwnd, (int)(monitor.X + (monitor.Width - Width * monitor.Scale) / 2), (int)(monitor.Y + (10 - Shadow) * monitor.Scale));
        WindowPlacement.RaiseTopmost(hwnd);
        RegisterKeys(); idle.Stop(); idle.Start();
        Arrive(); _ = ReadBrightnessAsync();
    }
    internal void Leave()
    {
        if (leaving || !IsVisible) return; leaving = true; hiddenAt = DateTime.UtcNow;
        Stop();
        if (!Motion.Enabled(controller.ViewModel)) { Hide(); return; }
        Motion.To(grow, ScaleTransform.ScaleYProperty, .9, 140);
        Motion.To(card, OpacityProperty, 0, 140, () => { Hide(); card.BeginAnimation(OpacityProperty, null); grow.BeginAnimation(ScaleTransform.ScaleYProperty, null); card.Opacity = 1; grow.ScaleY = 1; });
    }
    // A show during the closing fade must not be hidden by that fade's completion.
    private void ResetMotion()
    {
        Stop(); leaving = false;
        card.BeginAnimation(OpacityProperty, null); grow.BeginAnimation(ScaleTransform.ScaleYProperty, null); grow.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        card.Opacity = 1; grow.ScaleX = grow.ScaleY = 1;
    }
    private void Stop()
    {
        backdrop?.Leave(); videoScan?.Cancel(); videoKey = null; videoCrop = null; appVolumeSave.Stop();
        UnregisterKeys(); idle.Stop(); poll.Stop(); brightnessSave.Stop();
        weatherCancel?.Cancel(); weatherCancel?.Dispose(); weatherCancel = null;
        sampler?.Dispose(); sampler = null;
    }
    private void Prepare(nint target)
    {
        var palette = ThemePresets.Resolve(controller.ViewModel.Settings);
        var canvas = (Color)ColorConverter.ConvertFromString(palette.Canvas); canvas.A = 0xF2; opaqueBrush = new SolidColorBrush(canvas); canvas.A = 0x30; glassBrush = new SolidColorBrush(canvas); card.Background = opaqueBrush;
        hint.Text = summoned ? "←/→ 10 sn · ↑/↓ ses · Boşluk oynat · Tab sayfa · Esc kapat" : "";
        Flip(0, animate: false);
        killWindow = target; killArmedUntil = DateTime.MinValue; UpdateKillTarget();
        RenderApps(); TimerChanged(); MusicLinkText();
        weatherDue = DateTime.MinValue; UpdateWeatherSource();
        poll.Start(); _ = RefreshAsync();
    }
    private void Arrive()
    {
        if (!Motion.Enabled(controller.ViewModel)) { card.Opacity = 1; grow.ScaleY = 1; return; }
        card.Opacity = 0; grow.ScaleY = .88; grow.ScaleX = .96;
        Motion.To(card, OpacityProperty, 1, 180); Motion.To(grow, ScaleTransform.ScaleYProperty, 1, 300); Motion.To(grow, ScaleTransform.ScaleXProperty, 1, 300);
    }
    private MonitorInfo MonitorAt(int x, int y) => controller.ViewModel.Monitors.FirstOrDefault(m => x >= m.X && x < m.X + m.Width && y >= m.Y && y < m.Y + m.Height) ?? controller.ViewModel.Monitors.First(m => m.Primary);
    // WS_EX_NOACTIVATE keeps a game in front while the card is clicked; WS_EX_TOOLWINDOW keeps the card out of Alt+Tab.
    private void SetNoActivate(bool on)
    {
        var style = GetWindowLongPtr(hwnd, -20);
        style = on ? style | 0x08000000 | 0x80 : (style & ~0x08000000) | 0x80;
        SetWindowLongPtr(hwnd, -20, style);
    }
    private void Flip(int target, bool animate = true)
    {
        page = target; arrowGlyph.Text = page == 0 ? "\uE76C" : "\uE76B";
        dot1.SetResourceReference(Shape.FillProperty, page == 0 ? "Accent" : "Line"); dot2.SetResourceReference(Shape.FillProperty, page == 1 ? "Accent" : "Line");
        if (animate && Motion.Enabled(controller.ViewModel)) Motion.To(slide, TranslateTransform.XProperty, -page * PageWidth, 320);
        else { slide.BeginAnimation(TranslateTransform.XProperty, null); slide.X = -page * PageWidth; }
        ApplyBackground();
    }
    private void Touch() { if (summoned && IsVisible) { idle.Stop(); idle.Start(); } }

    // ---- Keys: registered while summoned (the game keeps focus), handled directly when the card has focus ----
    private void RegisterKeys()
    {
        UnregisterKeys(); controller.CardKeysActive(true);
        for (int i = 0; i < GameKeys.Length; i++)
        {
            var key = GameKeys[i]; var repeat = key is 0x25 or 0x27 or 0x26 or 0x28;
            if (RegisterHotKey(hwnd, 780 + i, repeat ? 0u : 0x4000u, (uint)key)) registered.Add(780 + i);
            else controller.Log.Write("island-key-busy-" + key);
        }
    }
    private void UnregisterKeys() { controller.CardKeysActive(false); foreach (var id in registered) UnregisterHotKey(hwnd, id); registered.Clear(); }
    private nint Hook(nint window, int message, nint w, nint l, ref bool handled)
    {
        if (message == 0x312 && w >= 780 && w < 780 + GameKeys.Length) { Press(GameKeys[w - 780]); handled = true; }
        return 0;
    }
    private static int? KeyOf(Key key) => key switch { Key.Left => 0x25, Key.Right => 0x27, Key.Up => 0x26, Key.Down => 0x28, Key.Space => 0x20, Key.Tab => 0x09, Key.Escape => 0x1B, _ => null };
    internal void Press(int key)
    {
        Touch();
        switch (key)
        {
            case 0x25: _ = SeekAsync(-10); break;
            case 0x27: _ = SeekAsync(10); break;
            case 0x26: ChangeVolume(5); break;
            case 0x28: ChangeVolume(-5); break;
            case 0x20: _ = MediaAsync(track?.Playing == true ? "pause" : "play"); break;
            case 0x09: Flip(page == 0 ? 1 : 0); break;
            case 0x1B: Leave(); break;
        }
    }

    // ---- Live data ----
    private async Task RefreshAsync()
    {
        if (polling || !IsVisible) return; polling = true;
        try
        {
            refreshing = true;
            try
            {
                var state = audio.Read(); var level = Math.Round(state.Level * 100);
                if (!volume.IsMouseCaptureWithin) volume.Value = level;
                volumeText.Text = state.Muted ? "Sessiz" : $"%{level:0}"; volumeGlyph.Text = state.Muted ? "\uE74F" : "\uE767"; volume.IsEnabled = muteButton.IsEnabled = true;
            }
            catch (Exception ex) when (ex is COMException or InvalidCastException or UnauthorizedAccessException) { volume.IsEnabled = muteButton.IsEnabled = false; volumeText.Text = "Yok"; }
            try
            {
                var mic = audio.ReadMicrophone(); micButton.IsEnabled = true;
                micGlyph.Text = mic.Muted ? "\uEC54" : "\uE720"; micButton.ToolTip = mic.Muted ? "Mikrofon kapalı · aç" : "Mikrofon açık · kapat";
                micButton.SetResourceReference(Control.BackgroundProperty, mic.Muted ? "Selection" : "Card");
            }
            catch (Exception ex) when (ex is COMException or InvalidCastException or UnauthorizedAccessException) { micButton.IsEnabled = false; micButton.ToolTip = "Mikrofon bulunamadı"; }
            finally { refreshing = false; }
            UpdateStats(); TimerChanged(); clockText.Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
            UpdateProgress();
            if (DateTime.UtcNow >= weatherDue) _ = LoadWeatherAsync();
            try { track = await media.ReadAsync(); trackRead = DateTime.UtcNow; }
            catch (Exception ex) when (ex is COMException or InvalidOperationException or UnauthorizedAccessException) { track = null; }
            ShowTrack();
            await RefreshAppAsync();
        }
        finally { polling = false; }
    }
    // The playing app's own volume and window. A browser shares one volume (and window) across its tabs.
    private async Task RefreshAppAsync()
    {
        var id = track?.AppId;
        AppVolume? level = null;
        try { if (id is not null) level = await Task.Run(() => AppAudio.Read(id)); } catch (COMException) { }
        appVolume.IsEnabled = level is not null;
        refreshing = true;
        if (level is not null && !appVolume.IsMouseCaptureWithin) appVolume.Value = Math.Round(level.Level * 100);
        refreshing = false;
        appLabel.Text = level?.Label ?? "Ses oturumu yok";
        appLabel.ToolTip = level is null ? "Uygulama şu an ses çalmıyor." : level.Label.StartsWith("tüm ", StringComparison.Ordinal) ? "Windows sekme başına ses vermez; bu bar tarayıcının tamamını ayarlar." : level.Label + " uygulamasının sesi";
        playerWindow = FindPlayerWindow();
        liveBadge.Visibility = playerWindow != 0 ? Visibility.Visible : Visibility.Collapsed;
        _ = UpdateVideoAsync();
    }
    private nint FindPlayerWindow()
    {
        if (track?.AppId is not { } id) return 0;
        var candidates = controller.FreshWindows().Where(w => IslandRules.MatchesApp(id, w.Executable is { } exe ? System.IO.Path.GetFileNameWithoutExtension(exe) : null)).ToList();
        // A minimized window has no picture; prefer a visible one (a mini player) that names the track.
        bool Titled(AppWindow w) => !string.IsNullOrWhiteSpace(track.Title) && w.Title.Contains(track.Title, StringComparison.OrdinalIgnoreCase);
        var pick = candidates.FirstOrDefault(w => !w.Minimized && Titled(w)) ?? candidates.FirstOrDefault(w => !w.Minimized) ?? candidates.FirstOrDefault(Titled) ?? candidates.FirstOrDefault();
        return pick?.Handle ?? 0;
    }
    // Find the video area once per track (and again if the player window is resized), then keep it behind page 1.
    private async Task UpdateVideoAsync()
    {
        if (!videoOn || track is not { Playing: true } || playerWindow == 0 || !IsVisible) { ApplyBackground(); return; }
        var info = WindowOps.Read(playerWindow);
        var size = info is null ? (0d, 0d) : (info.Bounds.Width, info.Bounds.Height);
        var key = track.AppId + "|" + track.Title + "|" + playerWindow;
        // Not found yet (paused, loading, ad): look again every 15 s.
        if (key == videoKey && size == videoWindowSize && (videoCrop is not null || DateTime.UtcNow - videoScanned < TimeSpan.FromSeconds(15))) { ApplyBackground(); return; }
        videoKey = key; videoWindowSize = size; videoScanned = DateTime.UtcNow;
        videoCrop = knownCrops.TryGetValue((playerWindow, size.Item1, size.Item2), out var known) ? known : null; ApplyBackground();
        videoScan?.Cancel(); var scan = videoScan = new CancellationTokenSource(); var window = playerWindow;
        var found = await VideoRegion.FindAsync(window, scan.Token, line => controller.Log.Write("island-video " + line));
        controller.Log.Write(found is null ? "island-video none" : "island-video found");
        if (scan.IsCancellationRequested || key != videoKey) return;
        // A rescan that sees no motion (paused, a still frame) keeps the remembered area.
        if (found is { } area) { videoCrop = area; knownCrops[(window, size.Item1, size.Item2)] = area; }
        ApplyBackground();
    }
    private void ApplyBackground()
    {
        var front = page == 0 && IsVisible && !leaving;
        var video = front && videoOn && videoCrop is not null && playerWindow != 0 && WindowOps.Read(playerWindow) is { Minimized: false };
        if (video && WindowOps.Read(hwnd)?.Bounds is { } b)
        {
            var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            backdrop ??= new VideoBackdrop();
            var inset = Shadow + VideoInset;
            backdrop.Present(hwnd, playerWindow, videoCrop!.Value, (int)Math.Round(b.X + inset * scale), (int)Math.Round(b.Y + inset * scale), (int)Math.Round((CardWidth - 2 * VideoInset) * scale), (int)Math.Round((CardHeight - 2 * VideoInset) * scale), (int)((28 - VideoInset) * scale));
            card.Background = glassBrush; blurArt.Visibility = Visibility.Collapsed; shade.Visibility = Visibility.Visible;
            videoRim.BorderBrush = opaqueBrush; videoRim.Visibility = Visibility.Visible;
            return;
        }
        backdrop?.Leave(); videoRim.Visibility = Visibility.Collapsed;
        var art = front && videoOn && artShown is not null && nowPlaying.Visibility == Visibility.Visible;
        blurArt.Source = art ? artShown : null; blurArt.Visibility = art ? Visibility.Visible : Visibility.Collapsed;
        shade.Visibility = art ? Visibility.Visible : Visibility.Collapsed; card.Background = opaqueBrush;
    }
    private void ToggleVideo()
    {
        Touch(); videoOn = !videoOn; videoKey = null;
        controller.ViewModel.Status = videoOn ? "Ada arka planında video açık." : "Ada arka planında video kapalı.";
        _ = UpdateVideoAsync();
    }
    private void ShowTrack()
    {
        var has = track is not null && (!string.IsNullOrWhiteSpace(track.Title) || !string.IsNullOrWhiteSpace(track.Artist));
        nowPlaying.Visibility = has ? Visibility.Visible : Visibility.Collapsed; nothingPlaying.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
        if (!has || track is null) return;
        title.Text = string.IsNullOrWhiteSpace(track.Title) ? "Bilinmeyen parça" : track.Title; artist.Text = track.Artist;
        if (!ReferenceEquals(artShown, track.Artwork))
        {
            artShown = track.Artwork;
            if (artShown is null) { art.SetResourceReference(Border.BackgroundProperty, "Card"); artGlyph.Visibility = Visibility.Visible; }
            else { art.Background = new ImageBrush(artShown) { Stretch = Stretch.UniformToFill }; artGlyph.Visibility = Visibility.Collapsed; }
        }
        playGlyph.Text = track.Playing ? "\uE769" : "\uE768";
        play.IsEnabled = track.Playing ? track.CanPause : track.CanPlay; previous.IsEnabled = track.CanPrevious; next.IsEnabled = track.CanNext;
        back10.IsEnabled = forward10.IsEnabled = track.CanSeek;
        back10.ToolTip = forward10.ToolTip = track.CanSeek ? "10 saniye sar (←/→)" : "Bu uygulama sarmaya izin vermiyor";
        UpdateProgress();
    }
    private void UpdateProgress()
    {
        if (track is null || track.Duration <= TimeSpan.Zero) { progress.Visibility = Visibility.Hidden; return; }
        var at = track.Position + (track.Playing ? DateTime.UtcNow - trackRead : TimeSpan.Zero); if (at > track.Duration) at = track.Duration;
        progress.Visibility = Visibility.Visible;
        done.Width = new(at.TotalSeconds, GridUnitType.Star); remaining.Width = new(Math.Max(0, (track.Duration - at).TotalSeconds), GridUnitType.Star);
        position.Text = Clock(at); duration.Text = Clock(track.Duration);
    }
    private void UpdateStats()
    {
        var vm = controller.ViewModel;
        Stat("cpu", vm.CpuText.StartsWith('%') ? vm.CpuValue : null);
        Stat("ram", vm.MemoryText.StartsWith('%') ? vm.MemoryValue : null);
        _ = SampleGpuAsync();
    }
    private async Task SampleGpuAsync()
    {
        try
        {
            sampler ??= await Task.Run(() => new PerformanceSampler());
            var current = sampler; if (current is null || !IsVisible) return;
            var sample = await Task.Run(current.Sample);
            if (IsVisible) Stat("gpu", sample.Gpu);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or ObjectDisposedException) { Stat("gpu", null); }
    }
    private void Stat(string key, double? value)
    {
        var (fill, rest, text) = stats[key]; var v = Math.Clamp(value ?? 0, 0, 100);
        fill.Width = new(v, GridUnitType.Star); rest.Width = new(100 - v, GridUnitType.Star); text.Text = value is null ? "Yok" : $"%{v:0}";
    }
    private void TimerChanged()
    {
        var timer = controller.Timer;
        timerText.Text = timer is null ? "Zamanlayıcı" : timer.Text(DateTimeOffset.Now);
        timerStop.Visibility = timer is null ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---- Weather: the island's own city, else the first weather widget's city; only Open-Meteo, every 30 minutes while open ----
    private string City => controller.ViewModel.Settings.IslandCity is { Length: > 0 } city ? city
        : controller.ViewModel.Settings.Widgets.FirstOrDefault(w => w.Kind == "weather" && !string.IsNullOrWhiteSpace(w.Text))?.Text ?? "";
    private void UpdateWeatherSource()
    {
        var hasCity = City.Length > 0;
        weatherAsk.Visibility = hasCity ? Visibility.Collapsed : Visibility.Visible; weatherShown.Visibility = hasCity ? Visibility.Visible : Visibility.Collapsed;
        cityInput.IsEnabled = !summoned && !controller.IsSafeMode && !controller.ReadOnly;
        if (summoned && !hasCity) cityInput.Text = "";
        if (hasCity && WeatherService.Cached("island", City) is { } cached) { ShowWeather(cached); weatherDue = cached.Time.UtcDateTime.AddMinutes(30); }
        else if (hasCity) { weatherGlyph.Text = ""; weatherTemp.Text = "…"; weatherLine.Text = City; }
    }
    private async Task LoadWeatherAsync()
    {
        var city = City; if (city.Length == 0) { weatherDue = DateTime.MaxValue; return; }
        weatherDue = DateTime.UtcNow.AddMinutes(30);
        weatherCancel?.Cancel(); weatherCancel = new();
        try { ShowWeather(await WeatherService.ReadAsync("island", city, weatherCancel.Token)); }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidDataException or KeyNotFoundException or InvalidOperationException or FormatException)
        { weatherDue = DateTime.UtcNow.AddMinutes(5); weatherTemp.Text = "—"; weatherLine.Text = ex is InvalidDataException ? ex.Message : "Hava durumu alınamadı"; }
    }
    private void ShowWeather(WeatherService.Result result)
    {
        var (text, glyph) = OpenMeteo.Describe(result.Report.Code, result.Report.Day);
        weatherGlyph.Text = glyph; weatherTemp.Text = Math.Round(result.Report.Temperature).ToString("0", CultureInfo.InvariantCulture) + "°";
        weatherLine.Text = text + " · " + result.Place.Name; weatherLine.ToolTip = result.Place.Label + " · Veri: Open-Meteo.com";
    }
    private async Task SaveCityAsync()
    {
        string city;
        try { city = OpenMeteo.CleanCity(cityInput.Text); } catch (InvalidDataException ex) { controller.ViewModel.Status = ex.Message; return; }
        if (await controller.SetIslandCityAsync(city)) { weatherDue = DateTime.MinValue; UpdateWeatherSource(); _ = LoadWeatherAsync(); }
    }

    // ---- Controls ----
    private async Task MediaAsync(string action)
    {
        Touch();
        try { if (!await media.InvokeAsync(action)) controller.ViewModel.Status = "Medya uygulaması bu komutu kabul etmedi."; }
        catch (Exception ex) when (ex is COMException or InvalidOperationException) { controller.ViewModel.Status = "Medya oturumuna ulaşılamadı."; }
        await RefreshAsync();
    }
    private async Task SeekAsync(int seconds)
    {
        Touch();
        try { if (!await media.SeekAsync(TimeSpan.FromSeconds(seconds))) controller.ViewModel.Status = track is null ? "Çalan bir şey yok." : "Bu uygulama sarmaya izin vermiyor."; }
        catch (Exception ex) when (ex is COMException or InvalidOperationException) { controller.ViewModel.Status = "Medya oturumuna ulaşılamadı."; }
        await RefreshAsync();
    }
    private void ChangeVolume(int step) => TryAudio(() => { var state = audio.Read(); audio.SetLevel(Math.Clamp(state.Level + step / 100f, 0, 1)); if (state.Muted && step > 0) audio.SetMute(false); _ = RefreshAsync(); });
    private void ToggleMute() => TryAudio(() => { audio.SetMute(!audio.Read().Muted); _ = RefreshAsync(); });
    private void ToggleMicrophone() => TryAudio(() => { var muted = !audio.ReadMicrophone().Muted; audio.SetMicrophoneMute(muted); controller.ViewModel.Status = muted ? "Mikrofon kapatıldı." : "Mikrofon açıldı."; _ = RefreshAsync(); });
    private void TryAudio(Action action)
    {
        Touch();
        try { action(); }
        catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException or UnauthorizedAccessException) { controller.ViewModel.Status = "Ses aygıtı değiştirilemedi."; controller.Log.Write("island-audio-failed", ex); }
    }
    private async Task ReadBrightnessAsync()
    {
        brightnessNote.Text = "Okunuyor…"; brightness.Visibility = Visibility.Hidden; brightnessText.Text = "";
        var center = PointToScreenSafe();
        brightnessMonitor = MonitorBrightness.MonitorAt(center.X, center.Y);
        var monitor = brightnessMonitor;
        MonitorBrightness.Reading? reading = null;
        try { reading = await Task.Run(() => MonitorBrightness.Read(monitor)); } catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
        if (reading is null) { brightnessNote.Text = "Bu monitör DDC/CI parlaklık ayarını desteklemiyor"; brightnessRow.ToolTip = "Monitörün menüsünden DDC/CI açıksa burada çalışır."; return; }
        refreshing = true; brightness.Value = reading.Percent; refreshing = false;
        brightness.Visibility = Visibility.Visible; brightnessNote.Text = ""; brightnessText.Text = $"%{reading.Percent}";
    }
    private (int X, int Y) PointToScreenSafe()
    {
        try { var p = PointToScreen(new Point(Width / 2, Height / 2)); return ((int)p.X, (int)p.Y); }
        catch (InvalidOperationException) { var m = controller.MonitorUnderPointer(); return (m.X + m.Width / 2, m.Y + m.Height / 2); }
    }
    private void TakeScreenshot()
    {
        Leave();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try { Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true }); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { controller.ViewModel.Status = "Ekran alıntısı aracı açılamadı."; controller.Log.Write("screenclip-failed", ex); }
        };
        timer.Start();
    }
    // The app that had focus before the card (the game, when summoned). Two presses within 4 s: arm, then end it.
    private void UpdateKillTarget()
    {
        killProcess = killWindow == 0 ? 0 : WindowOps.ProcessId(killWindow); killName = null;
        try { if (killProcess > 0) using (var process = Process.GetProcessById(killProcess)) killName = process.ProcessName; } catch (ArgumentException) { killProcess = 0; }
        var allowed = IslandRules.CanTerminate(killName, killProcess, Environment.ProcessId);
        SetAction(killButton, "\uE711", allowed ? "Kapat: " + killName : "Kapatılacak uygulama yok");
        killButton.IsEnabled = allowed && !controller.IsSafeMode;
    }
    private async Task KillAsync()
    {
        Touch();
        if (!IslandRules.CanTerminate(killName, killProcess, Environment.ProcessId)) return;
        if (DateTime.UtcNow > killArmedUntil)
        {
            killArmedUntil = DateTime.UtcNow.AddSeconds(4); SetAction(killButton, "\uE7BA", "Emin misin? Tekrar bas");
            await Task.Delay(4100); if (DateTime.UtcNow > killArmedUntil && IsVisible) UpdateKillTarget();
            return;
        }
        killArmedUntil = DateTime.MinValue;
        try
        {
            using var process = Process.GetProcessById(killProcess);
            process.Kill(); controller.Log.Write("island-app-closed");
            controller.ViewModel.Status = killName + " kapatıldı.";
            killWindow = 0; UpdateKillTarget();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException or NotSupportedException)
        { controller.ViewModel.Status = killName + " kapatılamadı (yönetici olarak çalışıyor olabilir; Görev Yöneticisi'ni dene)."; controller.Log.Write("island-app-close-failed", ex); UpdateKillTarget(); }
    }
    private void RenderApps()
    {
        apps.Children.Clear();
        var pinned = controller.ViewModel.Shortcuts.Take(7).ToList();
        if (pinned.Count == 0) { var none = Small(); none.Text = "Dock'a uygulama sabitleyince burada görünür."; apps.Children.Add(none); return; }
        foreach (var shortcut in pinned)
        {
            var image = new Image { Width = 26, Height = 26 };
            var button = new Button { Content = image, Width = 46, Height = 42, Padding = new(0), Margin = new(0, 0, 6, 0), ToolTip = shortcut.Name };
            System.Windows.Automation.AutomationProperties.SetName(button, shortcut.Name + " aç");
            var item = shortcut; button.Click += (_, _) => { Leave(); controller.Launch(item); };
            apps.Children.Add(button);
            _ = LoadIconAsync(item.Target, image);
        }
    }
    private async Task LoadIconAsync(string target, Image image)
    {
        try { var asset = await icons.GetAsync(target); image.Source = asset.Icon; }
        catch (Exception) { /* The tooltip still names the app. */ }
    }

    // ---- Building blocks ----
    private FrameworkElement StatRow(string key, string label)
    {
        var row = new Grid { Margin = new(0, 6, 0, 0) };
        row.ColumnDefinitions.Add(new() { Width = new(36) }); row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = new(40) });
        var name = Small(); name.Text = label; row.Children.Add(name);
        var bar = new Grid { Height = 4, VerticalAlignment = VerticalAlignment.Center }; var fill = new ColumnDefinition { Width = new(0, GridUnitType.Star) }; var rest = new ColumnDefinition { Width = new(100, GridUnitType.Star) };
        bar.ColumnDefinitions.Add(fill); bar.ColumnDefinitions.Add(rest); Grid.SetColumn(bar, 1);
        var rail = new Border { CornerRadius = new(2) }; rail.SetResourceReference(Border.BackgroundProperty, "Line"); Grid.SetColumnSpan(rail, 2); bar.Children.Add(rail);
        var used = new Border { CornerRadius = new(2) }; used.SetResourceReference(Border.BackgroundProperty, key == "gpu" ? "Secondary" : "Accent"); bar.Children.Add(used);
        row.Children.Add(bar);
        var value = new TextBlock { FontSize = 11, TextAlignment = TextAlignment.Right, Text = "…" }; Grid.SetColumn(value, 2); row.Children.Add(value);
        stats[key] = (fill, rest, value);
        return row;
    }
    private Button ActionButton(string glyph, string label, Action action)
    {
        var button = new Button { Padding = new(10, 7, 10, 7), Margin = new(0, 0, 6, 0), HorizontalContentAlignment = HorizontalAlignment.Left };
        SetAction(button, glyph, label); button.Click += (_, _) => action();
        return button;
    }
    private static void SetAction(Button button, string glyph, string label)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal }; var icon = Glyph(glyph, 14); icon.Margin = new(0, 0, 8, 0);
        content.Children.Add(icon); content.Children.Add(new TextBlock { Text = label, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 150 });
        button.Content = content; System.Windows.Automation.AutomationProperties.SetName(button, label);
    }
    private static Button IconButton(string glyph, string name, Action action, double size)
    {
        var button = new Button { Content = Glyph(glyph, size), ToolTip = name, Padding = new(10, 6, 10, 6), Margin = new(1), Background = Brushes.Transparent, BorderThickness = new(0) };
        System.Windows.Automation.AutomationProperties.SetName(button, name); button.Click += (_, _) => action();
        return button;
    }
    private static Button TextButton(string text, string name, Action action)
    {
        var button = new Button { Content = new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold }, ToolTip = name, Padding = new(8, 6, 8, 6), Margin = new(1), Background = Brushes.Transparent, BorderThickness = new(0) };
        System.Windows.Automation.AutomationProperties.SetName(button, name); button.Click += (_, _) => action();
        return button;
    }
    private static Ellipse Dot() => new() { Width = 6, Height = 6 };
    private static TextBlock Glyph(string code, double size) => new() { Text = code, FontFamily = new FontFamily(IconFont), FontSize = size, VerticalAlignment = VerticalAlignment.Center };
    private static TextBlock Line(double size, bool bold = false) => new() { FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextTrimming = TextTrimming.CharacterEllipsis };
    private static TextBlock Small() { var text = new TextBlock { FontSize = 11 }; text.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); return text; }
    private static TextBlock Figure() { var block = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = 12 }; System.Windows.Documents.Typography.SetNumeralAlignment(block, FontNumeralAlignment.Tabular); return block; }
    private static string Clock(TimeSpan span) => span.TotalHours >= 1 ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}" : $"{(int)span.TotalMinutes}:{span.Seconds:00}";
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
}
