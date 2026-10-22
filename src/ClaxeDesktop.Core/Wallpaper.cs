using System.Text.Json;
namespace ClaxeDesktop.Core;
public sealed record WallpaperDisplay(string Id,string Name,int X=0,int Y=0,int Width=0,int Height=0) { public override string ToString()=>Name; }
public sealed record WallpaperAssignment(string MonitorId,string AssetPath);
public interface IWallpaperService
{
    IReadOnlyList<WallpaperDisplay> Displays();string Read(string monitor);void Set(string monitor,string path);bool HasSlideshow();
}
public sealed record WallpaperLease(string MonitorId,string Previous,string Applied);
public sealed class WallpaperSession(IWallpaperService platform,string journal)
{
    private List<WallpaperLease> leases=[];
    private void Write()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(journal)!);var temp=journal+".tmp";
        using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)) { JsonSerializer.Serialize(file,leases);file.Flush(true); }
        File.Move(temp,journal,true);
    }
    public void Apply(string monitor,string asset)
    {
        if(platform.HasSlideshow())throw new InvalidOperationException("Windows slayt gösterisi etkin. Çakışmaması için önce Windows arka planını Resim olarak değiştir.");
        if(!platform.Displays().Any(m=>m.Id==monitor))throw new InvalidDataException("Seçili ekran bağlı değil.");
        if(!File.Exists(asset))throw new FileNotFoundException();
        var current=platform.Read(monitor);var previous=leases.FirstOrDefault(w=>w.MonitorId==monitor);
        // An external wallpaper change becomes the new baseline rather than being overwritten on exit.
        var original=previous is not null&&string.Equals(current,previous.Applied,StringComparison.OrdinalIgnoreCase)?previous.Previous:current;
        var next=new WallpaperLease(monitor,original,asset);leases.RemoveAll(w=>w.MonitorId==monitor);leases.Add(next);Write();
        platform.Set(monitor,asset);
    }
    public void Restore()
    {
        if(File.Exists(journal))
        {
            if(new FileInfo(journal).Length>65536)throw new InvalidDataException("Arka plan kurtarma kaydı sınırı aşıyor.");
            leases=JsonSerializer.Deserialize<List<WallpaperLease>>(File.ReadAllText(journal))??throw new InvalidDataException("Arka plan kurtarma kaydı okunamadı.");
            if(leases.Count>32||leases.Any(w=>w is null||string.IsNullOrEmpty(w.MonitorId)||w.Previous is null||string.IsNullOrEmpty(w.Applied)||w.Previous.Length>4096||w.Applied.Length>4096||w.Previous.StartsWith(@"\\")||w.Applied.StartsWith(@"\\")||(w.Previous.Length>0&&!Path.IsPathFullyQualified(w.Previous))||!Path.IsPathFullyQualified(w.Applied)))throw new InvalidDataException("Arka plan kurtarma kaydı geçersiz.");
        }
        if(leases.Count==0)return;
        var connected=platform.Displays().Select(m=>m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var remaining=new List<WallpaperLease>();
        foreach(var lease in leases)
        {
            if(!connected.Contains(lease.MonitorId)) { remaining.Add(lease);continue; }
            try { if(string.Equals(platform.Read(lease.MonitorId),lease.Applied,StringComparison.OrdinalIgnoreCase))platform.Set(lease.MonitorId,lease.Previous); }
            catch { remaining.Add(lease); }
        }
        leases=remaining;Write();if(leases.Count==0)File.Delete(journal);
        if(leases.Count>0)throw new IOException("Bazı ekranların arka planı geri yüklenemedi; kurtarma kaydı korundu.");
    }
}
