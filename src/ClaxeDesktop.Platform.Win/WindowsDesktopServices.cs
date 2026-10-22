using System.Runtime.InteropServices;
using ClaxeDesktop.Core;

namespace ClaxeDesktop.Platform.Win;
public sealed record AudioState(float Level,bool Muted);
public sealed class WindowsAudioService
{
    private static T WithEndpoint<T>(Func<IAudioEndpointVolume,T> action)
    {
        object? enumerator=null; IMMDevice? device=null; object? endpoint=null;
        try
        {
            enumerator=Activator.CreateInstance(Type.GetTypeFromCLSID(new("BCDE0395-E52F-467C-8E3D-C4579291692E"))!)!;
            Marshal.ThrowExceptionForHR(((IMMDeviceEnumerator)enumerator).GetDefaultAudioEndpoint(0,1,out device));
            var id=typeof(IAudioEndpointVolume).GUID; Marshal.ThrowExceptionForHR(device.Activate(ref id,23,0,out endpoint));
            return action((IAudioEndpointVolume)endpoint);
        }
        finally { if(endpoint is not null)Marshal.ReleaseComObject(endpoint); if(device is not null)Marshal.ReleaseComObject(device);if(enumerator is not null)Marshal.ReleaseComObject(enumerator); }
    }
    public AudioState Read() => WithEndpoint(endpoint=>{ Marshal.ThrowExceptionForHR(endpoint.GetMasterVolumeLevelScalar(out var volume));Marshal.ThrowExceptionForHR(endpoint.GetMute(out var mute));return new AudioState(volume,mute); });
    public void SetLevel(float level) { if(!float.IsFinite(level)||level is <0 or >1)throw new ArgumentOutOfRangeException(nameof(level)); WithEndpoint(endpoint=>{var context=Guid.Empty;Marshal.ThrowExceptionForHR(endpoint.SetMasterVolumeLevelScalar(level,ref context));return true;}); }
    public void SetMute(bool muted) => WithEndpoint(endpoint=>{var context=Guid.Empty;Marshal.ThrowExceptionForHR(endpoint.SetMute(muted,ref context));return true;});
    [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IMMDeviceEnumerator
    {
        [PreserveSig]int EnumAudioEndpoints(int flow,uint state,out nint devices);
        [PreserveSig]int GetDefaultAudioEndpoint(int flow,int role,out IMMDevice device);
    }
    [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IMMDevice
    { [PreserveSig]int Activate(ref Guid iid,uint context,nint parameters,[MarshalAs(UnmanagedType.IUnknown)]out object instance); }
    [ComImport,Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IAudioEndpointVolume
    {
        [PreserveSig]int RegisterControlChangeNotify(nint callback);[PreserveSig]int UnregisterControlChangeNotify(nint callback);[PreserveSig]int GetChannelCount(out uint count);
        [PreserveSig]int SetMasterVolumeLevel(float value,ref Guid context);[PreserveSig]int SetMasterVolumeLevelScalar(float value,ref Guid context);
        [PreserveSig]int GetMasterVolumeLevel(out float value);[PreserveSig]int GetMasterVolumeLevelScalar(out float value);
        [PreserveSig]int SetChannelVolumeLevel(uint channel,float value,ref Guid context);[PreserveSig]int SetChannelVolumeLevelScalar(uint channel,float value,ref Guid context);
        [PreserveSig]int GetChannelVolumeLevel(uint channel,out float value);[PreserveSig]int GetChannelVolumeLevelScalar(uint channel,out float value);
        [PreserveSig]int SetMute([MarshalAs(UnmanagedType.Bool)]bool muted,ref Guid context);[PreserveSig]int GetMute([MarshalAs(UnmanagedType.Bool)]out bool muted);
    }
}

public sealed class WindowsApplicationIndex
{
    public IReadOnlyList<Shortcut> Read(CancellationToken token)
    {
        var found=new List<Shortcut>();
        void Scan(string root,int depth)
        {
            if(depth>10||!Directory.Exists(root)||found.Count>=2000)return;
            try
            {
                foreach(var path in Directory.EnumerateFileSystemEntries(root))
                {
                    token.ThrowIfCancellationRequested();
                    if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)continue;
                    if(Directory.Exists(path))Scan(path,depth+1);
                    else if(Path.GetExtension(path).Equals(".lnk",StringComparison.OrdinalIgnoreCase)&&LaunchPolicy.IsAllowed(path))found.Add(new(Path.GetFileNameWithoutExtension(path),path));
                    if(found.Count>=2000)return;
                }
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { }
        }
        foreach(var folder in new[]{Environment.SpecialFolder.Programs,Environment.SpecialFolder.CommonPrograms}) Scan(Environment.GetFolderPath(folder),0);
        return found.OrderBy(a=>a.Name,StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
}
