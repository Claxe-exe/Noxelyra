namespace ClaxeDesktop.Core;

public sealed record WidgetInstance
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Kind { get; init; } = "system";
    public string Name { get; init; } = "Sistem ve saat";
    public Placement? Placement { get; init; }
    public double Width { get; init; } = 300;
    public double Height { get; init; } = 342;
    public bool Visible { get; init; } = true;
    public bool Locked { get; init; }
    public int Layer { get; init; }
    public string? GroupId { get; init; }
    public string Text { get; init; } = "Bugün neye odaklanıyorsun?";
    public string? WebPackageId { get; init; }
    public static WidgetInstance Default { get; } = new() { Id = "system-main" };
    public override string ToString() => $"{Name}{(Locked ? " · kilitli" : "")}{(!Visible ? " · gizli" : "")}";
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || Id.Length > 64 || !Id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') || string.IsNullOrWhiteSpace(Name) || Name.Length > 80)
            throw new InvalidDataException("Widget kimliği veya adı geçersiz.");
        if (Kind is not ("system" or "clock" or "calendar" or "note" or "web")) throw new InvalidDataException("Widget türü desteklenmiyor.");
        if (!double.IsFinite(Width) || !double.IsFinite(Height) || Width is < 160 or > 1200 || Height is < 120 or > 900 || Layer is < 0 or > 1000 || Text is null || Text.Length > 4000)
            throw new InvalidDataException("Widget ölçüsü veya içeriği geçersiz.");
        if (GroupId is not null && (GroupId.Length > 64 || !GroupId.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))) throw new InvalidDataException("Widget grubu geçersiz.");
        if (Kind == "web" && (WebPackageId is null || !Guid.TryParseExact(WebPackageId, "N", out _))) throw new InvalidDataException("Web widget paket kimliği geçersiz.");
        if (Placement is { } p && (string.IsNullOrWhiteSpace(p.MonitorId) || !double.IsFinite(p.X) || !double.IsFinite(p.Y) || Math.Abs(p.X) > 100000 || Math.Abs(p.Y) > 100000)) throw new InvalidDataException("Widget konumu geçersiz.");
    }
}

public static class WidgetLayout
{
    public static void Validate(IReadOnlyList<WidgetInstance> widgets)
    {
        if (widgets is null || widgets.Count > 30 || widgets.Select(w => w?.Id).Distinct(StringComparer.Ordinal).Count() != widgets.Count) throw new InvalidDataException("Widget sayısı veya kimlikleri geçersiz.");
        foreach (var widget in widgets) { if (widget is null) throw new InvalidDataException("Boş widget kaydı."); widget.Validate(); }
    }
    public static IReadOnlyList<WidgetInstance> MoveSelection(IReadOnlyList<WidgetInstance> items, IReadOnlySet<string> selected, string movedId, Placement target, IReadOnlyList<MonitorInfo> monitors, bool snap, int grid)
    {
        var moved = items.Single(w => w.Id == movedId); if (moved.Locked || moved.Placement is null) return items;
        var resolved = LayoutEngine.Resolve(target, monitors, moved.Width, moved.Height, 0, 0, snap, grid);
        var dx = resolved.X - moved.Placement.X; var dy = resolved.Y - moved.Placement.Y;
        return items.Select(w =>
        {
            if (w.Locked || !selected.Contains(w.Id) || w.Placement is null) return w;
            var sameMonitor = w.Placement.MonitorId == moved.Placement.MonitorId;
            var next = new Placement(sameMonitor ? resolved.Monitor.Id : w.Placement.MonitorId, w.Placement.X + dx, w.Placement.Y + dy);
            var fit = LayoutEngine.Resolve(next, monitors, w.Width, w.Height, 0, 0, snap, grid);
            return w with { Placement = new(fit.Monitor.Id, fit.X, fit.Y) };
        }).ToArray();
    }
    public static IReadOnlyList<WidgetInstance> Align(IReadOnlyList<WidgetInstance> items, IReadOnlySet<string> selected, string direction)
    {
        var chosen = items.Where(w => selected.Contains(w.Id) && !w.Locked && w.Placement is not null).ToArray();
        if (chosen.Length < 2) return items;
        if (chosen.Select(w => w.Placement!.MonitorId).Distinct().Count() != 1) throw new InvalidDataException("Hizalama için aynı ekrandaki widget'ları seç.");
        var x = chosen.Min(w => w.Placement!.X); var y = chosen.Min(w => w.Placement!.Y);
        return items.Select(w => chosen.Contains(w) ? w with { Placement = w.Placement! with { X = direction == "left" ? x : w.Placement!.X, Y = direction == "top" ? y : w.Placement!.Y } } : w).ToArray();
    }
}
public sealed class WidgetLayoutHistory
{
    private readonly List<WidgetInstance[]> undo = [];
    private readonly List<WidgetInstance[]> redo = [];
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public void Push(IReadOnlyList<WidgetInstance> widgets) { undo.Add(widgets.ToArray()); if (undo.Count > 50) undo.RemoveAt(0); redo.Clear(); }
    public IReadOnlyList<WidgetInstance> Undo(IReadOnlyList<WidgetInstance> current) { if (!CanUndo) return current; redo.Add(current.ToArray()); var value = undo[^1]; undo.RemoveAt(undo.Count - 1); return value; }
    public IReadOnlyList<WidgetInstance> Redo(IReadOnlyList<WidgetInstance> current) { if (!CanRedo) return current; undo.Add(current.ToArray()); var value = redo[^1]; redo.RemoveAt(redo.Count - 1); return value; }
    public void Clear() { undo.Clear(); redo.Clear(); }
}
