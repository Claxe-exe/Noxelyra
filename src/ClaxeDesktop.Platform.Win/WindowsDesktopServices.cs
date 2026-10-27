using System.Runtime.InteropServices;
using ClaxeDesktop.Core;

namespace ClaxeDesktop.Platform.Win;
public sealed record AudioState(float Level,bool Muted);
public sealed class WindowsAudioService
{
    // flow 0 = output (multimedia role), 1 = microphone (console role, the "default device" in Windows sound settings).
    private static T WithEndpoint<T>(Func<IAudioEndpointVolume,T> action, int flow = 0)
    {
        object? enumerator=null; IMMDevice? device=null; object? endpoint=null;
        try
        {
            enumerator=Activator.CreateInstance(Type.GetTypeFromCLSID(new("BCDE0395-E52F-467C-8E3D-C4579291692E"))!)!;
            Marshal.ThrowExceptionForHR(((IMMDeviceEnumerator)enumerator).GetDefaultAudioEndpoint(flow,flow==0?1:0,out device));
            var id=typeof(IAudioEndpointVolume).GUID; Marshal.ThrowExceptionForHR(device.Activate(ref id,23,0,out endpoint));
            return action((IAudioEndpointVolume)endpoint);
        }
        finally { if(endpoint is not null)Marshal.ReleaseComObject(endpoint); if(device is not null)Marshal.ReleaseComObject(device);if(enumerator is not null)Marshal.ReleaseComObject(enumerator); }
    }
    public AudioState Read() => WithEndpoint(endpoint=>{ Marshal.ThrowExceptionForHR(endpoint.GetMasterVolumeLevelScalar(out var volume));Marshal.ThrowExceptionForHR(endpoint.GetMute(out var mute));return new AudioState(volume,mute); });
    public void SetLevel(float level) { if(!float.IsFinite(level)||level is <0 or >1)throw new ArgumentOutOfRangeException(nameof(level)); WithEndpoint(endpoint=>{var context=Guid.Empty;Marshal.ThrowExceptionForHR(endpoint.SetMasterVolumeLevelScalar(level,ref context));return true;}); }
    public void SetMute(bool muted) => WithEndpoint(endpoint=>{var context=Guid.Empty;Marshal.ThrowExceptionForHR(endpoint.SetMute(muted,ref context));return true;});
    /// <summary>The default microphone; throws COMException when no microphone is present.</summary>
    public AudioState ReadMicrophone() => WithEndpoint(endpoint=>{ Marshal.ThrowExceptionForHR(endpoint.GetMasterVolumeLevelScalar(out var volume));Marshal.ThrowExceptionForHR(endpoint.GetMute(out var mute));return new AudioState(volume,mute); },1);
    public void SetMicrophoneMute(bool muted) => WithEndpoint(endpoint=>{var context=Guid.Empty;Marshal.ThrowExceptionForHR(endpoint.SetMute(muted,ref context));return true;},1);
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
        // Store apps (ChatGPT, WhatsApp, ...) have no .lnk in the Start menu folders; Windows lists them in its Apps folder.
        foreach(var app in PackagedApps()) if(!found.Any(f=>string.Equals(f.Name,app.Name,StringComparison.CurrentCultureIgnoreCase))) found.Add(app);
        return found.OrderBy(a=>a.Name,StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    private static List<Shortcut> PackagedApps()
    {
        var result=new List<Shortcut>();
        // Shell.Application is an apartment-threaded COM object; read it on its own STA thread.
        var thread=new Thread(()=>
        {
            dynamic? shell=null;
            try
            {
                shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
                dynamic folder=shell!.NameSpace("shell:AppsFolder");
                foreach(dynamic item in folder.Items())
                {
                    string name=item.Name; string path=item.Path; var target=LaunchPolicy.AppsFolderPrefix+path;
                    if(!string.IsNullOrWhiteSpace(name)&&LaunchPolicy.IsPackagedApp(target)) lock(result) result.Add(new(name.Length>80?name[..80]:name,target));
                    if(result.Count>=1000)break;
                }
            }
            catch(Exception ex) when(ex is COMException or InvalidCastException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or ArgumentException) { }
            finally { if(shell is not null) Marshal.FinalReleaseComObject(shell); }
        }){IsBackground=true,Name="Noxelyra Store apps"};
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(TimeSpan.FromSeconds(10));
        lock(result) return result.ToList();
    }
}
