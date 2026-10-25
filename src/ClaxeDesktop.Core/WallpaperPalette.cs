namespace ClaxeDesktop.Core;

/// <summary>
/// pywal-style palette: the wallpaper's most vivid frequent hue becomes the accent, a distinct second hue the secondary colour,
/// and the surfaces are deep tints of the accent. The result always passes <see cref="ThemePresets.ValidateCustom"/>.
/// </summary>
public static class WallpaperPalette
{
    private const int Bins = 36;
    /// <summary>Hue used when the image has no clear colour (black-and-white or grey wallpapers): Midnight Noxelyra's purple.</summary>
    public const double FallbackHue = 262;
    public static ThemePalette Generate(IReadOnlyList<(byte R, byte G, byte B)> pixels)
    {
        if (pixels is null || pixels.Count == 0) throw new InvalidDataException("Görselde okunacak renk yok.");
        var weight = new double[Bins]; var hueX = new double[Bins]; var hueY = new double[Bins]; var saturation = new double[Bins];
        foreach (var (r, g, b) in pixels)
        {
            var (h, s, l) = ToHsl(r, g, b);
            if (s < .18 || l < .10 || l > .94) continue;
            // Vivid, mid-light colours count most; dark shadows and washed-out highlights count little.
            var w = s * Math.Max(0.05, 1 - Math.Abs(l - .55) * 1.6);
            var bin = (int)(h / (360.0 / Bins)) % Bins;
            weight[bin] += w; hueX[bin] += Math.Cos(h * Math.PI / 180) * w; hueY[bin] += Math.Sin(h * Math.PI / 180) * w; saturation[bin] += s * w;
        }
        var smooth = Enumerable.Range(0, Bins).Select(i => weight[i] + .5 * (weight[(i + Bins - 1) % Bins] + weight[(i + 1) % Bins])).ToArray();
        var colourful = weight.Sum() > pixels.Count * .02;
        double accentHue = FallbackHue, accentSaturation = .6, secondHue;
        if (colourful)
        {
            var best = Array.IndexOf(smooth, smooth.Max());
            accentHue = MeanHue(best); accentSaturation = saturation[best] / Math.Max(weight[best], 1e-9);
            var second = Enumerable.Range(0, Bins).Where(i => Distance(i, best) >= 4 && smooth[i] >= smooth[best] * .25).OrderByDescending(i => smooth[i]).Cast<int?>().FirstOrDefault();
            secondHue = second is { } index ? MeanHue(index) : (accentHue + 140) % 360;
        }
        else secondHue = (accentHue + 140) % 360;
        double MeanHue(int bin) { var h = Math.Atan2(hueY[bin], hueX[bin]) * 180 / Math.PI; return h < 0 ? h + 360 : h; }

        var canvas = FromHsl(accentHue, .30, .055); var surface = FromHsl(accentHue, .26, .10);
        var accent = Lift(accentHue, Math.Clamp(accentSaturation, .5, .85), .68, canvas, 4.5);
        var secondary = Lift(secondHue, .55, .70, canvas, 4.5);
        var text = Lift(accentHue, .30, .94, surface, 7); var muted = Lift(accentHue, .14, .72, surface, 4.5);
        var palette = new ThemePalette(ThemePresets.WallpaperId, "Duvar kâğıdından", canvas, surface, FromHsl(accentHue, .22, .15), accent, secondary, text, muted,
            FromHsl(accentHue, .20, .32), FromHsl(accentHue, .22, .22), FromHsl(accentHue, .38, .30));
        ThemePresets.ValidateCustom(palette);
        return palette;
    }
    private static int Distance(int a, int b) { var d = Math.Abs(a - b) % Bins; return Math.Min(d, Bins - d); }
    // Raises lightness until the colour reaches the wanted contrast against the background.
    private static string Lift(double hue, double saturation, double lightness, string background, double contrast)
    {
        var color = FromHsl(hue, saturation, lightness);
        while (ThemePresets.Contrast(color, background) < contrast && lightness < .99) { lightness = Math.Min(.99, lightness + .02); color = FromHsl(hue, saturation, lightness); }
        return color;
    }
    public static (double H, double S, double L) ToHsl(byte red, byte green, byte blue)
    {
        double r = red / 255.0, g = green / 255.0, b = blue / 255.0;
        var max = Math.Max(r, Math.Max(g, b)); var min = Math.Min(r, Math.Min(g, b)); var l = (max + min) / 2;
        if (max - min < 1e-9) return (0, 0, l);
        var d = max - min; var s = l > .5 ? d / (2 - max - min) : d / (max + min);
        var h = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
        return (h * 60, s, l);
    }
    public static string FromHsl(double hue, double saturation, double lightness)
    {
        hue = ((hue % 360) + 360) % 360; saturation = Math.Clamp(saturation, 0, 1); lightness = Math.Clamp(lightness, 0, 1);
        var c = (1 - Math.Abs(2 * lightness - 1)) * saturation; var x = c * (1 - Math.Abs(hue / 60 % 2 - 1)); var m = lightness - c / 2;
        var (r, g, b) = hue switch { < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x), < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x) };
        static int Byte(double v) => (int)Math.Round(Math.Clamp(v, 0, 1) * 255);
        return $"#{Byte(r + m):X2}{Byte(g + m):X2}{Byte(b + m):X2}";
    }
}
