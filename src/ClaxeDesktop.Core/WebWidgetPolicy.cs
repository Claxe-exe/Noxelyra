using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaxeDesktop.Core;
public sealed record WidgetSize(double Width = 280, double Height = 180);
public sealed record WebWidgetManifest
{
    public int ManifestVersion { get; init; } = 1;
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Entry { get; init; } = "index.html";
    public WidgetSize DefaultSizeDip { get; init; } = new();
    public WidgetSize MinSizeDip { get; init; } = new(160, 120);
    public string[] Permissions { get; init; } = [];
    public string[] NetworkOrigins { get; init; } = [];
    public int RefreshHintSeconds { get; init; } = 60;
    public static WebWidgetManifest Parse(string json)
    {
        if (json.Length > 16384) throw new InvalidDataException("Widget manifesti çok büyük.");
        WebWidgetManifest manifest;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            void CheckDuplicates(JsonElement element)
            {
                if(element.ValueKind == JsonValueKind.Object) { var names = new HashSet<string>(StringComparer.Ordinal); foreach(var property in element.EnumerateObject()) { if(!names.Add(property.Name)) throw new InvalidDataException("Tekrarlanan manifest alanı."); CheckDuplicates(property.Value); } }
                else if(element.ValueKind == JsonValueKind.Array) foreach(var item in element.EnumerateArray()) CheckDuplicates(item);
            }
            CheckDuplicates(document.RootElement);
            manifest = JsonSerializer.Deserialize<WebWidgetManifest>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 8 }) ?? throw new InvalidDataException("Manifest boş.");
        }
        catch (JsonException) { throw new InvalidDataException("Widget manifesti geçersiz."); }
        if (manifest.ManifestVersion != 1 || manifest.Id is null || manifest.Id.Length is < 3 or > 80 || !manifest.Id.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-') || string.IsNullOrWhiteSpace(manifest.Name) || manifest.Name.Length > 80) throw new InvalidDataException("Widget adı, kimliği veya sürümü geçersiz.");
        if (manifest.Permissions is null || manifest.NetworkOrigins is null || manifest.Permissions.Length != 0 || manifest.NetworkOrigins.Length != 0) throw new InvalidDataException("Bu sürüm yalnız yerel, ağ ve özel izin istemeyen widget'ları destekler.");
        if (!IsRelativePath(manifest.Entry) || !Path.GetExtension(manifest.Entry).Equals(".html", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Widget giriş dosyası yerel .html olmalı.");
        foreach (var size in new[] { manifest.DefaultSizeDip, manifest.MinSizeDip }) if (size is null || !double.IsFinite(size.Width) || !double.IsFinite(size.Height) || size.Width is < 160 or > 1200 || size.Height is < 120 or > 900) throw new InvalidDataException("Widget boyutları geçersiz.");
        if (manifest.DefaultSizeDip.Width < manifest.MinSizeDip.Width || manifest.DefaultSizeDip.Height < manifest.MinSizeDip.Height || manifest.RefreshHintSeconds is < 1 or > 3600) throw new InvalidDataException("Widget minimum boyutu veya yenileme aralığı geçersiz.");
        return manifest;
    }
    public static bool IsRelativePath(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 240 && !Path.IsPathRooted(value) && !value.Contains('\\') && !value.Contains(':') && !value.Contains('%') && value.Split('/').All(s => s.Length > 0 && s is not ("." or "..") && s.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' or ' '));
}
public static class WebWidgetPolicy
{
    public const string Origin = "https://widget.claxe.invalid";
    public static bool IsLocalRequest(string url, out string path)
    {
        path = "";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.GetLeftPart(UriPartial.Authority) != Origin || !string.IsNullOrEmpty(uri.UserInfo)) return false;
        if(!url.StartsWith(Origin + "/",StringComparison.Ordinal)) return false;
        var raw = url[(Origin.Length + 1)..].Split('?', '#')[0];
        path = Uri.UnescapeDataString(raw);
        return WebWidgetManifest.IsRelativePath(path);
    }
    public static string ResolveFile(string root, string relative)
    {
        if (!WebWidgetManifest.IsRelativePath(relative)) throw new InvalidDataException("Widget dosya yolu geçersiz.");
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Widget kökü dışına erişim engellendi.");
        var current = new DirectoryInfo(Path.GetDirectoryName(path)!);
        while (current is not null && current.FullName.Length >= fullRoot.TrimEnd(Path.DirectorySeparatorChar).Length)
        { if ((current.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Widget bağlantı/junction içeremez."); current = current.Parent; }
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Widget dosyası bağlantı olamaz.");
        return path;
    }
    public static (string Id, string Method) ParseMessage(string sender, string json)
    {
        if (!IsLocalRequest(sender, out _) || json.Length > 4096) throw new InvalidDataException("Widget mesaj kaynağı veya boyutu geçersiz.");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 4 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException();
            var properties = document.RootElement.EnumerateObject().ToArray();
            if (properties.Length != 2 || properties.Select(p => p.Name).Distinct().Count() != 2 || properties.Any(p => p.Name is not ("id" or "method"))) throw new InvalidDataException("Widget mesaj alanları geçersiz.");
            var id = document.RootElement.GetProperty("id").GetString(); var method = document.RootElement.GetProperty("method").GetString();
            if (string.IsNullOrWhiteSpace(id) || id.Length > 64 || method is not ("theme.getTokens" or "time.getLocal")) throw new InvalidDataException("Widget komutu desteklenmiyor.");
            return (id, method);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException) { throw new InvalidDataException("Widget mesajı geçersiz."); }
    }
}
