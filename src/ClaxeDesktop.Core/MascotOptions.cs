namespace ClaxeDesktop.Core;
public sealed record MascotOptions
{
    public bool Enabled {get;init;}
    public string? ImagePath {get;init;}
    public string? HappyImagePath {get;init;}
    public int Size {get;init;}=140;
    public Placement? Placement {get;init;}
    public void Validate()
    {
        if(Size is <80 or >240||(Enabled&&ImagePath is null))throw new InvalidDataException("Maskot boyutu veya görseli geçersiz.");
        foreach(var path in new[]{ImagePath,HappyImagePath})if(path is not null&&(!Path.IsPathFullyQualified(path)||path.StartsWith(@"\\")||!path.EndsWith(".png",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("Maskot yerel PNG olmalı.");
        if(Placement is{} p&&(string.IsNullOrWhiteSpace(p.MonitorId)||!double.IsFinite(p.X)||!double.IsFinite(p.Y)||Math.Abs(p.X)>100000||Math.Abs(p.Y)>100000))throw new InvalidDataException("Maskot konumu geçersiz.");
    }
}
