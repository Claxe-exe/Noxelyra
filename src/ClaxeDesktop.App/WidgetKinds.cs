using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

/// <summary>Live widget kinds: music card, weather, system graph and big clock. Each one works only while its window is visible.</summary>
public partial class WidgetWindow
{
    private const string IconFont = "Segoe Fluent Icons, Segoe MDL2 Assets";
    private Action? liveTick;
    private CancellationTokenSource? liveCancel;
    private PerformanceSampler? sampler;
    private int liveTicks;
    internal bool LiveRunning => liveTick is not null;
    internal static bool IsLive(string kind) => kind is "music" or "weather" or "graph" or "bigclock";
    private void StopLive() { liveTick = null; liveCancel?.Cancel(); liveCancel?.Dispose(); liveCancel = null; sampler?.Dispose(); sampler = null; }
    private void BuildLive(Panel stack)
    {
        liveCancel = new(); liveTicks = 0;
        switch (Model.Kind)
        {
            case "bigclock": BuildBigClock(); break;
            case "music": BuildMusic(stack); break;
            case "weather": BuildWeather(stack); break;
            case "graph": BuildGraph(stack); break;
        }
    }
    private void LiveTick() { liveTicks++; liveTick?.Invoke(); }

    // Big clock: scales with the card, no timers of its own (the controller already ticks once a second).
    private void BuildBigClock()
    {
        var column = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var date = Text("Date", 13, "Muted"); date.HorizontalAlignment = HorizontalAlignment.Center;
        var time = Text("Time", 84); time.FontWeight = FontWeights.Light; time.HorizontalAlignment = HorizontalAlignment.Center;
        System.Windows.Documents.Typography.SetNumeralAlignment(time, FontNumeralAlignment.Tabular);
        var greeting = Text("Greeting", 15, "Accent"); greeting.HorizontalAlignment = HorizontalAlignment.Center;
        column.Children.Add(date); column.Children.Add(time); column.Children.Add(greeting);
        body.Children.Clear(); body.Children.Add(new Viewbox { Child = column, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both });
    }

