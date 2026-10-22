using Windows.Media.Control;
namespace ClaxeDesktop.App;
internal sealed record MediaState(string Title,string Artist,bool CanPlay,bool CanPause,bool CanNext,bool CanPrevious,bool Playing);
internal sealed class MediaService
{
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    public async Task<MediaState?> ReadAsync()
    {
        manager??=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        var session=manager.GetCurrentSession();if(session is null)return null;
        var properties=await session.TryGetMediaPropertiesAsync();var playback=session.GetPlaybackInfo();var c=playback.Controls;
        return new(properties.Title,properties.Artist,c.IsPlayEnabled,c.IsPauseEnabled,c.IsNextEnabled,c.IsPreviousEnabled,playback.PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);
    }
    public async Task<bool> InvokeAsync(string action)
    {
        var session=manager?.GetCurrentSession();if(session is null)return false;
        return action switch { "play"=>await session.TryPlayAsync(),"pause"=>await session.TryPauseAsync(),"next"=>await session.TrySkipNextAsync(),"previous"=>await session.TrySkipPreviousAsync(),_=>false };
    }
}
