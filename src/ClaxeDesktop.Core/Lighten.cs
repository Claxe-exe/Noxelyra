namespace ClaxeDesktop.Core;

public enum StartupSource { UserRun, UserFolder, MachineRun, MachineFolder }
/// <summary>A program Windows starts at sign-in. Only user-scope entries are switchable without administrator rights.</summary>
public sealed record StartupEntry(string Name, string Command, StartupSource Source, bool Enabled)
{
    public bool UserScope => Source is StartupSource.UserRun or StartupSource.UserFolder;
    public string ApprovalKey => Lighten.ApprovedRoot + (Source is StartupSource.UserFolder or StartupSource.MachineFolder ? @"\StartupFolder" : @"\Run");
}
public sealed record MemoryUse(string Name, int Processes, long Bytes);

/// <summary>The Windows values Noxelyra's lighten center may change, all per-user and the same ones Task Manager and Settings write.</summary>
public static class Lighten
{
    public const string ApprovedRoot = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";
    public const string Background = @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications";
    public const string PhoneLink = "Microsoft.YourPhone_8wekyb3d8bbwe", CrossDevice = "MicrosoftWindows.CrossDevice_cw5n1h2txyewy";
    public const string ExplorerPolicy = @"Software\Policies\Microsoft\Windows\Explorer";
    public static IReadOnlySet<string> AllowedKeys { get; } = new HashSet<string> { ApprovedRoot + @"\Run", ApprovedRoot + @"\StartupFolder", Background + "\\" + PhoneLink, Background + "\\" + CrossDevice, ExplorerPolicy };
    /// <summary>Task Manager's StartupApproved layout: first byte even = enabled, odd = disabled, then a FILETIME of the change.</summary>
    public static bool IsEnabled(byte[]? approval) => approval is not { Length: > 0 } || (approval[0] & 1) == 0;
    public static byte[] Approval(bool enabled, DateTime utc)
    {
        var bytes = new byte[12]; bytes[0] = enabled ? (byte)2 : (byte)3;
        if (!enabled) BitConverter.GetBytes(utc.ToFileTimeUtc()).CopyTo(bytes, 4);
        return bytes;
    }
    public static LookValue Startup(StartupEntry entry, bool enabled, DateTime utc)
    {
        if (!entry.UserScope) throw new InvalidOperationException("Bu başlangıç girdisi yönetici izni gerektirir; Görev Yöneticisi'nden değiştir.");
        return new(entry.ApprovalKey, entry.Name, true, Binary: Approval(enabled, utc));
    }
    public static IReadOnlyList<LookValue> PhoneInBackground(bool allowed) => [.. new[] { PhoneLink, CrossDevice }.SelectMany(p => new LookValue[] { new(Background + "\\" + p, "Disabled", true, allowed ? 0u : 1u), new(Background + "\\" + p, "DisabledByUser", true, allowed ? 0u : 1u) })];
    public static IReadOnlyList<LookValue> WebSearch(bool enabled) => [new(ExplorerPolicy, "DisableSearchBoxSuggestions", true, enabled ? 0u : 1u)];
    public static IReadOnlyList<MemoryUse> Group(IEnumerable<(string Name, long Bytes)> processes, int top) =>
        processes.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Select(g => new MemoryUse(g.Key, g.Count(), g.Sum(p => p.Bytes))).OrderByDescending(m => m.Bytes).Take(top).ToList();
}
