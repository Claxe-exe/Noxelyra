namespace ClaxeDesktop.Core;

public enum DesktopPresence { Unknown, Desktop, Application, OwnApplication }
public enum DesktopFollowAction { None, Reveal, Lower }
public static class DesktopFollowPolicy
{
    public static DesktopFollowAction Decide(bool enabled, bool follow, bool safe, bool locked, bool suppressed, DesktopPresence presence)
    {
        if (!enabled || !follow || safe || locked) return DesktopFollowAction.None;
        if (presence == DesktopPresence.Application) return DesktopFollowAction.Lower;
        return presence == DesktopPresence.Desktop && !suppressed ? DesktopFollowAction.Reveal : DesktopFollowAction.None;
    }
}
