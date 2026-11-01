using System.Globalization;
using System.Text;
using System.Text.Json;
namespace ClaxeDesktop.Core;

/// <summary>Launcher ranking: tolerant of Turkish letters, initials and small typos. A higher score is a better match; null is no match.</summary>
public static class FuzzyMatcher
{
    /// <summary>The score from which a match counts as a real text hit, not a guess (used for files and windows).</summary>
    public const int Literal = 500;
    public static string Fold(string text)
    {
        var folded = new StringBuilder(text.Length);
        foreach (var c in text)
            folded.Append(c switch { 'İ' or 'I' or 'ı' => 'i', 'Ş' or 'ş' => 's', 'Ğ' or 'ğ' => 'g', 'Ü' or 'ü' => 'u', 'Ö' or 'ö' => 'o', 'Ç' or 'ç' => 'c', _ => char.ToLowerInvariant(c) });
        return folded.ToString();
    }
    private static readonly char[] Separators = [' ', '-', '_', '.', '(', ')', '/', '\\'];
    public static int? Score(string name, string query)
    {
        if (query.Length == 0) return 0;
        string n = Fold(name), q = Fold(query).Trim();
        if (q.Length == 0) return 0;
        if (n == q) return 1000;
        if (n.StartsWith(q, StringComparison.Ordinal)) return 800 + Math.Max(0, 60 - n.Length);
        var words = n.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(w => w.StartsWith(q, StringComparison.Ordinal))) return 680;
        var parts = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1 && parts.All(p => n.Contains(p, StringComparison.Ordinal))) return 600;
        if (n.Contains(q, StringComparison.Ordinal)) return Literal + 20;
        if (q.Length >= 2 && string.Concat(words.Select(w => w[0])).StartsWith(q, StringComparison.Ordinal)) return 460;
        if (q.Length < 3) return null;
        // In order with few letters between: "chrm" finds Chrome.
        int at = -1, first = -1;
        foreach (var c in q) { at = n.IndexOf(c, at + 1); if (at < 0) break; if (first < 0) first = at; }
        if (at >= 0 && at - first < q.Length * 2) return 300 - (at - first - q.Length);
        // One typo in up to six letters, two in longer queries, against a word or the same-length start of a word.
        var allowed = q.Length >= 7 ? 2 : q.Length >= 4 ? 1 : 0;
        if (allowed == 0) return null;
        var best = words.Select(w => Math.Min(Distance(w, q), w.Length > q.Length ? Distance(w[..q.Length], q) : int.MaxValue)).DefaultIfEmpty(int.MaxValue).Min();
        return best <= allowed ? 200 - best * 40 : null;
    }
    // Optimal string alignment: insert, delete, replace and swapping two neighbouring letters each cost 1.
    private static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
            for (var j = 1; j <= b.Length; j++)
            {
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1]) d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
            }
        return d[a.Length, b.Length];
    }
}

