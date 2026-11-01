namespace ClaxeDesktop.Core;

/// <summary>What Windows accepts as a file or folder name, checked before the shell is asked to rename anything.</summary>
public static class FileNameRules
{
    private const string Invalid = "<>:\"/\\|?*";
    private static readonly string[] Reserved = ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];
    /// <summary>Why <paramref name="name"/> cannot be used, or null when it can.</summary>
    public static string? Problem(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Ad boş olamaz.";
        if (name.Length > 200) return "Ad en fazla 200 karakter olabilir.";
        if (name.Any(c => char.IsControl(c) || Invalid.Contains(c))) return "Ad şu karakterleri içeremez: " + Invalid;
        if (name[^1] is '.' or ' ') return "Ad nokta veya boşlukla bitemez.";
        var stem = name.Split('.')[0].TrimEnd();
        if (Reserved.Contains(stem, StringComparer.OrdinalIgnoreCase)) return $"\"{stem}\" Windows'a ayrılmış bir ad.";
        return null;
    }
    /// <summary>"Yeni klasör", then "Yeni klasör (2)", (3)… until <paramref name="exists"/> says the name is free.</summary>
    public static string Unique(string stem, string extension, Func<string, bool> exists)
    {
        var name = stem + extension;
        for (var i = 2; exists(name); i++) name = $"{stem} ({i}){extension}";
        return name;
    }
}
