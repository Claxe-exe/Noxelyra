using System.Runtime.InteropServices;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.Platform.Win;
public sealed class WindowsWallpaperService : IWallpaperService
{
    private static T Use<T>(Func<IDesktopWallpaper,T> action)
    {
        var instance=Activator.CreateInstance(Type.GetTypeFromCLSID(new("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD"))!)!;
        try { return action((IDesktopWallpaper)instance); }finally { Marshal.ReleaseComObject(instance); }
    }
    // Windows also lists monitors that are remembered but not attached; GetMonitorRECT fails for those, so they are skipped.
    public IReadOnlyList<WallpaperDisplay> Displays()=>Use(w=>
    {
        w.GetMonitorDevicePathCount(out var count); var result=new List<WallpaperDisplay>();
        for(uint i=0;i<count;i++)
        {
            w.GetMonitorDevicePathAt(i,out var id); Rect r;
            try { w.GetMonitorRECT(id,out r); } catch (COMException) { continue; }
            result.Add(new(id,$"Ekran {result.Count+1} · {r.Right-r.Left} × {r.Bottom-r.Top}",r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top));
        }
        return (IReadOnlyList<WallpaperDisplay>)result;
    });
    public string Read(string monitor)=>Use(w=>{w.GetWallpaper(monitor,out var path);return path;});
    public void Set(string monitor,string path)=>Use(w=>{w.SetWallpaper(monitor,path);return true;});
    public bool HasSlideshow()=>Use(w=>{w.GetStatus(out var state);return (state&2)!=0;});
    [StructLayout(LayoutKind.Sequential)]private struct Rect { public int Left,Top,Right,Bottom; }
    [ComImport,Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)]string monitor,[MarshalAs(UnmanagedType.LPWStr)]string path);
        void GetWallpaper([MarshalAs(UnmanagedType.LPWStr)]string monitor,[MarshalAs(UnmanagedType.LPWStr)]out string path);
        void GetMonitorDevicePathAt(uint index,[MarshalAs(UnmanagedType.LPWStr)]out string monitor);void GetMonitorDevicePathCount(out uint count);
        void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)]string monitor,out Rect rect);
        void SetBackgroundColor(uint color);void GetBackgroundColor(out uint color);void SetPosition(uint position);void GetPosition(out uint position);
        void SetSlideshow(nint items);void GetSlideshow(out nint items);void SetSlideshowOptions(uint options,uint interval);void GetSlideshowOptions(out uint options,out uint interval);
        void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)]string monitor,uint direction);void GetStatus(out uint status);
        void Enable([MarshalAs(UnmanagedType.Bool)]bool enabled);
    }
}