/// <summary>Arithmetic only: + - * / ^ %, parentheses, kök()/sqrt() and pi. Nothing is ever executed; anything else is simply not a calculation.</summary>
public static class Calculator
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    /// <summary>The value of <paramref name="text"/>, or null when it is not a calculation (a plain number needs a leading "=").</summary>
    public static double? Evaluate(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 200) return null;
        var source = text.Trim(); var forced = source[0] == '=';
        if (forced) source = source[1..];
        if (!forced && !source.Any(c => "+*/^%x×÷".Contains(c)) && !(source.IndexOf('-', 1 < source.Length ? 1 : 0) > 0) && !Fold(source).Contains("kok(") && !Fold(source).Contains("sqrt(")) return null;
        try
        {
            var parser = new Parser(Fold(source)); var value = parser.Run();
            return double.IsFinite(value) ? value : null;
        }
        catch (FormatException) { return null; }
    }
    public static string Format(double value) => Math.Round(value, 10).ToString("#,0.##########", Turkish);
    private static string Fold(string text) => FuzzyMatcher.Fold(text).Replace(" ", "").Replace('×', '*').Replace('÷', '/').Replace('x', '*');
    private sealed class Parser(string text)
    {
        private int at, depth;
        private char Next => at < text.Length ? text[at] : '\0';
        internal double Run() { var value = Sum(); if (at != text.Length) throw new FormatException(); return value; }
        private double Sum()
        {
            var value = Product();
            while (Next is '+' or '-') { var plus = text[at++] == '+'; var right = Product(); value = plus ? value + right : value - right; }
            return value;
        }
        private double Product()
        {
            var value = Unary();
            while (Next is '*' or '/') { var times = text[at++] == '*'; var right = Unary(); value = times ? value * right : value / right; }
            return value;
        }
        private double Unary()
        {
            if (Next == '-') { at++; return -Unary(); }
            if (Next == '+') { at++; return Unary(); }
            return Power();
        }
        private double Power()
        {
            var value = Atom();
            while (Next == '%') { at++; value /= 100; }
            if (Next == '^') { at++; return Math.Pow(value, Unary()); }
            return value;
        }
        private double Atom()
        {
            if (++depth > 40) throw new FormatException();
            try
            {
                if (Next == '(') { at++; var inner = Sum(); if (Next != ')') throw new FormatException(); at++; return inner; }
                foreach (var name in new[] { "kok(", "sqrt(" })
                    if (string.CompareOrdinal(text, at, name, 0, name.Length) == 0)
                    { at += name.Length; var inner = Sum(); if (Next != ')') throw new FormatException(); at++; return Math.Sqrt(inner); }
                if (string.CompareOrdinal(text, at, "pi", 0, 2) == 0) { at += 2; return Math.PI; }
                var start = at;
                while (char.IsAsciiDigit(Next) || Next is '.' or ',') at++;
                if (at == start) throw new FormatException();
                var digits = text[start..at].Replace(',', '.');
                return double.TryParse(digits, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) ? number : throw new FormatException();
            }
            finally { depth--; }
        }
    }
}

/// <summary>Which apps were opened from the launcher and how recently; kept only on this PC and clearable from the launcher.</summary>
public sealed class LauncherHistory
{
    public sealed record Entry(string Target, int Count, DateTimeOffset Last);
    public const int MaxEntries = 200;
    private readonly Dictionary<string, Entry> entries = new(StringComparer.OrdinalIgnoreCase);
    public int Count => entries.Count;
    public void Record(string target, DateTimeOffset now)
    {
        entries[target] = new(target, (entries.GetValueOrDefault(target)?.Count ?? 0) + 1, now);
        if (entries.Count > MaxEntries) entries.Remove(entries.Values.OrderBy(e => e.Last).First().Target);
    }
    /// <summary>Use count, halved for every two weeks since the last use.</summary>
    public double Score(string target, DateTimeOffset now) => entries.TryGetValue(target, out var e) ? e.Count * Math.Pow(.5, Math.Max(0, (now - e.Last).TotalDays) / 14) : 0;
    public IReadOnlyList<string> Top(int count, DateTimeOffset now) => entries.Values.OrderByDescending(e => Score(e.Target, now)).ThenByDescending(e => e.Last).Take(count).Select(e => e.Target).ToArray();
    public void Clear() => entries.Clear();
    public string Serialize() => JsonSerializer.Serialize(entries.Values);
    /// <summary>Reads a saved history; a damaged file gives an empty one, since this is only a convenience.</summary>
    public static LauncherHistory Parse(string json)
    {
        var history = new LauncherHistory();
        try
        {
            foreach (var entry in (JsonSerializer.Deserialize<List<Entry>>(json) ?? []).Take(MaxEntries))
                if (entry is { Target.Length: > 0 and <= 4096, Count: > 0 }) history.entries[entry.Target] = entry;
        }
        catch (JsonException) { history.entries.Clear(); }
        return history;
    }
}

