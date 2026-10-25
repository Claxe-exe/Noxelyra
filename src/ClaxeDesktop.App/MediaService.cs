using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;
namespace ClaxeDesktop.App;
internal sealed record MediaState(string Title, string Artist, bool CanPlay, bool CanPause, bool CanNext, bool CanPrevious, bool Playing)
{
    /// <summary>Album art as published by the playing app (Spotify, browsers, players), or null.</summary>
    public BitmapImage? Artwork { get; init; }
    public TimeSpan Position { get; init; }
    public TimeSpan Duration { get; init; }
    /// <summary>The app accepts position changes (±10 s on the island).</summary>
    public bool CanSeek { get; init; }
}
/// <summary>Windows' global media session (the same one the volume flyout shows). Art is decoded once per track.</summary>
internal sealed class MediaService
{
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private string? artKey;
    private BitmapImage? art;
    public async Task<MediaState?> ReadAsync()
    {
        manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        var session = manager.GetCurrentSession(); if (session is null) return null;
        var properties = await session.TryGetMediaPropertiesAsync(); var playback = session.GetPlaybackInfo(); var c = playback.Controls;
        var playing = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        var key = properties.Title + "\u0001" + properties.Artist;
        if (key != artKey) { artKey = key; art = await LoadArtAsync(properties.Thumbnail); }
        var timeline = session.GetTimelineProperties();
        var position = timeline.Position + (playing ? DateTimeOffset.Now - timeline.LastUpdatedTime : TimeSpan.Zero);
        var duration = timeline.EndTime - timeline.StartTime;
        if (duration < TimeSpan.Zero || duration > TimeSpan.FromHours(24)) duration = TimeSpan.Zero;
        if (position < TimeSpan.Zero) position = TimeSpan.Zero; if (duration > TimeSpan.Zero && position > duration) position = duration;
        return new(properties.Title, properties.Artist, c.IsPlayEnabled, c.IsPauseEnabled, c.IsNextEnabled, c.IsPreviousEnabled, playing) { Artwork = art, Position = position, Duration = duration, CanSeek = c.IsPlaybackPositionEnabled };
    }
    private static async Task<BitmapImage?> LoadArtAsync(IRandomAccessStreamReference? reference)
    {
        if (reference is null) return null;
        try
        {
            using var stream = await reference.OpenReadAsync();
            if (stream.Size is 0 or > 8 * 1024 * 1024) return null;
            using var reader = new DataReader(stream); await reader.LoadAsync((uint)stream.Size);
            var bytes = new byte[stream.Size]; reader.ReadBytes(bytes);
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 160; image.StreamSource = new MemoryStream(bytes); image.EndInit(); image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
    }
    /// <summary>Moves the playing track by <paramref name="delta"/> when the app allows it; false when it does not.</summary>
    public async Task<bool> SeekAsync(TimeSpan delta)
    {
        manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        var session = manager.GetCurrentSession(); if (session is null) return false;
        var playback = session.GetPlaybackInfo(); if (!playback.Controls.IsPlaybackPositionEnabled) return false;
        var timeline = session.GetTimelineProperties();
        var playing = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        var position = timeline.Position + (playing ? DateTimeOffset.Now - timeline.LastUpdatedTime : TimeSpan.Zero);
        var target = ClaxeDesktop.Core.IslandRules.Seek(position, delta, timeline.EndTime - timeline.StartTime);
        return await session.TryChangePlaybackPositionAsync(target.Ticks);
    }
    public async Task<bool> InvokeAsync(string action)
    {
        manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        var session = manager.GetCurrentSession(); if (session is null) return false;
        return action switch { "play" => await session.TryPlayAsync(), "pause" => await session.TryPauseAsync(), "next" => await session.TrySkipNextAsync(), "previous" => await session.TrySkipPreviousAsync(), _ => false };
    }
}
