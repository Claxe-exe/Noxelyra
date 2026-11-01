using System.Diagnostics;
using System.Runtime.InteropServices;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.Platform.Win;

public sealed record AudioOutput(string Id, string Name, bool Default);
/// <summary>One line of the mixer: every audio session of an app, or Windows' system sounds.</summary>
public sealed record AudioApp(string Key, string Label, float Level, bool Muted, string? Path);

/// <summary>Output devices and the app mixer. Listing and per-app volume are documented Core Audio.</summary>
public static partial class AppAudio
{
    private const string SystemKey = "system";
    public static IReadOnlyList<AudioOutput> Outputs()
    {
        object? enumerator = null; nint collectionPointer = 0; IMMDeviceCollection? collection = null; IMMDevice? fallback = null;
        var result = new List<AudioOutput>(); string? current = null;
        try
        {
            enumerator = NewEnumerator(); var devices = (IMMDeviceEnumerator)enumerator;
            if (devices.GetDefaultAudioEndpoint(0, 1, out fallback) == 0) fallback.GetId(out current);
            Marshal.ThrowExceptionForHR(devices.EnumAudioEndpoints(0, 1, out collectionPointer));
            collection = (IMMDeviceCollection)Marshal.GetObjectForIUnknown(collectionPointer);
            collection.GetCount(out var count);
            for (int i = 0; i < count; i++)
            {
                if (collection.Item(i, out var device) != 0) continue;
                try { device.GetId(out var id); result.Add(new(id, FriendlyName(device), id == current)); }
                finally { Marshal.ReleaseComObject(device); }
            }
        }
        finally
        {
            if (collection is not null) Marshal.ReleaseComObject(collection); if (collectionPointer != 0) Marshal.Release(collectionPointer);
            if (fallback is not null) Marshal.ReleaseComObject(fallback); if (enumerator is not null) Marshal.ReleaseComObject(enumerator);
        }
        return result;
    }
    private static string FriendlyName(IMMDevice device)
    {
        if (device.OpenPropertyStore(0, out var store) != 0) return "Ses aygıtı";
        var value = new PropertyValue();
        try
        {
            var key = new PropertyKey { Format = new("a45c254e-df1c-4efd-8020-67d146a850e0"), Id = 14 };
            return store.GetValue(ref key, out value) == 0 && value.Type == 31 && Marshal.PtrToStringUni(value.Pointer) is { Length: > 0 } name ? name : "Ses aygıtı";
        }
        finally { PropVariantClear(ref value); Marshal.ReleaseComObject(store); }
    }
    /// <summary>
    /// Makes a device the Windows default. Windows has no public API for this; IPolicyConfig is the undocumented interface its own
    /// settings use, so any failure returns false and the caller sends the user to Windows' sound settings instead.
    /// </summary>
    public static bool SetDefaultOutput(string id)
    {
        object? client = null;
        try
        {
            client = Activator.CreateInstance(Type.GetTypeFromCLSID(new("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9"))!);
            if (client is not IPolicyConfig policy) return false;
            foreach (var role in new[] { 0, 1, 2 }) if (policy.SetDefaultEndpoint(id, role) != 0) return false;
            return true;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException or NotSupportedException or TypeLoadException or NullReferenceException) { return false; }
        finally { if (client is not null) Marshal.ReleaseComObject(client); }
    }
    public static IReadOnlyList<AudioApp> Apps()
    {
        var apps = new Dictionary<string, AudioApp>(StringComparer.OrdinalIgnoreCase);
        Sessions((volume, _, name, pid, system) =>
        {
            var key = system ? SystemKey : name; if (key.Length == 0 || apps.ContainsKey(key)) return;
            volume.GetMasterVolume(out var level); volume.GetMute(out var muted);
            string? path = null;
            if (!system && pid > 0) try { using var process = Process.GetProcessById(pid); path = process.MainModule?.FileName; } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
            apps[key] = new(key, system ? "Sistem sesleri" : IslandRules.AppLabel(name), level, muted, path);
        });
        return apps.Values.OrderBy(a => a.Key == SystemKey ? 1 : 0).ThenBy(a => a.Label, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    public static void SetApp(string key, float? level, bool? muted)
    {
        var context = Guid.Empty;
        Sessions((volume, _, name, _, system) =>
        {
            if (!string.Equals(system ? SystemKey : name, key, StringComparison.OrdinalIgnoreCase)) return;
            if (level is { } value && float.IsFinite(value)) volume.SetMasterVolume(Math.Clamp(value, 0, 1), ref context);
            if (muted is { } mute) volume.SetMute(mute, ref context);
        });
    }
    // Every live session on the default output device.
    private static void Sessions(Action<ISimpleAudioVolume, IAudioSessionControl2, string, int, bool> action)
    {
        object? enumerator = null; IMMDevice? device = null; object? manager = null; IAudioSessionEnumerator? sessions = null;
        try
        {
            enumerator = NewEnumerator();
            Marshal.ThrowExceptionForHR(((IMMDeviceEnumerator)enumerator).GetDefaultAudioEndpoint(0, 1, out device));
            var iid = typeof(IAudioSessionManager2).GUID; Marshal.ThrowExceptionForHR(device.Activate(ref iid, 23, 0, out manager));
            Marshal.ThrowExceptionForHR(((IAudioSessionManager2)manager).GetSessionEnumerator(out sessions));
            sessions.GetCount(out var count);
            for (int i = 0; i < count; i++)
            {
                if (sessions.GetSession(i, out var control) != 0 || control is null) continue;
                try
                {
                    control.GetState(out var state); if (state == 2) continue;
                    control.GetProcessId(out var pid); var system = control.IsSystemSoundsSession() == 0;
                    var name = ""; if (pid > 0 && !system) try { using var process = Process.GetProcessById((int)pid); name = process.ProcessName; } catch (ArgumentException) { continue; }
                    action((ISimpleAudioVolume)control, control, name, (int)pid, system);
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
    private static object NewEnumerator() => Activator.CreateInstance(Type.GetTypeFromCLSID(new("BCDE0395-E52F-467C-8E3D-C4579291692E"))!)!;

    [StructLayout(LayoutKind.Sequential)] private struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Explicit, Size = 24)] private struct PropertyValue { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public nint Pointer; }
    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropertyValue value);
    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int Item(int index, out IMMDevice device);
    }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropertyValue value);
    }
    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string id, out nint format);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, int useDefault, out nint format);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, nint endpointFormat, nint mixFormat);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, int useDefault, out long defaultPeriod, out long minimumPeriod);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, ref long period);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, nint mode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, nint mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, int store, ref PropertyKey key, out PropertyValue value);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, int store, ref PropertyKey key, ref PropertyValue value);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
    }
}