/// <summary>The clipboard history: text only, in memory only, newest first, so it is gone when Noxelyra exits.</summary>
public sealed class ClipboardBuffer
{
    public const int MaxItems = 30, MaxLength = 20000;
    private readonly List<string> items = [];
    public IReadOnlyList<string> Items => items;
    public void Add(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxLength) return;
        items.Remove(text); items.Insert(0, text);
        if (items.Count > MaxItems) items.RemoveAt(items.Count - 1);
    }
    public void Clear() => items.Clear();
}

/// <summary>Names of files under the user's folders, read only when the launcher searches. Hidden and system items, links, and build or dependency folders are skipped.</summary>
public static class FileIndex
{
    private static readonly HashSet<string> Skipped = new(["node_modules", ".git", ".vs", "bin", "obj", "AppData", "__pycache__"], StringComparer.OrdinalIgnoreCase);
    public static IReadOnlyList<string> Build(IEnumerable<string> roots, int limit, int depth, CancellationToken token)
    {
        var found = new List<string>(); var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint | FileAttributes.Offline };
        var pending = new Queue<(string Path, int Depth)>(roots.Where(Directory.Exists).Select(r => (r, 0)));
        while (pending.Count > 0 && found.Count < limit)
        {
            token.ThrowIfCancellationRequested();
            var (folder, level) = pending.Dequeue();
            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(folder, "*", options))
                {
                    if (found.Count >= limit) break;
                    found.Add(entry);
                    if (level < depth && Directory.Exists(entry) && !Skipped.Contains(Path.GetFileName(entry))) pending.Enqueue((entry, level + 1));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return found;
    }
}

/// <summary>What the launcher may send to the browser, and only when the user presses Enter on that row.</summary>
public static class WebTarget
{
    public const string SearchPrefix = "https://www.google.com/search?q=";
    public static string SearchUrl(string query) => SearchPrefix + Uri.EscapeDataString(query.Trim());
    /// <summary>An https/http address typed in the launcher ("github.com", "https://…"), or null.</summary>
    public static string? Address(string query)
    {
        var text = query.Trim();
        if (text.Length is < 4 or > 2000 || text.Any(char.IsWhiteSpace)) return null;
        if (!text.Contains("://", StringComparison.Ordinal))
        {
            var host = text.Split('/')[0];
            if (host.Length < 4 || !host.Contains('.') || host.StartsWith('.') || host.EndsWith('.') || !host.All(c => char.IsLetterOrDigit(c) || c is '.' or '-') || host.Split('.')[^1].Length < 2 || host.Split('.')[^1].All(char.IsDigit)) return null;
            text = "https://" + text;
        }
        return Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" ? uri.AbsoluteUri : null;
    }
}

