using System.Collections.Concurrent;
using System.Net.Http;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;

/// <summary>
/// Open-Meteo client for the weather widget. It talks only to the two allowlisted HTTPS hosts, follows no redirects, keeps no
/// cookies, caps each response at 256 KB and gives up after 10 s. It runs only for a weather widget the user added and named a city for.
/// </summary>
internal static class WeatherService
{
    internal sealed record Result(string City, WeatherPlace Place, WeatherReport Report, DateTimeOffset Time);
    private static readonly HttpClient Client = Create();
    private static readonly ConcurrentDictionary<string, WeatherPlace?> Places = new(StringComparer.OrdinalIgnoreCase);
    // The last result per widget, so a widget hidden by a fullscreen game does not fetch again the moment it comes back.
    private static readonly ConcurrentDictionary<string, Result> Recent = new(StringComparer.Ordinal);
    private static HttpClient Create()
    {
        var handler = new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes(10) };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 256 * 1024 };
        var version = typeof(WeatherService).Assembly.GetName().Version;
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Noxelyra/{version?.Major ?? 0}.{version?.Minor ?? 0}.{version?.Build ?? 0}");
        return client;
    }
    internal static Result? Cached(string widget, string city) => Recent.TryGetValue(widget, out var result) && result.City == OpenMeteo.CleanCity(city) ? result : null;
    internal static async Task<Result> ReadAsync(string widget, string city, CancellationToken token)
    {
        var key = OpenMeteo.CleanCity(city);
        if (!Places.TryGetValue(key, out var place)) { place = OpenMeteo.ParsePlace(await GetAsync(OpenMeteo.GeocodeUri(key), token)); Places[key] = place; }
        if (place is null) throw new InvalidDataException($"\"{key}\" bulunamadı. Şehir adını kontrol et.");
        var result = new Result(key, place, OpenMeteo.ParseForecast(await GetAsync(OpenMeteo.ForecastUri(place), token)), DateTimeOffset.Now);
        Recent[widget] = result; return result;
    }
    private static async Task<string> GetAsync(Uri uri, CancellationToken token)
    {
        if (!OpenMeteo.Allowed(uri)) throw new InvalidOperationException("Hava durumu için izin verilmeyen adres.");
        using var response = await Client.GetAsync(uri, token);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Open-Meteo {(int)response.StatusCode} döndürdü.");
        return await response.Content.ReadAsStringAsync(token);
    }
}
