using System.Diagnostics;
using System.Runtime.InteropServices;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.Platform.Win;

/// <summary>Volume of the app behind a media session; Label names it ("Spotify", or "tüm Chrome" for a browser).</summary>
public sealed record AppVolume(float Level, bool Muted, string Label, int Sessions);

/// <summary>
/// Per-app volume through the documented Core Audio session API (the mixer Windows shows in Volume Mixer).
/// Sessions are matched to the media app id; a browser has one volume for all of its tabs.
/// </summary>
public static class AppAudio
{
    public static AppVolume? Read(string? appId)
    {
        AppVolume? result = null;
        Each(appId, (volume, label, _) =>
        {
            if (result is null) { volume.GetMasterVolume(out var level); volume.GetMute(out var muted); result = new(level, muted, label, 1); }
            else result = result with { Sessions = result.Sessions + 1 };
        });
        return result;
    }
    /// <summary>Sets every matching session to the level; false when the app has no audio session right now.</summary>
    public static bool Set(string? appId, float level)
    {
        if (!float.IsFinite(level)) return false;
        var changed = false; var context = Guid.Empty; level = Math.Clamp(level, 0, 1);
        Each(appId, (volume, _, _) => { Marshal.ThrowExceptionForHR(volume.SetMasterVolume(level, ref context)); if (level > 0) volume.SetMute(false, ref context); changed = true; });
        return changed;
    }
    private static void Each(string? appId, Action<ISimpleAudioVolume, string, int> action)
    {
        if (IslandRules.AppTarget(appId).Process is null) return;
        object? enumerator = null; IMMDevice? device = null; object? manager = null; IAudioSessionEnumerator? sessions = null;
        try
        {
            enumerator = Activator.CreateInstance(Type.GetTypeFromCLSID(new("BCDE0395-E52F-467C-8E3D-C4579291692E"))!)!;
            Marshal.ThrowExceptionForHR(((IMMDeviceEnumerator)enumerator).GetDefaultAudioEndpoint(0, 1, out device));
            var iid = typeof(IAudioSessionManager2).GUID; Marshal.ThrowExceptionForHR(device.Activate(ref iid, 23, 0, out manager));
            Marshal.ThrowExceptionForHR(((IAudioSessionManager2)manager).GetSessionEnumerator(out sessions));
            sessions.GetCount(out var count);
            for (int i = 0; i < count; i++)
            {
                if (sessions.GetSession(i, out var control) != 0 || control is null) continue;
                try
                {
                    control.GetProcessId(out var pid); control.GetSessionIdentifier(out var identifier);
                    var name = ""; if (pid > 0) try { using var process = Process.GetProcessById((int)pid); name = process.ProcessName; } catch (ArgumentException) { continue; }
                    if (!IslandRules.MatchesApp(appId, name, identifier)) continue;
                    action((ISimpleAudioVolume)control, IslandRules.AppLabel(name), (int)pid);
                }
                finally { Marshal.ReleaseComObject(control); }
            }
        }
        finally
        {
            if (sessions is not null) Marshal.ReleaseComObject(sessions); if (manager is not null) Marshal.ReleaseComObject(manager);
            if (device is not null) Marshal.ReleaseComObject(device); if (enumerator is not null) Marshal.ReleaseComObject(enumerator);
        }
    }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, uint state, out nint devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IMMDevice
    { [PreserveSig] int Activate(ref Guid iid, uint context, nint parameters, [MarshalAs(UnmanagedType.IUnknown)] out object instance); }
    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(nint session, uint flags, out nint control);
        [PreserveSig] int GetSimpleAudioVolume(nint session, uint flags, out nint volume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessions);
    }
    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, out IAudioSessionControl2 session);
    }
    [ComImport, Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IAudioSessionControl2
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid context);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid context);
        [PreserveSig] int GetGroupingParam(out Guid grouping);
        [PreserveSig] int SetGroupingParam(ref Guid grouping, ref Guid context);
        [PreserveSig] int RegisterAudioSessionNotification(nint client);
        [PreserveSig] int UnregisterAudioSessionNotification(nint client);
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string identifier);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string identifier);
        [PreserveSig] int GetProcessId(out uint pid);
    }
    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float level, ref Guid context);
        [PreserveSig] int GetMasterVolume(out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
    }
}
