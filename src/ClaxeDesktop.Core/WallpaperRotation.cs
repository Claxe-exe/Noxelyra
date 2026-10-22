namespace ClaxeDesktop.Core;
public sealed record WallpaperRotation(string MonitorId,List<string> AssetPaths,int IntervalSeconds=120);
public static class WallpaperPausePolicy
{
    public static bool Pause(bool safe,bool locked,bool? conservePower,bool fullscreen)=>safe||locked||conservePower!=false||fullscreen;
}
