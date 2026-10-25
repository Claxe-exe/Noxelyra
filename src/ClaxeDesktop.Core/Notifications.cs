using System.Text.Json;
namespace ClaxeDesktop.Core;

/// <summary>A Noxelyra notification. Other apps' Windows notifications cannot be read by an unpackaged app and are not shown here.</summary>
public sealed record NotificationItem(string Id, string Title, string Body, DateTimeOffset Time, string Kind = "info", bool Read = false);

/// <summary>Newest-first history capped at 100 plus the do-not-disturb switch, saved atomically next to the settings.</summary>
public sealed class NotificationLog
{
    public const int Limit = 100;
    private sealed record Stored(bool DoNotDisturb, List<NotificationItem> Items);
    private readonly string file;
    private readonly List<NotificationItem> items;
    private bool doNotDisturb;
    public NotificationLog(string file)
    {
        this.file = file;
        var stored = Load(file); items = stored.Items; doNotDisturb = stored.DoNotDisturb;
    }
    public IReadOnlyList<NotificationItem> Items => items;
    public int Unread => items.Count(i => !i.Read);
    /// <summary>While on, notifications are still recorded but not shown in the island.</summary>
    public bool DoNotDisturb { get => doNotDisturb; set { if (doNotDisturb == value) return; doNotDisturb = value; Save(); } }
    public NotificationItem Add(string title, string body, string kind, DateTimeOffset now)
    {
        title = Clip(title, 80); body = Clip(body, 400);
        if (title.Length == 0) throw new ArgumentException("Bildirim başlığı boş olamaz.");
        var item = new NotificationItem(Guid.NewGuid().ToString("N"), title, body, now, kind is "info" or "update" or "timer" or "warning" ? kind : "info");
        items.Insert(0, item); if (items.Count > Limit) items.RemoveRange(Limit, items.Count - Limit);
        Save(); return item;
    }
    public void MarkAllRead() { if (Unread == 0) return; for (int i = 0; i < items.Count; i++) items[i] = items[i] with { Read = true }; Save(); }
    public void Clear() { items.Clear(); Save(); }
    private static string Clip(string? text, int max) { text = (text ?? "").Trim(); return text.Length > max ? text[..max] : text; }
    private static Stored Load(string file)
    {
        try
        {
            if (!File.Exists(file) || new FileInfo(file).Length > 512 * 1024) return new(false, []);
            var loaded = JsonSerializer.Deserialize<Stored>(File.ReadAllText(file));
            var valid = (loaded?.Items ?? []).Where(i => i is not null && !string.IsNullOrWhiteSpace(i.Id) && !string.IsNullOrWhiteSpace(i.Title))
                .Select(i => i with { Title = Clip(i.Title, 80), Body = Clip(i.Body, 400) }).Take(Limit).ToList();
            return new(loaded?.DoNotDisturb == true, valid);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException) { return new(false, []); }
    }
    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
            var temporary = file + ".tmp"; File.WriteAllText(temporary, JsonSerializer.Serialize(new Stored(doNotDisturb, items))); File.Move(temporary, file, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* History is a convenience; a failed save never blocks the notification. */ }
    }
}

/// <summary>A countdown or a stopwatch for the dynamic island.</summary>
public sealed record IslandTimer(DateTimeOffset Started, TimeSpan? Length)
{
    public static readonly int[] Presets = [1, 3, 5, 10, 15, 25];
    public bool Stopwatch => Length is null;
    public TimeSpan Remaining(DateTimeOffset now) => Length is { } length && length - (now - Started) is var left && left > TimeSpan.Zero ? left : TimeSpan.Zero;
    public TimeSpan Elapsed(DateTimeOffset now) => now - Started < TimeSpan.Zero ? TimeSpan.Zero : now - Started;
    public bool Finished(DateTimeOffset now) => Length is not null && Remaining(now) == TimeSpan.Zero;
    /// <summary>What the island shows: time left for a countdown (rounded up), elapsed time for a stopwatch.</summary>
    public string Text(DateTimeOffset now) => Format(Stopwatch ? Elapsed(now) : TimeSpan.FromSeconds(Math.Ceiling(Remaining(now).TotalSeconds)));
    public static string Format(TimeSpan span) => span.TotalHours >= 1 ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}" : $"{span.Minutes:00}:{span.Seconds:00}";
}

/// <summary>Small rules behind the island card's media and "close frozen app" controls.</summary>
public static class IslandRules
{
    /// <summary>Position after a relative seek, kept inside the track (or at zero when the length is unknown).</summary>
    public static TimeSpan Seek(TimeSpan position, TimeSpan delta, TimeSpan duration)
    {
        var target = position + delta;
        if (target < TimeSpan.Zero) target = TimeSpan.Zero;
        if (duration > TimeSpan.Zero && target > duration) target = duration;
        return target;
    }
    private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    { "explorer", "dwm", "csrss", "winlogon", "wininit", "services", "lsass", "smss", "svchost", "fontdrvhost", "sihost", "ctfmon", "taskmgr", "System", "Idle", "Registry", "Noxelyra" };
    /// <summary>Whether the island may end this process: never Windows' own shell and session processes, nor Noxelyra itself.</summary>
    public static bool CanTerminate(string? processName, int processId, int ownId) =>
        processId > 4 && processId != ownId && !string.IsNullOrWhiteSpace(processName) && !Protected.Contains(processName);
}
