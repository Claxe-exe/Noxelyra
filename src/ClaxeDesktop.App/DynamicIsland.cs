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
/// a volume change, a Noxelyra notification or a running timer. A click opens the two-page island card (track, weather, stats / PC controls).
/// Nothing animates continuously: the bar is a layered window and every frame would repaint all of it.
/// </summary>
internal sealed class DynamicIsland : Grid
{
    private enum View { Clock, Music, Volume, Note, Timer }
    private const string IconFont = "Segoe Fluent Icons, Segoe MDL2 Assets";
    private const double VolumeTrack = 92;
    private readonly DesktopViewModel vm;
    private readonly DesktopController controller;
    private readonly Dictionary<View, FrameworkElement> views = [];
    private readonly TextBlock musicTitle = Text(13), noteTitle = Text(13), noteBody = Text(13), timerText = Figure(), volumeText = Figure();
    private readonly TextBlock volumeGlyph = Glyph("\uE767"), artGlyph = Glyph("\uE8D6"), timerGlyph = Glyph("\uE916");
    private readonly Border art = new() { Width = 22, Height = 22, CornerRadius = new(7), Margin = new(0, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Border volumeFill = new() { Height = 4, CornerRadius = new(2), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly DispatcherTimer expiry = new();
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
    public bool Enabled { get; set { field = value; if (!value) transient = null; Render(); } } = true;
    /// <summary>Width the bar can give the island; set by the panel on every resize.</summary>
    public double Available { get; set { field = value; Resize(); } } = double.PositiveInfinity;
    public DynamicIsland(DesktopViewModel vm, DesktopController controller)
    {
        this.vm = vm; this.controller = controller;
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
        Loaded += (_, _) => Render();
    }
    private void Add(View view, params UIElement[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(14, 0, 14, 0), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
        foreach (var child in children) row.Children.Add(child);
        views[view] = row; Children.Add(row);
    }
    /// <summary>The current media session from the panel's poll; null when nothing is published.</summary>
    public void ShowMedia(MediaState? state)
    {
        media = state is not null && (!string.IsNullOrWhiteSpace(state.Title) || !string.IsNullOrWhiteSpace(state.Artist)) ? state : null;
        mediaRead = DateTime.UtcNow;
        if (media is not null)
        {
            musicTitle.Text = string.IsNullOrWhiteSpace(media.Artist) ? media.Title : $"{media.Title} · {media.Artist}";
            if (!ReferenceEquals(media.Artwork, artSource))
            {
                artSource = media.Artwork;
                art.Background = artSource is null ? (Brush)FindResource("Card") : new ImageBrush(artSource) { Stretch = Stretch.UniformToFill };
                artGlyph.Visibility = artSource is null ? Visibility.Visible : Visibility.Collapsed;
            }
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
    /// <summary>Click or Enter: the island card under the island, whatever the island shows.</summary>
    public void Click() => controller.ShowIslandCard(this);
    /// <summary>Right click: timer presets, the card and quick settings.</summary>
    public void OpenMenu() => controller.ShowTimerMenu(this);
    private static TextBlock Glyph(string code) => new() { Text = code, FontFamily = new FontFamily(IconFont), FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
    private static TextBlock Text(double size) => new() { FontSize = size, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private static TextBlock Figure() { var block = new TextBlock { VerticalAlignment = VerticalAlignment.Center }; System.Windows.Documents.Typography.SetNumeralAlignment(block, FontNumeralAlignment.Tabular); return block; }
}
