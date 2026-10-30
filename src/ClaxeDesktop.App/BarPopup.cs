using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
namespace ClaxeDesktop.App;

/// <summary>Building blocks for the island card's expanded view: small graphs, meters and rows.</summary>
internal static class BarPopup
{
    private const string IconFont = "Segoe Fluent Icons, Segoe MDL2 Assets";
    internal static TextBlock Glyph(string code, double size = 14) => new() { Text = code, FontFamily = new FontFamily(IconFont), FontSize = size, VerticalAlignment = VerticalAlignment.Center };
    internal static TextBlock Muted(string text, double size = 12) { var block = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap }; block.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); return block; }
    internal static TextBlock Figure(string text, double size, FontWeight weight)
    {
        var block = new TextBlock { Text = text, FontSize = size, FontWeight = weight, VerticalAlignment = VerticalAlignment.Center };
        System.Windows.Documents.Typography.SetNumeralAlignment(block, FontNumeralAlignment.Tabular); return block;
    }
    internal static TextBlock Heading(string text) => new() { Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new(0, 14, 0, 6), Opacity = .9 };
    /// <summary>Header line: icon, title and a muted subtitle the caller keeps updating.</summary>
    internal static FrameworkElement Header(string glyph, string title, TextBlock subtitle)
    {
        var row = new DockPanel { Margin = new(0, 0, 0, 4) };
        var icon = new Border { Width = 34, Height = 34, CornerRadius = new(11), Margin = new(0, 0, 12, 0), Child = Glyph(glyph, 16) };
        ((TextBlock)icon.Child).HorizontalAlignment = HorizontalAlignment.Center; ((TextBlock)icon.Child).SetResourceReference(TextBlock.ForegroundProperty, "Accent");
        icon.SetResourceReference(Border.BackgroundProperty, "Card"); DockPanel.SetDock(icon, Dock.Left); row.Children.Add(icon);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold });
        subtitle.FontSize = 12; subtitle.TextTrimming = TextTrimming.CharacterEllipsis; subtitle.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); text.Children.Add(subtitle);
        row.Children.Add(text); return row;
    }
    /// <summary>Area graph of up to <paramref name="length"/> samples per series, newest on the right, scaled to the largest value.</summary>
    internal static void Draw(Canvas canvas, IReadOnlyList<double[]> series, string[] brushes, int length, double? ceiling = null)
    {
        canvas.Children.Clear();
        double width = canvas.Width, height = canvas.Height;
        var top = ceiling ?? Math.Max(1, series.SelectMany(s => s).DefaultIfEmpty(0).Max() * 1.15);
        for (int s = 0; s < series.Count; s++)
        {
            var values = series[s]; if (values.Length < 2) continue;
            var offset = length - values.Length; var line = new PointCollection();
            for (int i = 0; i < values.Length; i++) line.Add(new Point(width * (offset + i) / (length - 1), height - Math.Min(1, values[i] / top) * (height - 2) - 1));
            var area = new PointCollection(line) { new Point(line[^1].X, height), new Point(line[0].X, height) };
            var fill = new Polygon { Points = area, Opacity = .16 }; fill.SetResourceReference(Shape.FillProperty, brushes[s]);
            var stroke = new Polyline { Points = line, StrokeThickness = 1.6, StrokeLineJoin = PenLineJoin.Round }; stroke.SetResourceReference(Shape.StrokeProperty, brushes[s]);
            canvas.Children.Add(fill); canvas.Children.Add(stroke);
        }
    }
    internal static Canvas Graph(double width, double height) { var canvas = new Canvas { Width = width, Height = height, ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Left }; return canvas; }
    /// <summary>A thin rounded usage bar; returns the fill so the caller can resize it.</summary>
    internal static Grid Meter(out Border fill)
    {
        var track = new Border { Height = 6, CornerRadius = new(3) }; track.SetResourceReference(Border.BackgroundProperty, "Card");
        fill = new Border { Height = 6, CornerRadius = new(3), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 }; fill.SetResourceReference(Border.BackgroundProperty, "Accent");
        var grid = new Grid { Margin = new(0, 6, 0, 0) }; grid.Children.Add(track); grid.Children.Add(fill); return grid;
    }
    internal static Button Action(string glyph, string text, Action click)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal }; var icon = Glyph(glyph, 13); icon.Margin = new(0, 0, 8, 0);
        content.Children.Add(icon); content.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        var button = new Button { Content = content, Padding = new(12, 7, 12, 7), Margin = new(0, 0, 8, 0) };
        System.Windows.Automation.AutomationProperties.SetName(button, text);
        button.Click += (_, _) => click(); return button;
    }
    /// <summary>Name on the left, value on the right; long names trim.</summary>
    internal static DockPanel Row(string name, string value, out TextBlock valueText)
    {
        var row = new DockPanel { Margin = new(0, 2, 0, 2) };
        valueText = Figure(value, 12, FontWeights.SemiBold); DockPanel.SetDock(valueText, Dock.Right); row.Children.Add(valueText);
        row.Children.Add(new TextBlock { Text = name, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new(0, 0, 10, 0) });
        return row;
    }
}