    // Music card: the same Windows media session the bar uses, with album art and a progress line.
    private void BuildMusic(Panel stack)
    {
        var media = new MediaService(); MediaState? state = null; var read = DateTime.UtcNow; var busy = false;
        var art = new Border { CornerRadius = new(18), Height = Math.Clamp(Model.Height - 230, 64, Model.Width - 32), Margin = new(0, 0, 0, 14) };
        art.SetResourceReference(Border.BackgroundProperty, "Card");
        var note = Glyph("\uE8D6", 34); note.HorizontalAlignment = HorizontalAlignment.Center; note.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); art.Child = note;
        var title = new TextBlock { FontSize = 16, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, Text = "Şu an çalan bir şey yok" };
        var artist = new TextBlock { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new(0, 2, 0, 10), Text = "Spotify, tarayıcı veya bir oynatıcıda müzik başlat." };
        artist.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        var track = new Grid { Height = 4 }; var done = new ColumnDefinition { Width = new GridLength(0, GridUnitType.Star) }; var left = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
        track.ColumnDefinitions.Add(done); track.ColumnDefinitions.Add(left);
        var rail = new Border { CornerRadius = new(2) }; rail.SetResourceReference(Border.BackgroundProperty, "Line"); Grid.SetColumnSpan(rail, 2); track.Children.Add(rail);
        var fill = new Border { CornerRadius = new(2) }; fill.SetResourceReference(Border.BackgroundProperty, "Accent"); track.Children.Add(fill);
        var times = new DockPanel { Margin = new(0, 4, 0, 6) }; var position = Small(); var duration = Small(); DockPanel.SetDock(duration, Dock.Right); times.Children.Add(duration); times.Children.Add(position);
        var progress = new StackPanel { Visibility = Visibility.Collapsed }; progress.Children.Add(track); progress.Children.Add(times);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        Button previous = null!, play = null!, next = null!; ImageSource? shown = null;
        Button Control(string glyph, string name, double size, Func<string> action)
        {
            var button = new Button { Content = Glyph(glyph, size), Padding = new(14, 8, 14, 8), Background = Brushes.Transparent, BorderThickness = new(0), ToolTip = name, IsEnabled = false };
            System.Windows.Automation.AutomationProperties.SetName(button, name);
            button.Click += async (_, _) =>
            {
                try { if (!await media.InvokeAsync(action())) vm.Status = "Medya uygulaması bu komutu kabul etmedi."; }
                catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException) { vm.Status = "Medya oturumuna ulaşılamadı."; }
                await Refresh();
            };
            buttons.Children.Add(button); return button;
        }
        previous = Control("\uE892", "Önceki parça", 16, () => "previous");
        play = Control("\uE768", "Oynat / duraklat", 22, () => state?.Playing == true ? "pause" : "play");
        next = Control("\uE893", "Sonraki parça", 16, () => "next");
        stack.Children.Add(art); stack.Children.Add(title); stack.Children.Add(artist); stack.Children.Add(progress); stack.Children.Add(buttons);
        void Progress()
        {
            if (state is null || state.Duration <= TimeSpan.Zero) { progress.Visibility = Visibility.Collapsed; return; }
            var at = state.Position + (state.Playing ? DateTime.UtcNow - read : TimeSpan.Zero); if (at > state.Duration) at = state.Duration;
            progress.Visibility = Visibility.Visible;
            done.Width = new GridLength(at.TotalSeconds, GridUnitType.Star); left.Width = new GridLength(Math.Max(0, (state.Duration - at).TotalSeconds), GridUnitType.Star);
            position.Text = Clock(at); duration.Text = Clock(state.Duration);
        }
        async Task Refresh()
        {
            if (busy) return; busy = true;
            try { state = await media.ReadAsync(); read = DateTime.UtcNow; }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException or UnauthorizedAccessException) { state = null; }
            finally { busy = false; }
            if (state is null || (string.IsNullOrWhiteSpace(state.Title) && string.IsNullOrWhiteSpace(state.Artist)))
            {
                state = null; title.Text = "Şu an çalan bir şey yok"; artist.Text = "Spotify, tarayıcı veya bir oynatıcıda müzik başlat.";
                art.SetResourceReference(Border.BackgroundProperty, "Card"); note.Visibility = Visibility.Visible; shown = null;
                previous.IsEnabled = play.IsEnabled = next.IsEnabled = false; progress.Visibility = Visibility.Collapsed; return;
            }
            title.Text = string.IsNullOrWhiteSpace(state.Title) ? "Bilinmeyen parça" : state.Title; artist.Text = state.Artist;
            if (!ReferenceEquals(shown, state.Artwork))
            {
                shown = state.Artwork;
                if (shown is null) { art.SetResourceReference(Border.BackgroundProperty, "Card"); note.Visibility = Visibility.Visible; }
                else { art.Background = new ImageBrush(shown) { Stretch = Stretch.UniformToFill }; note.Visibility = Visibility.Collapsed; }
            }
            ((TextBlock)play.Content).Text = state.Playing ? "\uE769" : "\uE768";
            play.IsEnabled = state.Playing ? state.CanPause : state.CanPlay; previous.IsEnabled = state.CanPrevious; next.IsEnabled = state.CanNext;
            Progress();
        }
        liveTick = () => { if (liveTicks % 2 == 0) _ = Refresh(); else Progress(); };
        _ = Refresh();
    }

    // Weather: only after the user names a city; only the two Open-Meteo hosts; refreshed every 30 minutes while visible.
    private void BuildWeather(Panel stack)
    {
        var city = Model.Text?.Trim() ?? "";
        if (city.Length == 0) { WeatherEntry(stack, ""); return; }
        var view = new StackPanel(); stack.Children.Add(view);
        var footer = new DockPanel { Margin = new(0, 12, 0, 0) };
        var change = new Button { Content = "Şehri değiştir", Padding = new(8, 3, 8, 3), FontSize = 11 }; DockPanel.SetDock(change, Dock.Right); footer.Children.Add(change);
        var source = Small(); source.TextWrapping = TextWrapping.Wrap; footer.Children.Add(source);
        stack.Children.Add(footer);
        change.Click += (_, _) => { stack.Children.Clear(); StopLive(); liveCancel = new(); WeatherEntry(stack, city); };
        var due = DateTime.MinValue; var busy = false;
        void Render(WeatherService.Result result)
        {
            view.Children.Clear();
            var report = result.Report; var (text, glyph) = OpenMeteo.Describe(report.Code, report.Day);
            var top = new DockPanel();
            var icon = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe UI Symbol"), FontSize = 44, Margin = new(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
            icon.SetResourceReference(TextBlock.ForegroundProperty, "Accent"); DockPanel.SetDock(icon, Dock.Left); top.Children.Add(icon);
            var now = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            now.Children.Add(new TextBlock { Text = Degrees(report.Temperature), FontSize = 40, FontWeight = FontWeights.Light });
            now.Children.Add(new TextBlock { Text = text, FontSize = 14, FontWeight = FontWeights.SemiBold });
            top.Children.Add(now); view.Children.Add(top);
            var place = Small(); place.Text = result.Place.Label; place.Margin = new(0, 6, 0, 0); place.TextTrimming = TextTrimming.CharacterEllipsis; view.Children.Add(place);
            var details = new TextBlock { Text = $"Hissedilen {Degrees(report.FeelsLike)} · Nem %{report.Humidity} · Rüzgâr {report.Wind.ToString("0", CultureInfo.InvariantCulture)} km/sa", FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new(0, 6, 0, 12) };
            view.Children.Add(details);
            var days = new UniformGrid { Columns = Math.Max(1, report.Days.Count) };
            foreach (var day in report.Days)
            {
                var cell = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                var name = Small(); name.HorizontalAlignment = HorizontalAlignment.Center;
                name.Text = day.Date == DateOnly.FromDateTime(DateTime.Today) ? "Bugün" : day.Date.ToString("ddd", CultureInfo.GetCultureInfo("tr-TR"));
                cell.Children.Add(name);
                var dayIcon = new TextBlock { Text = OpenMeteo.Describe(day.Code).Glyph, FontFamily = new FontFamily("Segoe UI Symbol"), FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 4, 0, 4), ToolTip = OpenMeteo.Describe(day.Code).Text };
                cell.Children.Add(dayIcon);
                cell.Children.Add(new TextBlock { Text = $"{Degrees(day.Max)} / {Degrees(day.Min)}", FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center });
                days.Children.Add(cell);
            }
            view.Children.Add(days);
            source.Text = "Veri: Open-Meteo.com · " + result.Time.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
        }
        async Task Fetch()
        {
            if (busy || liveCancel is not { } cancel) return; busy = true;
            if (view.Children.Count == 0) { var wait = Small(); wait.Text = "Hava durumu alınıyor…"; view.Children.Add(wait); }
            try { var result = await WeatherService.ReadAsync(Model.Id, city, cancel.Token); Render(result); due = DateTime.UtcNow.AddMinutes(30); }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException or KeyNotFoundException or InvalidOperationException or FormatException)
            {
                due = DateTime.UtcNow.AddMinutes(5);
                var reason = ex is HttpRequestException or TaskCanceledException ? "Open-Meteo'ya ulaşılamadı; 5 dakika sonra yeniden denenecek." : ex is InvalidDataException ? ex.Message : "Yanıt okunamadı; 5 dakika sonra yeniden denenecek.";
                if (WeatherService.Cached(Model.Id, city) is null) { view.Children.Clear(); var error = new TextBlock { Text = reason, TextWrapping = TextWrapping.Wrap }; view.Children.Add(error); }
                source.Text = reason; workspace.WebStatus("Hava durumu: " + reason);
            }
            finally { busy = false; }
        }
        if (WeatherService.Cached(Model.Id, city) is { } cached) { Render(cached); due = cached.Time.UtcDateTime.AddMinutes(30); }
        liveTick = () => { if (DateTime.UtcNow >= due) _ = Fetch(); };
        if (DateTime.UtcNow >= due) _ = Fetch();
    }
    private void WeatherEntry(Panel stack, string current)
    {
        stack.Children.Add(new TextBlock { Text = "Hangi şehrin havası?", FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new(0, 0, 0, 8) });
        var input = new TextBox { Text = current, MaxLength = OpenMeteo.MaxCityLength };
        System.Windows.Automation.AutomationProperties.SetName(input, "Şehir adı");
        var show = new Button { Content = "Göster", HorizontalAlignment = HorizontalAlignment.Left, Margin = new(0, 8, 0, 0), Padding = new(14, 6, 14, 6) };
        var notice = new TextBlock { Text = "Yazdığın şehir adı Open-Meteo'ya (HTTPS) gönderilir; hesap, anahtar veya konum izni yok. Veri 30 dakikada bir, yalnız widget görünürken yenilenir.", TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new(0, 10, 0, 0) };
        notice.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new(0, 8, 0, 0) };
        async Task Submit()
        {
            string city;
            try { city = OpenMeteo.CleanCity(input.Text); } catch (InvalidDataException ex) { error.Text = ex.Message; return; }
            show.IsEnabled = false;
            if (!await workspace.SetTextAsync(Model.Id, city)) { error.Text = "Kaydedilemedi: " + vm.Status; show.IsEnabled = true; }
        }
        show.Click += async (_, _) => await Submit();
        input.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await Submit(); } };
        stack.Children.Add(input); stack.Children.Add(show); stack.Children.Add(error); stack.Children.Add(notice);
    }

    // System graph: one minute of CPU, GPU, RAM, disk and network. Missing counters say so instead of drawing zeros.
    private void BuildGraph(Panel stack)
    {
        var rows = new (string Key, string Label, string Brush)[] { ("cpu", "İşlemci", "Accent"), ("gpu", "Ekran kartı", "Secondary"), ("ram", "Bellek", "Accent"), ("disk", "Disk", "Secondary"), ("net", "Ağ", "Accent") };
        var history = rows.ToDictionary(r => r.Key, _ => new MetricHistory(60));
        var labels = new Dictionary<string, TextBlock>(); var canvases = new Dictionary<string, Canvas>();
        foreach (var (key, label, brush) in rows)
        {
            var head = new DockPanel { Margin = new(0, 4, 0, 2) };
            var value = Small(); value.Text = "…"; DockPanel.SetDock(value, Dock.Right); head.Children.Add(value); head.Children.Add(new TextBlock { Text = label, FontSize = 12 });
            var canvas = new Canvas { Height = 30, ClipToBounds = true, Margin = new(0, 0, 0, 6), Tag = brush };
            stack.Children.Add(head); stack.Children.Add(canvas); labels[key] = value; canvases[key] = canvas;
            canvas.SizeChanged += (_, _) => Draw(canvas, history[key], key == "net" ? history[key].Peak(64 * 1024) : 100);
        }
        var busy = false;
        async Task Sample()
        {
            if (busy) return; busy = true;
            try
            {
                sampler ??= await Task.Run(() => new PerformanceSampler());
                var cancel = liveCancel; var current = sampler; if (cancel is null || current is null) return;
                var sample = await Task.Run(current.Sample);
                if (cancel.IsCancellationRequested) return;
                history["cpu"].Add(vm.CpuText.StartsWith('%') ? vm.CpuValue : null); history["ram"].Add(vm.MemoryText.StartsWith('%') ? vm.MemoryValue : null);
                history["gpu"].Add(sample.Gpu); history["disk"].Add(sample.Disk); history["net"].Add(sample.NetworkBytesPerSecond);
                foreach (var (key, _, _) in rows)
                {
                    var latest = history[key].Latest;
                    labels[key].Text = latest is not { } v ? "Yok" : key == "net" ? Rate(v) : $"%{v:0}";
                    Draw(canvases[key], history[key], key == "net" ? history[key].Peak(64 * 1024) : 100);
                }
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or ObjectDisposedException) { labels["gpu"].Text = labels["disk"].Text = labels["net"].Text = "Yok"; }
            finally { busy = false; }
        }
        var note = Small(); note.Text = "Son 60 saniye · Windows performans sayaçları"; note.Margin = new(0, 4, 0, 0); stack.Children.Add(note);
        liveTick = () => _ = Sample();
        _ = Sample();
    }
    private void Draw(Canvas canvas, MetricHistory history, double peak)
    {
        canvas.Children.Clear();
        var width = canvas.ActualWidth; var height = canvas.ActualHeight; if (width <= 0 || height <= 0) return;
        var brush = (Brush)FindResource((string)canvas.Tag);
        foreach (var segment in history.Segments(width, height - 2, peak))
        {
            if (segment.Count < 2) continue;
            var line = new Polyline { Stroke = brush, StrokeThickness = 1.6, StrokeLineJoin = PenLineJoin.Round };
            var area = new Polygon { Fill = brush, Opacity = .16 };
            foreach (var (x, y) in segment) { line.Points.Add(new(x, y + 1)); area.Points.Add(new(x, y + 1)); }
            area.Points.Add(new(segment[^1].X, height)); area.Points.Add(new(segment[0].X, height));
            canvas.Children.Add(area); canvas.Children.Add(line);
        }
    }
    private static string Rate(double bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024:0.0} MB/sn" : $"{bytes / 1024:0} KB/sn";
    private static string Degrees(double value) => Math.Round(value).ToString("0", CultureInfo.InvariantCulture) + "°";
    private static string Clock(TimeSpan span) => span.TotalHours >= 1 ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}" : $"{(int)span.TotalMinutes}:{span.Seconds:00}";
    private static TextBlock Glyph(string code, double size) => new() { Text = code, FontFamily = new FontFamily(IconFont), FontSize = size, VerticalAlignment = VerticalAlignment.Center };
    private static TextBlock Small() { var text = new TextBlock { FontSize = 11 }; text.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); return text; }
}
