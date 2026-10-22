using System.Text.Json;
using System.Text.Json.Serialization;
namespace ClaxeDesktop.Core;
public sealed record DesktopProfile
{
    public string Kind {get;init;}="ClaxeDesktop.Profile";
    public int Version {get;init;}=1;
    public string Id {get;init;}=Guid.NewGuid().ToString("N");
    public string Name {get;init;}="Yeni profil";
    public ThemeConfiguration Appearance {get;init;}=new();
    public int IconSize {get;init;}=48;
    public int PanelWidth {get;init;}=960;
    public int PanelHeight {get;init;}=48;
    public int GridSize {get;init;}=12;
    public string DockEdge {get;init;}="floating";
    public Placement? Panel {get;init;}
    public Placement? Dock {get;init;}
    public List<WidgetInstance> NativeWidgets {get;init;}=[];
    public override string ToString()=>Name;
    public void Validate()
    {
        if(Kind!="ClaxeDesktop.Profile"||Version!=1||!Guid.TryParseExact(Id,"N",out _)||string.IsNullOrWhiteSpace(Name)||Name.Length>64||Name.Any(char.IsControl)||Appearance is null)throw new InvalidDataException("Profil kimliği, adı veya sürümü geçersiz.");
        WidgetLayout.Validate(NativeWidgets);
        if(NativeWidgets.Any(w=>w.Kind=="web"||w.WebPackageId is not null))throw new InvalidDataException("Portatif profil yalnız yerel widget düzeni içerir.");
        foreach(var p in NativeWidgets.Select(w=>w.Placement).Concat(new[]{Panel,Dock}))if(p is not null&&(!p.MonitorId.StartsWith("screen-",StringComparison.Ordinal)||!int.TryParse(p.MonitorId[7..],out var index)||index is <0 or >31))throw new InvalidDataException("Profil ekran yuvası geçersiz.");
        _=Apply(new(),[new("screen","Screen",0,0,1920,1080,1,true)],false);
    }
    public static DesktopProfile Capture(string name,Settings settings,IReadOnlyList<MonitorInfo> monitors)
    {
        var ids=monitors.OrderByDescending(m=>m.Primary).ThenBy(m=>m.X).Select(m=>m.Id).ToList();
        Placement? Slot(Placement? p){if(p is null)return null;var index=ids.IndexOf(p.MonitorId);if(index<0){index=ids.Count;ids.Add(p.MonitorId);}return p with{MonitorId="screen-"+index};}
        return new(){Name=name,Appearance=ThemeConfiguration.From(settings),IconSize=settings.IconSize,PanelWidth=settings.PanelWidth,PanelHeight=settings.PanelHeight,GridSize=settings.GridSize,DockEdge=settings.DockEdge,Panel=Slot(settings.Panel),Dock=Slot(settings.Dock),NativeWidgets=settings.Widgets.Where(w=>w.Kind!="web").Select(w=>w with{Placement=Slot(w.Placement),WebPackageId=null}).ToList()};
    }
    public Settings Apply(Settings current,IReadOnlyList<MonitorInfo> monitors,bool validate=true)
    {
        if(validate)Validate();if(monitors.Count==0)throw new InvalidDataException("Ekran bulunamadı.");
        var ordered=monitors.OrderByDescending(m=>m.Primary).ThenBy(m=>m.X).ToArray();
        Placement? Map(Placement? p){if(p is null)return null;var index=int.Parse(p.MonitorId[7..]);return p with{MonitorId=ordered[index<ordered.Length?index:0].Id};}
        var next=Appearance.ApplyTo(current) with{IconSize=IconSize,PanelWidth=PanelWidth,PanelHeight=PanelHeight,GridSize=GridSize,DockEdge=DockEdge,Panel=Map(Panel),Dock=Map(Dock),Widgets=current.Widgets.Where(w=>w.Kind=="web").Concat(NativeWidgets.Select(w=>w with{Placement=Map(w.Placement)})).ToList()};
        next.Validate();return next;
    }
}
public sealed record ProfileRule
{
    public string Id {get;init;}=Guid.NewGuid().ToString("N");
    public string Name {get;init;}="Yeni kural";
    public string ProfileId {get;init;}="";
    public bool Enabled {get;init;}
    public int Priority {get;init;}=50;
    public string? Application {get;init;}
    public int? StartMinute {get;init;}
    public int? EndMinute {get;init;}
    public int? MonitorCount {get;init;}
    public override string ToString()=>$"{Name} · {Priority}{(Enabled?" · açık":" · kapalı")}";
    public void Validate()
    {
        if(!Guid.TryParseExact(Id,"N",out _)||!Guid.TryParseExact(ProfileId,"N",out _)||string.IsNullOrWhiteSpace(Name)||Name.Length>64||Name.Any(char.IsControl)||(Application is not null&&Application.Any(char.IsControl))||Priority is <0 or >100||Application is{Length:>120}||Application?.IndexOfAny(['\\','/',':'])>=0||MonitorCount is <1 or >16||StartMinute is <0 or >1439||EndMinute is <0 or >1439||StartMinute.HasValue!=EndMinute.HasValue||(string.IsNullOrWhiteSpace(Application)&&!StartMinute.HasValue&&!MonitorCount.HasValue))throw new InvalidDataException("Kural koşulları geçersiz. En az bir koşul seç.");
    }
    public bool Matches(AutomationContext c)
    {
        if(!Enabled)return false;
        if(!string.IsNullOrWhiteSpace(Application)&&!string.Equals(Application,c.ForegroundApplication,StringComparison.OrdinalIgnoreCase))return false;
        if(MonitorCount.HasValue&&c.MonitorCount!=MonitorCount)return false;
        if(StartMinute is{} start&&EndMinute is{} end){var minute=c.LocalTime.Hour*60+c.LocalTime.Minute;if(start<end ? minute<start||minute>=end : start!=end&&minute<start&&minute>=end)return false;}
        return true;
    }
}
public sealed record ProfileBook
{
    public int Version {get;init;}=1;
    public bool AutomationEnabled {get;init;}
    public List<DesktopProfile> Profiles {get;init;}=[];
    public List<ProfileRule> Rules {get;init;}=[];
    public void Validate(){if(Version!=1||Profiles is null||Rules is null||Profiles.Count>32||Rules.Count>64||Profiles.Select(p=>p?.Id).Distinct().Count()!=Profiles.Count||Rules.Select(r=>r?.Id).Distinct().Count()!=Rules.Count)throw new InvalidDataException("Profil kitabı geçersiz.");foreach(var p in Profiles){if(p is null)throw new InvalidDataException();p.Validate();}foreach(var r in Rules){if(r is null)throw new InvalidDataException();r.Validate();if(!Profiles.Any(p=>p.Id==r.ProfileId))throw new InvalidDataException("Kuralın hedef profili yok.");}}
}
public sealed record AutomationContext(DateTimeOffset Now,DateTime LocalTime,string? ForegroundApplication,int MonitorCount,string? ActiveProfileId);
public sealed record AutomationState(string? Candidate=null,DateTimeOffset CandidateSince=default,DateTimeOffset LastSwitch=default,DateTimeOffset ManualUntil=default);
public sealed record AutomationDecision(string? ProfileId,AutomationState State);
public static class ProfileAutomation
{
    public static AutomationDecision Evaluate(ProfileBook book,AutomationContext c,AutomationState state)
    {
        if(!book.AutomationEnabled||c.Now<state.ManualUntil)return new(null,state with{Candidate=null});
        var rule=book.Rules.Where(r=>r.Matches(c)).OrderByDescending(r=>r.Priority).FirstOrDefault();
        if(rule is null||rule.ProfileId==c.ActiveProfileId)return new(null,state with{Candidate=null});
        if(state.Candidate!=rule.Id)return new(null,state with{Candidate=rule.Id,CandidateSince=c.Now});
        if(c.Now-state.CandidateSince<TimeSpan.FromSeconds(3)||c.Now-state.LastSwitch<TimeSpan.FromSeconds(30))return new(null,state);
        return new(rule.ProfileId,state with{LastSwitch=c.Now,Candidate=null});
    }
}
public static class ProfileFiles
{
    internal static readonly JsonSerializerOptions Json=new(){WriteIndented=true,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,MaxDepth=24};
    public static T Parse<T>(string text) where T:class
    {
        if(text.Length>1048576)throw new InvalidDataException("Profil dosyası 1 MB sınırını aşıyor.");
        try{using var doc=JsonDocument.Parse(text,new JsonDocumentOptions{MaxDepth=24});void Duplicates(JsonElement element){if(element.ValueKind==JsonValueKind.Object){var names=new HashSet<string>();foreach(var p in element.EnumerateObject()){if(!names.Add(p.Name))throw new InvalidDataException("Tekrarlanan profil alanı.");Duplicates(p.Value);}}else if(element.ValueKind==JsonValueKind.Array)foreach(var item in element.EnumerateArray())Duplicates(item);}Duplicates(doc.RootElement);return JsonSerializer.Deserialize<T>(text,Json)??throw new InvalidDataException();}catch(JsonException){throw new InvalidDataException("Profil JSON alanları geçersiz.");}
    }
    public static DesktopProfile Import(string text){var value=Parse<DesktopProfile>(text);using var doc=JsonDocument.Parse(text);foreach(var key in new[]{"Kind","Version","Id","Name","Appearance","NativeWidgets"})if(!doc.RootElement.TryGetProperty(key,out _))throw new InvalidDataException("Profil türü, sürümü veya zorunlu alanları eksik.");value.Validate();return value;}
    public static string Export(DesktopProfile profile){profile.Validate();return JsonSerializer.Serialize(profile,Json);}
}
public sealed class ProfileStore(string directory)
{
    private string Pathname=>Path.Combine(directory,"profiles.json");
    private readonly SemaphoreSlim gate=new(1,1);
    public bool ReadOnly {get;private set;}
    public string? Notice {get;private set;}
    private bool brokenPrimary;
    public async Task<ProfileBook> LoadAsync()
    {
        foreach(var path in new[]{Pathname,Pathname+".bak"})
        {
            if(!File.Exists(path))continue;
            try{if(new FileInfo(path).Length>1048576)throw new InvalidDataException();var text=await File.ReadAllTextAsync(path);using var doc=JsonDocument.Parse(text);if(doc.RootElement.TryGetProperty("Version",out var version)&&version.GetInt32()>1){ReadOnly=true;return new();}var book=ProfileFiles.Parse<ProfileBook>(text);book.Validate();return book;}catch(Exception ex)when(ex is IOException or JsonException or InvalidOperationException or UnauthorizedAccessException or FormatException){if(path==Pathname){brokenPrimary=true;Notice="Profil kitabı bozuk; sağlam yedek veya güvenli boş kitap kullanılıyor.";}}
        }
        return new();
    }
    public async Task SaveAsync(ProfileBook book)
    {
        if(ReadOnly)throw new IOException("Yeni sürüm profil kitabı salt okunur.");book.Validate();await gate.WaitAsync();
        try{var bytes=JsonSerializer.SerializeToUtf8Bytes(book,ProfileFiles.Json);if(bytes.Length>1048576)throw new InvalidDataException("Profil kitabı 1 MB sınırını aşıyor.");Directory.CreateDirectory(directory);var temporary=Pathname+".tmp";await using(var file=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){await file.WriteAsync(bytes);file.Flush(true);}if(File.Exists(Pathname))File.Replace(temporary,Pathname,brokenPrimary?null:Pathname+".bak");else File.Move(temporary,Pathname);brokenPrimary=false;}
        finally{gate.Release();}
    }
}



