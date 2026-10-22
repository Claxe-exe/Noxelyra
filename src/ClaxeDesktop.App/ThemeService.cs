using System.Windows;
using System.Windows.Media;
using ClaxeDesktop.Core;

namespace ClaxeDesktop.App;
public sealed class ThemeService
{
    public void Apply(Settings settings)
    {
        var theme = ThemePresets.Resolve(settings);
        var resources = Application.Current.Resources;
        var palette = new Dictionary<string, string> { ["Canvas"] = theme.Canvas, ["Surface"] = theme.Surface, ["Card"] = theme.Card, ["Accent"] = theme.Accent, ["Secondary"] = theme.Secondary, ["Text"] = theme.Text, ["Muted"] = theme.Muted, ["Line"] = theme.Line, ["Hover"] = theme.Hover, ["Selection"] = theme.Selection };
        foreach (var entry in palette) resources[entry.Key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(entry.Value));
        if (!SystemParameters.HighContrast) return;
        resources["Canvas"] = SystemColors.WindowBrush; resources["Surface"] = SystemColors.WindowBrush; resources["Card"] = SystemColors.ControlBrush;
        resources["Text"] = SystemColors.WindowTextBrush; resources["Muted"] = SystemColors.WindowTextBrush; resources["Accent"] = SystemColors.HighlightBrush;
        resources["Secondary"] = SystemColors.HighlightBrush; resources["Line"] = SystemColors.WindowTextBrush; resources["Hover"] = SystemColors.HighlightBrush; resources["Selection"] = SystemColors.HighlightBrush;
    }
}