public static class EmojiCatalog
{
    public sealed record Emoji(string Glyph, string Keywords);
    public static IReadOnlyList<Emoji> All { get; } =
    [
        new("😀", "gülümseme mutlu smile"), new("😂", "kahkaha gülme ağlama lol joy"), new("🤣", "yerlere yatma gülme rofl"), new("😅", "terleme gülme sweat"), new("😊", "mutlu utangaç blush"),
        new("😍", "aşık kalp göz love"), new("🥰", "sevgi kalpler love"), new("😘", "öpücük kiss"), new("😎", "havalı gözlük cool"), new("🤔", "düşünme hmm think"),
        new("🙄", "göz devirme roll"), new("😴", "uyku uyuyan sleep"), new("😭", "ağlama hüngür cry"), new("😢", "üzgün gözyaşı sad"), new("😡", "kızgın sinir angry"),
        new("🤬", "küfür öfke rage"), new("😱", "korku çığlık scream"), new("🤯", "kafa patlama mind blown"), new("🥳", "parti kutlama party"), new("🤩", "hayran yıldız star"),
        new("😇", "melek masum angel"), new("😈", "şeytan devil"), new("💀", "kuru kafa ölü skull"), new("👻", "hayalet ghost"), new("🤡", "palyaço clown"),
        new("🥲", "buruk gülme"), new("😬", "gergin diş"), new("🫡", "selam asker salute"), new("🤝", "tokalaşma anlaşma handshake"), new("👍", "beğen tamam ok like"),
        new("👎", "beğenmedim dislike"), new("👏", "alkış clap"), new("🙏", "lütfen dua teşekkür pray"), new("💪", "güç kas strong"), new("👀", "gözler bakış eyes"),
        new("👋", "merhaba selam el wave"), new("✌", "barış zafer peace"), new("🤞", "şans parmak luck"), new("👌", "tamam süper ok"), new("🫶", "kalp eller"),
        new("❤", "kalp kırmızı aşk heart"), new("🧡", "kalp turuncu"), new("💛", "kalp sarı"), new("💚", "kalp yeşil"), new("💙", "kalp mavi"),
        new("💜", "kalp mor purple"), new("🖤", "kalp siyah"), new("🤍", "kalp beyaz"), new("💔", "kırık kalp"), new("✨", "parıltı sparkle"),
        new("🔥", "ateş fire yanıyor"), new("⭐", "yıldız star"), new("🌙", "ay hilal gece moon"), new("☀", "güneş sun"), new("🌧", "yağmur bulut rain"),
        new("⚡", "şimşek elektrik lightning"), new("❄", "kar soğuk snow"), new("🌈", "gökkuşağı rainbow"), new("🌸", "çiçek sakura kiraz"), new("🌹", "gül rose"),
        new("🐱", "kedi cat"), new("🐶", "köpek dog"), new("🦊", "tilki fox"), new("🐺", "kurt wolf"), new("🐸", "kurbağa frog"),
        new("🍕", "pizza"), new("🍔", "hamburger burger"), new("🍟", "patates kızartması fries"), new("☕", "kahve coffee çay"), new("🍺", "bira beer"),
        new("🎉", "kutlama konfeti party"), new("🎁", "hediye gift"), new("🎂", "pasta doğum günü cake"), new("🎮", "oyun gamepad game"), new("🕹", "joystick oyun"),
        new("🎧", "kulaklık müzik headphones"), new("🎵", "nota müzik music"), new("🎬", "film sinema movie"), new("📷", "fotoğraf kamera camera"), new("💻", "bilgisayar laptop"),
        new("🖥", "ekran masaüstü monitor"), new("⌨", "klavye keyboard"), new("🖱", "fare mouse"), new("📱", "telefon phone"), new("🔋", "pil battery"),
        new("💡", "ampul fikir idea"), new("🔒", "kilit lock"), new("🔑", "anahtar key"), new("📌", "raptiye pin"), new("📎", "ataş clip"),
        new("📁", "klasör folder"), new("📝", "not yazı note"), new("📅", "takvim calendar"), new("⏰", "alarm saat clock"), new("🗑", "çöp kutusu trash"),
        new("✅", "tamam onay check"), new("❌", "hayır çarpı cross"), new("⚠", "uyarı dikkat warning"), new("❓", "soru question"), new("❗", "ünlem"),
        new("🚀", "roket rocket"), new("🚗", "araba car"), new("✈", "uçak plane"), new("🏠", "ev home"), new("🌍", "dünya world"),
        new("💰", "para money"), new("🏆", "kupa ödül trophy"), new("🇹🇷", "türkiye bayrak flag"), new("🤖", "robot"), new("👑", "taç kral crown"),
    ];
    public static IEnumerable<Emoji> Search(string query)
    {
        var q = FuzzyMatcher.Fold(query).Trim();
        return q.Length == 0 ? All : All.Where(e => FuzzyMatcher.Fold(e.Keywords).Contains(q, StringComparison.Ordinal));
    }
}
