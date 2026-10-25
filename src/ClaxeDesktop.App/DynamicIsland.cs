using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;

/// <summary>
/// The bar's centre island, iPhone-style. It shows the clock and morphs (width animation and cross-fade) into the playing track,
/// a volume change, a Noxelyra notification or a running timer. A click expands the track into a card or opens the timer menu.
/// Nothing animates continuously: the bar is a layered window and every frame would repaint all of it.
/// </summary>
internal sealed class DynamicIsland : Grid
{
    private enum View { Clock, Music, Volume, Note, Timer }
    private const string IconFont = "Segoe Fluent Icons, Segoe MDL2 Assets";
    private const double VolumeTrack = 92, CardTrack = 214;
    private readonly DesktopViewModel vm;
    private readonly DesktopController controller;
    private readonly Func<string, Task> mediaCommand;
    private readonly Dictionary<View, FrameworkElement> views = [];
    private readonly TextBlock musicTitle = Text(13), noteTitle = Text(13), noteBody = Text(13), timerText = Figure(), volumeText = Figure();
    private readonly TextBlock volumeGlyph = Glyph("\uE767"), artGlyph = Glyph("\uE8D6"), timerGlyph = Glyph("\uE916");
    private readonly Border art = new() { Width = 22, Height = 22, CornerRadius = new(7), Margin = new(0, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Border volumeFill = new() { Height = 4, CornerRadius = new(2), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly DispatcherTimer expiry = new();
    private readonly Popup card = new() { AllowsTransparency = true, StaysOpen = false, Placement = PlacementMode.Custom };
    private readonly Border cardArt = new() { Width = 72, Height = 72, CornerRadius = new(14), Margin = new(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top };
    private readonly TextBlock cardTitle = Text(15), cardArtist = Text(12), cardPosition = Figure(), cardDuration = Figure(), cardPlay = Glyph("\uE768");
    private readonly Border cardFill = new() { Height = 4, CornerRadius = new(2), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly FrameworkElement cardProgress;
    private readonly Button cardPrevious, cardPlayButton, cardNext;
    private View shown = View.Clock;
    internal string State => shown.ToString();
    private View? transient;
    private MediaState? media;
    private DateTime mediaRead;
    private ImageSource? artSource;
    private double targetWidth = -1;
    /// <summary>The date next to the time; it gives way first on a narrow bar.</summary>
    public TextBlock Date { get; } = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 10, 0) };
    /// <summary>Off: a plain clock, as before the island existed.</summary>
    public bool Enabled { get; set { field = value; if (!value) { transient = null; card.IsOpen = false; } Render(); } } = true;
    /// <summary>Width the bar can give the island; set by the panel on every resize.</summary>
    public double Available { get; set { field = value; Resize(); } } = double.PositiveInfinity;
    public DynamicIsland(DesktopViewModel vm, DesktopController controller, Func<string, Task> mediaCommand)
    {
        this.vm = vm; this.controller = controller; this.mediaCommand = mediaCommand;
        ClipToBounds = true; Focusable = true;
        System.Windows.Automation.AutomationProperties.SetName(this, "Dinamik ada: saat, medya, ses, bildirim ve zamanlayıcı");

        Date.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        Date.SetBinding(TextBlock.TextProperty, new Binding(nameof(DesktopViewModel.ShortDate)));
        var time = Figure(); time.FontWeight = FontWeights.SemiBold; time.SetBinding(TextBlock.TextProperty, new Binding(nameof(DesktopViewModel.Time)));
        Add(View.Clock, Date, time);

        artGlyph.FontSize = 12; artGlyph.HorizontalAlignment = HorizontalAlignment.Center; art.Child = artGlyph; art.SetResourceReference(Border.BackgroundProperty, "Card");
        musicTitle.FontWeight = FontWeights.SemiBold;
        Add(View.Music, art, musicTitle);

        volumeGlyph.Margin = new(0, 0, 10, 0);
        var track = new Grid { Width = VolumeTrack, Height = 4, Margin = new(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        var rail = new Border { CornerRadius = new(2) }; rail.SetResourceReference(Border.BackgroundProperty, "Line");
        volumeFill.SetResourceReference(Border.BackgroundProperty, "Accent"); track.Children.Add(rail); track.Children.Add(volumeFill);
        volumeText.MinWidth = 44;
        Add(View.Volume, volumeGlyph, track, volumeText);

        var bell = Glyph("\uEA8F"); bell.Margin = new(0, 0, 9, 0); bell.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
        noteTitle.FontWeight = FontWeights.SemiBold; noteTitle.MaxWidth = 200;
        noteBody.Margin = new(8, 0, 0, 0); noteBody.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        Add(View.Note, bell, noteTitle, noteBody);

        timerGlyph.Margin = new(0, 0, 9, 0); timerGlyph.SetResourceReference(TextBlock.ForegroundProperty, "Accent"); timerText.FontWeight = FontWeights.SemiBold;
        Add(View.Timer, timerGlyph, timerText);
        views[View.Clock].Visibility = Visibility.Visible;

        expiry.Tick += (_, _) => { expiry.Stop(); transient = null; Render(); };
        (cardProgress, cardPrevious, cardPlayButton, cardNext) = BuildCard();
        Loaded += (_, _) => Render();
    }
    private void Add(View view, params UIElement[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(14, 0, 14, 0), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
        foreach (var child in children) row.Children.Add(child);
        views[view] = row; Children.Add(row);
    }
    private (FrameworkElement, Button, Button, Button) BuildCard()
    {
        var body = new Border { Width = 344, CornerRadius = new(22), BorderThickness = new(1), Padding = new(16), Margin = new(14, 4, 14, 18) };
        body.SetResourceReference(Border.BackgroundProperty, "Surface"); body.SetResourceReference(Border.BorderBrushProperty, "Line");
        body.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Opacity = .4, Color = Colors.Black };
        body.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "Text");
        var layout = new DockPanel(); body.Child = layout;
        cardArt.SetResourceReference(Border.BackgroundProperty, "Card");
        var bigNote = Glyph("\uE8D6"); bigNote.FontSize = 26; bigNote.HorizontalAlignment = HorizontalAlignment.Center; cardArt.Child = bigNote;
        DockPanel.SetDock(cardArt, Dock.Left); layout.Children.Add(cardArt);
        var details = new StackPanel(); layout.Children.Add(details);
        cardTitle.FontWeight = FontWeights.SemiBold; cardTitle.MaxWidth = CardTrack; cardArtist.MaxWidth = CardTrack; cardArtist.Margin = new(0, 2, 0, 10);
        cardArtist.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        details.Children.Add(cardTitle); details.Children.Add(cardArtist);

        var progress = new StackPanel();
        var track = new Grid { Width = CardTrack, Height = 4, HorizontalAlignment = HorizontalAlignment.Left };
        var rail = new Border { CornerRadius = new(2) }; rail.SetResourceReference(Border.BackgroundProperty, "Line");
        cardFill.SetResourceReference(Border.BackgroundProperty, "Accent"); track.Children.Add(rail); track.Children.Add(cardFill);
        var times = new DockPanel { Width = CardTrack, HorizontalAlignment = HorizontalAlignment.Left, Margin = new(0, 4, 0, 0) };
        cardPosition.FontSize = cardDuration.FontSize = 11; cardPosition.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); cardDuration.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        DockPanel.SetDock(cardDuration, Dock.Right); times.Children.Add(cardDuration); times.Children.Add(cardPosition);
        progress.Children.Add(track); progress.Children.Add(times); details.Children.Add(progress);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Margin = new(-8, 6, 0, 0) };
        Button Control(TextBlock glyph, string name, Func<string> action)
        {
            glyph.FontSize = 18;
            var button = new Button { Content = glyph, ToolTip = name, Padding = new(12, 6, 12, 6), Background = Brushes.Transparent, BorderThickness = new(0) };
            System.Windows.Automation.AutomationProperties.SetName(button, name);
            button.Click += async (_, _) => await mediaCommand(action());
            buttons.Children.Add(button); return button;
        }
        var previous = Control(Glyph("\uE892"), "Önceki parça", () => "previous");
        var play = Control(cardPlay, "Oynat / duraklat", () => media?.Playing == true ? "pause" : "play");
        var next = Control(Glyph("\uE893"), "Sonraki parça", () => "next");
        var timer = new Button { Content = Glyph("\uE916"), ToolTip = "Zamanlayıcı", Padding = new(12, 6, 12, 6), Margin = new(12, 0, 0, 0), Background = Brushes.Transparent, BorderThickness = new(0) };
        System.Windows.Automation.AutomationProperties.SetName(timer, "Zamanlayıcı menüsü");
        timer.Click += (_, _) => { card.IsOpen = false; OpenMenu(); };
        buttons.Children.Add(timer);
        details.Children.Add(buttons);

        card.Child = body; card.PlacementTarget = this;
        // Centred under the island, whatever its current width.
        card.CustomPopupPlacementCallback = (popup, target, _) => [new CustomPopupPlacement(new Point((target.Width - popup.Width) / 2, target.Height + 2), PopupPrimaryAxis.Horizontal)];
        card.Opened += (_, _) => UpdateCard();
        return (progress, previous, play, next);
    }
    /// <summary>The current media session from the panel's poll; null when nothing is published.</summary>
    public void ShowMedia(MediaState? state)
    {
        media = state is not null && (!string.IsNullOrWhiteSpace(state.Title) || !string.IsNullOrWhiteSpace(state.Artist)) ? state : null;
        mediaRead = DateTime.UtcNow;
        if (media is null) card.IsOpen = false;
        else
        {
            musicTitle.Text = string.IsNullOrWhiteSpace(media.Artist) ? media.Title : $"{media.Title} · {media.Artist}";
            if (!ReferenceEquals(media.Artwork, artSource))
            {
                artSource = media.Artwork;
                art.Background = artSource is null ? (Brush)FindResource("Card") : new ImageBrush(artSource) { Stretch = Stretch.UniformToFill };
                artGlyph.Visibility = artSource is null ? Visibility.Visible : Visibility.Collapsed;
            }
            if (card.IsOpen) UpdateCard();
        }
        Render();
    }
    public void ShowVolume(int level, bool muted, string glyph)
    {
        volumeGlyph.Text = glyph; volumeText.Text = muted ? "Sessiz" : $"%{level}";
        volumeFill.Width = muted ? 0 : VolumeTrack * Math.Clamp(level, 0, 100) / 100.0;
        Flash(View.Volume, 1800);
    }
    public void ShowNotification(NotificationItem item)
    {
        noteTitle.Text = item.Title; noteBody.Text = item.Body;
        noteBody.Visibility = string.IsNullOrWhiteSpace(item.Body) ? Visibility.Collapsed : Visibility.Visible;
        Flash(View.Note, 5000);
    }
    private void Flash(View view, int milliseconds)
    {
        if (!Enabled) return;
        transient = view; expiry.Stop(); expiry.Interval = TimeSpan.FromMilliseconds(milliseconds); expiry.Start();
        Render();
    }
    /// <summary>Once a second from the panel: timer text and the track position.</summary>
    public void Tick()
    {
        if (controller.Timer is { } timer) timerText.Text = timer.Text(DateTimeOffset.Now);
        if (card.IsOpen) UpdateProgress();
        Render();
    }
    private void Render()
    {
        if (controller.Timer is { } timer) { timerText.Text = timer.Text(DateTimeOffset.Now); timerGlyph.Text = timer.Stopwatch ? "\uE916" : "\uE823"; }
        var next = !Enabled ? View.Clock : transient ?? (controller.Timer is not null ? View.Timer : media is { Playing: true } ? View.Music : View.Clock);
        if (next != shown)
        {
            views[shown].Visibility = Visibility.Collapsed;
            var view = views[next]; view.Visibility = Visibility.Visible;
            if (Motion.Enabled(vm) && IsLoaded) { view.Opacity = 0; Motion.To(view, OpacityProperty, 1, 260, delay: 60); }
            shown = next;
        }
        Resize();
    }
    private void Resize()
    {
        if (views.Count == 0) return;
        var view = views[shown]; var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        musicTitle.MaxWidth = Math.Clamp(Available - 80, 60, 260);
        noteBody.MaxWidth = Math.Clamp(Available - 120 - Math.Min(noteTitle.DesiredSize.Width, 200), 0, 300);
        if (shown == View.Clock)
        {
            Date.Visibility = Visibility.Visible; view.Measure(infinite);
            if (view.DesiredSize.Width > Available) Date.Visibility = Visibility.Collapsed;
        }
        view.Measure(infinite);
        var target = Math.Ceiling(view.DesiredSize.Width);
        if (Math.Abs(target - targetWidth) < .5) return;
        var first = targetWidth < 0; targetWidth = target;
        if (!first && IsLoaded && Motion.Enabled(vm)) Motion.To(this, WidthProperty, target, 420);
        else { BeginAnimation(WidthProperty, null); Width = target; }
    }
    /// <summary>Click or Enter: a notification opens quick settings, the track opens its card, anything else the timer menu.</summary>
    public void Click()
    {
        if (shown == View.Note) { controller.ShowQuickSettings(); return; }
        if (media is not null && (shown == View.Music || shown == View.Volume)) { Expand(!card.IsOpen); return; }
        OpenMenu();
    }
    private void Expand(bool open)
    {
        card.PopupAnimation = Motion.Enabled(vm) ? PopupAnimation.Fade : PopupAnimation.None;
        card.IsOpen = open && media is not null;
    }
    public void OpenMenu()
    {
        var menu = new ContextMenu { PlacementTarget = this, Placement = PlacementMode.Bottom };
        void Add(string text, Action action) { var item = new MenuItem { Header = text }; item.Click += (_, _) => action(); menu.Items.Add(item); }
        if (media is not null) { Add("Çalan parça", () => Expand(true)); menu.Items.Add(new Separator()); }
        foreach (var minutes in IslandTimer.Presets) { var length = minutes; Add(length == 25 ? "25 dakika · odak" : $"{length} dakika", () => controller.StartTimer(length)); }
        Add("Kronometre", () => controller.StartTimer(null));
        if (controller.Timer is not null) Add("Zamanlayıcıyı durdur", controller.StopTimer);
        menu.Items.Add(new Separator());
        Add("Bildirimler ve hızlı ayarlar", controller.ShowQuickSettings);
        menu.IsOpen = true;
    }
    private void UpdateCard()
    {
        if (media is null) return;
        cardTitle.Text = string.IsNullOrWhiteSpace(media.Title) ? "Bilinmeyen parça" : media.Title;
        cardArtist.Text = media.Artist; cardArtist.Visibility = string.IsNullOrWhiteSpace(media.Artist) ? Visibility.Collapsed : Visibility.Visible;
        cardArt.Background = artSource is null ? (Brush)FindResource("Card") : new ImageBrush(artSource) { Stretch = Stretch.UniformToFill };
        cardArt.Child.Visibility = artSource is null ? Visibility.Visible : Visibility.Collapsed;
        cardPlay.Text = media.Playing ? "\uE769" : "\uE768";
        cardPlayButton.IsEnabled = media.Playing ? media.CanPause : media.CanPlay; cardPrevious.IsEnabled = media.CanPrevious; cardNext.IsEnabled = media.CanNext;
        UpdateProgress();
    }
    private void UpdateProgress()
    {
        if (media is null || media.Duration <= TimeSpan.Zero) { cardProgress.Visibility = Visibility.Collapsed; return; }
        var position = media.Position + (media.Playing ? DateTime.UtcNow - mediaRead : TimeSpan.Zero);
        if (position > media.Duration) position = media.Duration;
        cardProgress.Visibility = Visibility.Visible;
        cardFill.Width = CardTrack * position.TotalSeconds / media.Duration.TotalSeconds;
        cardPosition.Text = Clock(position); cardDuration.Text = Clock(media.Duration);
    }
    private static string Clock(TimeSpan span) => span.TotalHours >= 1 ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}" : $"{(int)span.TotalMinutes}:{span.Seconds:00}";
    private static TextBlock Glyph(string code) => new() { Text = code, FontFamily = new FontFamily(IconFont), FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
    private static TextBlock Text(double size) => new() { FontSize = size, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private static TextBlock Figure() { var block = new TextBlock { VerticalAlignment = VerticalAlignment.Center }; System.Windows.Documents.Typography.SetNumeralAlignment(block, FontNumeralAlignment.Tabular); return block; }
}
