using System.Globalization;
using System.Text.Json;
namespace ClaxeDesktop.Core;

/// <summary>A geocoded place from Open-Meteo.</summary>
public sealed record WeatherPlace(string Name, string? Region, string? Country, double Latitude, double Longitude)
{
    public string Label => string.Join(", ", new[] { Name, Region, Country }.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct());
}
public sealed record WeatherDay(DateOnly Date, int Code, double Min, double Max);
public sealed record WeatherReport(double Temperature, double FeelsLike, int Humidity, double Wind, int Code, bool Day, IReadOnlyList<WeatherDay> Days);

/// <summary>
/// The weather widget's only network contract: two fixed Open-Meteo HTTPS hosts, no key, no account, no location lookup.
/// Only the city name the user typed is sent. Parsing is strict and size-bounded so a bad response cannot break the widget.
/// </summary>
public static class OpenMeteo
{
    public static readonly IReadOnlySet<string> Hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "geocoding-api.open-meteo.com", "api.open-meteo.com" };
    public const int MaxCityLength = 80;
    public static string CleanCity(string? city)
    {
        var text = new string((city ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (text.Length is 0 or > MaxCityLength) throw new InvalidDataException("Şehir adı 1–80 karakter olmalı.");
        return text;
    }
    public static Uri GeocodeUri(string city) => new($"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(CleanCity(city))}&count=1&language=tr&format=json");
    public static Uri ForecastUri(WeatherPlace place) => new(string.Create(CultureInfo.InvariantCulture,
        $"https://api.open-meteo.com/v1/forecast?latitude={place.Latitude:0.####}&longitude={place.Longitude:0.####}&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m,is_day&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=4"));
    public static bool Allowed(Uri uri) => uri.Scheme == Uri.UriSchemeHttps && Hosts.Contains(uri.Host) && uri.IsDefaultPort;
    /// <summary>The first match, or null when the city is unknown.</summary>
    public static WeatherPlace? ParsePlace(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0) return null;
        var first = results[0];
        var latitude = first.GetProperty("latitude").GetDouble(); var longitude = first.GetProperty("longitude").GetDouble();
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180) throw new InvalidDataException("Konum geçersiz.");
        static string? Text(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? Clip(v.GetString()) : null;
        return new(Text(first, "name") ?? "?", Text(first, "admin1"), Text(first, "country"), latitude, longitude);
    }
    public static WeatherReport ParseForecast(string json)
    {
        using var document = JsonDocument.Parse(json);
        var current = document.RootElement.GetProperty("current"); var daily = document.RootElement.GetProperty("daily");
        var dates = daily.GetProperty("time"); var codes = daily.GetProperty("weather_code"); var max = daily.GetProperty("temperature_2m_max"); var min = daily.GetProperty("temperature_2m_min");
        var days = new List<WeatherDay>();
        for (int i = 0; i < Math.Min(7, dates.GetArrayLength()); i++)
        {
            if (codes[i].ValueKind != JsonValueKind.Number || max[i].ValueKind != JsonValueKind.Number || min[i].ValueKind != JsonValueKind.Number) continue;
            days.Add(new(DateOnly.ParseExact(dates[i].GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture), codes[i].GetInt32(), min[i].GetDouble(), max[i].GetDouble()));
        }
        return new(current.GetProperty("temperature_2m").GetDouble(), current.GetProperty("apparent_temperature").GetDouble(), current.GetProperty("relative_humidity_2m").GetInt32(),
            current.GetProperty("wind_speed_10m").GetDouble(), current.GetProperty("weather_code").GetInt32(), current.GetProperty("is_day").GetInt32() == 1, days);
    }
    /// <summary>WMO weather code to a Turkish description and a Segoe UI Symbol glyph.</summary>
    public static (string Text, string Glyph) Describe(int code, bool day = true) => code switch
    {
        0 => ("Açık", day ? "☀" : "☾"),
        1 => ("Az bulutlu", day ? "⛅" : "☾"),
        2 => ("Parçalı bulutlu", "⛅"),
        3 => ("Kapalı", "☁"),
        45 or 48 => ("Sisli", "☁"),
        51 or 53 or 55 => ("Çisenti", "☂"),
        56 or 57 => ("Donan çisenti", "☂"),
        61 => ("Hafif yağmur", "⛆"), 63 => ("Yağmur", "⛆"), 65 => ("Şiddetli yağmur", "⛆"),
        66 or 67 => ("Donan yağmur", "⛆"),
        71 => ("Hafif kar", "❄"), 73 => ("Kar", "❄"), 75 => ("Yoğun kar", "❄"), 77 => ("Kar taneleri", "❄"),
        80 or 81 or 82 => ("Sağanak", "☔"),
        85 or 86 => ("Kar sağanağı", "❄"),
        95 => ("Gök gürültülü fırtına", "⛈"),
        96 or 99 => ("Dolulu fırtına", "⛈"),
        _ => ("Bilinmiyor", "☁")
    };
    private static string? Clip(string? text) => text is null ? null : text.Length > 60 ? text[..60] : text;
}

/// <summary>The last N samples of one metric for a sparkline; missing samples are kept as gaps.</summary>
public sealed class MetricHistory(int capacity = 60)
{
    private readonly Queue<double?> samples = new();
    public int Capacity { get; } = capacity is >= 2 and <= 3600 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
    public IReadOnlyList<double?> Values => samples.ToArray();
    public double? Latest { get; private set; }
    public void Add(double? value)
    {
        if (value is { } v && !double.IsFinite(v)) value = null;
        samples.Enqueue(value); Latest = value;
        while (samples.Count > Capacity) samples.Dequeue();
    }
    /// <summary>The largest sample, at least <paramref name="floor"/> so a quiet graph does not exaggerate noise.</summary>
    public double Peak(double floor) => Math.Max(floor, samples.Where(s => s.HasValue).Select(s => s!.Value).DefaultIfEmpty(0).Max());
    /// <summary>Points scaled into a width × height box (y grows downwards); gaps break the line into segments.</summary>
    public IReadOnlyList<IReadOnlyList<(double X, double Y)>> Segments(double width, double height, double peak)
    {
        var values = Values; var result = new List<IReadOnlyList<(double, double)>>(); var current = new List<(double, double)>();
        var step = width / (Capacity - 1); var offset = Capacity - values.Count;
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] is not { } v) { if (current.Count > 0) { result.Add(current); current = []; } continue; }
            current.Add(((offset + i) * step, height - Math.Clamp(v / peak, 0, 1) * height));
        }
        if (current.Count > 0) result.Add(current);
        return result;
    }
}
