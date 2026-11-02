using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;

/// <summary>Everything about the look on one page: ready-made rices, themes with real previews, and a palette maker that always stays readable.</summary>
internal sealed class ThemeGalleryWindow : Window
{
    private readonly DesktopController controller;
    private readonly bool editable;
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 6, 0, 0) };
    private readonly WrapPanel sets = new(), themes = new(), saved = new();
    private readonly Border livePreview = new() { Margin = new(0, 0, 20, 0), VerticalAlignment = VerticalAlignment.Top };
    private readonly Slider hue = Scale(0, 360, 262), saturation = Scale(40, 90, 62), second = Scale(0, 360, 190), lift = Scale(0, 8, 0);
    private readonly TextBox name = new() { Text = "Benim paletim", MaxLength = 40, Padding = new(6, 4, 6, 4) };
    private Button? restore;
    internal ThemeGalleryWindow(DesktopController controller)
    {
        this.controller = controller; editable = !controller.IsSafeMode && !controller.ReadOnly;
        Title = "Noxelyra · Tema galerisi"; Width = 900; Height = 820; MinWidth = 640; MinHeight = 520; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty, "Canvas"); SetResourceReference(ForegroundProperty, "Text");
        Glass.PrepareFramed(this, controller.ViewModel.Settings); Motion.Attach(this, controller.ViewModel, () => controller.IsExiting);
        var root = new StackPanel { Margin = new(28) }; Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(SplitText.Make("Tema galerisi", 24, FontWeights.SemiBold, controller.ViewModel));
        status.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); status.Text = "Tıkladığın anda uygulanır. Hazır setten önceki görünüme tek tuşla dönebilirsin.";
        root.Children.Add(status);
        Section(root, "Hazır setler", "Renk, yüzey, köşe ve boyutlar birlikte. Widget düzenine ve dosyalarına dokunmaz.").Children.Add(sets);
        Section(root, "Temalar", "Yalnız renkleri değiştirir.").Children.Add(themes);
        BuildMaker(Section(root, "Kendi paletin", "Tonları seç; yazıların okunaklı kalmasını Noxelyra sağlar."));
        var more = Section(root, "Diğer", null); var actions = new WrapPanel(); more.Children.Add(actions);
        Action(actions, "Duvar kâğıdından renk al", async () => { await controller.UseWallpaperPaletteAsync(); Say(controller.ViewModel.Status); Render(); });
        Action(actions, "Rice dosyası içe / dışa aktar…", controller.ShowFullControls, always: true);
        restore = Action(actions, "Önceki görünüme dön", async () => { Say(await controller.RestoreRiceBackupAsync()); Render(); });
        Render();
    }
    private void Say(string message) => status.Text = message;
    private void Render()
    {
        var settings = controller.ViewModel.Settings;
        sets.Children.Clear();
        foreach (var preset in RicePresets.All)
        {
            var chosen = preset; var current = preset.Matches(settings);
            sets.Children.Add(Card(Preview(preset.Palette, preset.CornerRadius, preset.BorderWidth), preset.Name, preset.Description, current, async () => { Say(await controller.ApplyRicePresetAsync(chosen)); Render(); }));
        }
        themes.Children.Clear();
        foreach (var palette in ThemePresets.All)
        {
            var id = palette.Id;
            themes.Children.Add(Card(Preview(palette, settings.CornerRadius, settings.BorderWidth), palette.Name, null, settings.ThemeId == id, async () => { await controller.SetThemeAsync(id); Say(controller.ViewModel.Status); Render(); }));
        }
        if (settings.CustomPalette is { } custom)
            themes.Children.Add(Card(Preview(custom, settings.CornerRadius, settings.BorderWidth), custom.Name, null, settings.ThemeId == ThemePresets.WallpaperId, async () => { await controller.ApplyCustomPaletteAsync(custom, custom.Name + " uygulandı."); Say(controller.ViewModel.Status); Render(); }));
        saved.Children.Clear();
        foreach (var palette in settings.SavedPalettes)
        {
            var item = palette;
            var card = Card(Preview(palette, 12, 1, .7), palette.Name, null, settings.ThemeId == ThemePresets.WallpaperId && settings.CustomPalette == palette, async () => { await controller.ApplyCustomPaletteAsync(item, item.Name + " uygulandı."); Say(controller.ViewModel.Status); Render(); });
            var menu = new ContextMenu(); var remove = new MenuItem { Header = "Sil" }; remove.Click += async (_, _) => { await controller.SetSavedPalettesAsync(controller.ViewModel.Settings.SavedPalettes.Where(p => p != item).ToList()); Render(); }; menu.Items.Add(remove); card.ContextMenu = menu; card.ToolTip = "Sağ tık: sil";
            saved.Children.Add(card);
        }
        if (restore is not null) restore.IsEnabled = editable && controller.HasRiceBackup;
        RefreshMaker();
    }

    // ---- Palette maker ----
    private ThemePalette Built() => WallpaperPalette.Build(name.Text.Trim().Length == 0 ? "Benim paletim" : name.Text.Trim(), hue.Value, saturation.Value / 100, second.Value, lift.Value / 100);
    private void RefreshMaker() => livePreview.Child = Preview(Built(), 16, 1, 1.3);
    private void BuildMaker(StackPanel body)
    {
        var line = new DockPanel(); body.Children.Add(line);
        DockPanel.SetDock(livePreview, Dock.Left); line.Children.Add(livePreview);
        var fields = new StackPanel(); line.Children.Add(fields);
        Field(fields, "Vurgu tonu", hue, rainbow: true); Field(fields, "Doygunluk", saturation); Field(fields, "İkinci ton", second, rainbow: true); Field(fields, "Zemin aydınlığı", lift);
        fields.Children.Add(new TextBlock { Text = "Ad", Margin = new(0, 8, 0, 3), FontSize = 12 }); fields.Children.Add(name);
        System.Windows.Automation.AutomationProperties.SetName(name, "Palet adı");
        name.TextChanged += (_, _) => { if (IsLoaded) RefreshMaker(); };
        var actions = new WrapPanel { Margin = new(0, 10, 0, 0) }; fields.Children.Add(actions);
        Action(actions, "Uygula", async () => { var palette = Built(); await controller.ApplyCustomPaletteAsync(palette, palette.Name + " uygulandı."); Say(controller.ViewModel.Status); Render(); });
        Action(actions, "Kaydet ve uygula", async () =>
        {
            var palette = Built(); var list = controller.ViewModel.Settings.SavedPalettes.Where(p => p.Name != palette.Name).ToList();
            if (list.Count >= Settings.MaxSavedPalettes) { Say($"En fazla {Settings.MaxSavedPalettes} palet kaydedilir; önce birini sil."); return; }
            list.Add(palette); await controller.SetSavedPalettesAsync(list); await controller.ApplyCustomPaletteAsync(palette, palette.Name + " kaydedildi ve uygulandı."); Say(controller.ViewModel.Status); Render();
        });
        saved.Margin = new(0, 12, 0, 0); body.Children.Add(saved);
    }
    private void Field(Panel parent, string label, Slider slider, bool rainbow = false)
    {
        parent.Children.Add(new TextBlock { Text = label, FontSize = 12, Margin = new(0, 2, 0, 2) });
        if (rainbow)
        {
            var track = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(1, 0) };
            foreach (var at in new[] { 0, 60, 120, 180, 240, 300, 360 }) track.GradientStops.Add(new((Color)ColorConverter.ConvertFromString(WallpaperPalette.FromHsl(at, .75, .6)), at / 360d));
            // The hue slider's whole rail is the rainbow; a fill over it would hide the colours.
            slider.Background = track; slider.Foreground = Brushes.Transparent;
        }
        System.Windows.Automation.AutomationProperties.SetName(slider, label);
        slider.ValueChanged += (_, _) => { if (IsLoaded) RefreshMaker(); };
        parent.Children.Add(slider);
    }
    private static Slider Scale(double minimum, double maximum, double value) => new() { Minimum = minimum, Maximum = maximum, Value = value, Margin = new(0, 0, 0, 4) };

    // ---- Pieces ----
    private static StackPanel Section(Panel parent, string title, string? help)
    {
        parent.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new(0, 24, 0, help is null ? 8 : 2) });
        if (help is not null) { var note = new TextBlock { Text = help, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 10) }; note.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); parent.Children.Add(note); }
        var body = new StackPanel(); parent.Children.Add(body); return body;
    }
    private Button Action(Panel parent, string text, Action run, bool always = false)
    {
        var button = new Button { Content = text, Padding = new(14, 7, 14, 7), Margin = new(0, 0, 8, 8), IsEnabled = always || editable };
        button.Click += (_, _) => run(); parent.Children.Add(button); return button;
    }
    private Button Card(FrameworkElement preview, string title, string? description, bool current, Action run)
    {
        var stack = new StackPanel { Width = preview.Width + 8 };
        var ring = new Border { CornerRadius = new(13), BorderThickness = new(2), Padding = new(2), Child = preview, BorderBrush = Brushes.Transparent };
        if (current) ring.SetResourceReference(Border.BorderBrushProperty, "Accent");
        stack.Children.Add(ring);
        stack.Children.Add(new TextBlock { Text = current ? title + "  ·  seçili" : title, FontSize = 13, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new(4, 8, 4, 0) });
        if (description is not null) { var note = new TextBlock { Text = description, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new(4, 2, 4, 2) }; note.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); stack.Children.Add(note); }
        var button = new Button { Content = stack, Padding = new(6), Margin = new(0, 0, 8, 8), Style = (Style)FindResource("DockTile"), IsEnabled = editable, VerticalAlignment = VerticalAlignment.Top };
        System.Windows.Automation.AutomationProperties.SetName(button, title + (current ? ", seçili" : ""));
        button.Click += (_, _) => run();
        return button;
    }
    /// <summary>A small desktop drawn in the palette's own colours: bar, widget, window and dock.</summary>
    private static Border Preview(ThemePalette palette, int corner, double border, double zoom = 1)
    {
        Brush Paint(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        var radius = Math.Min(corner, 20) * .35 * zoom; var edge = Paint(palette.Line); var stroke = new Thickness(Math.Max(.5, border * .6));
        var scene = new Canvas { Width = 180 * zoom, Height = 104 * zoom, ClipToBounds = true };
        void Place(FrameworkElement part, double x, double y) { Canvas.SetLeft(part, x * zoom); Canvas.SetTop(part, y * zoom); scene.Children.Add(part); }
        Border Pill(double w, double h, string fill, bool outlined = true) => new() { Width = w * zoom, Height = h * zoom, CornerRadius = new(radius), Background = Paint(fill), BorderBrush = edge, BorderThickness = outlined ? stroke : new(0) };
        Place(Pill(164, 10, palette.Surface), 8, 6);
        Place(new Border { Width = 5 * zoom, Height = 5 * zoom, CornerRadius = new(3 * zoom), Background = Paint(palette.Accent) }, 12, 8.5);
        Place(Pill(34, 3, palette.Text, false), 21, 9.5);
        Place(Pill(26, 6, palette.Accent, false), 77, 8);
        // A window with a heading, body text and an accent button.
        var window = Pill(92, 56, palette.Card); Place(window, 12, 22);
        Place(Pill(46, 4, palette.Text, false), 19, 29); Place(Pill(72, 3, palette.Muted, false), 19, 38); Place(Pill(60, 3, palette.Muted, false), 19, 44);
        Place(Pill(26, 9, palette.Accent, false), 19, 58); Place(Pill(26, 9, palette.Hover, false), 49, 58);
        // A widget with the secondary colour.
        Place(Pill(58, 40, palette.Surface), 112, 22);
        Place(Pill(30, 5, palette.Secondary, false), 119, 29); Place(Pill(44, 3, palette.Muted, false), 119, 39); Place(Pill(36, 3, palette.Muted, false), 119, 45);
        // The dock.
        Place(Pill(84, 14, palette.Surface), 48, 85);
        for (var i = 0; i < 5; i++) Place(new Border { Width = 8 * zoom, Height = 8 * zoom, CornerRadius = new(radius * .6), Background = Paint(i == 1 ? palette.Accent : i == 3 ? palette.Secondary : palette.Hover) }, 54 + i * 15, 88);
        return new Border { Width = 180 * zoom, Height = 104 * zoom, CornerRadius = new(11), Background = Paint(palette.Canvas), Child = scene, ClipToBounds = true };
    }
}
