using System.Windows;
using ClaxeDesktop.Core;

namespace ClaxeDesktop.App;
public sealed class WidgetWorkspace
{
    private readonly DesktopController controller;
    private readonly List<WidgetWindow> windows = [];
    private readonly WidgetLayoutHistory history = new();
    private WidgetEditorWindow? editor;
    private IReadOnlyList<WidgetInstance> baseline;
    private string? resizing;
    private bool shelfVisible = true;
    public IReadOnlyList<WidgetInstance> Items { get; private set; }
    public HashSet<string> Selection { get; } = [];
    public bool Editing { get; private set; }
    public bool Snap { get; set; }
    public IReadOnlyList<MonitorInfo> Monitors => controller.ViewModel.Monitors;
    public bool SafeMode => controller.IsSafeMode;
    public string Status => controller.ViewModel.Status;
    public WebWidgetPackages Packages { get; }
    private readonly string data;
    public event Action? Changed;
    public WidgetWorkspace(DesktopController controller, string data)
    {
        this.controller = controller; this.data = data; Packages = new(Path.Combine(data,"widget-packages"));
        Items = baseline = controller.ViewModel.Settings.Widgets.ToArray();
        Snap = controller.ViewModel.SnapToGrid;
    }
    public string UserData(string id) => Path.Combine(data,"web-data",id);
    internal void WebStatus(string message) { controller.ViewModel.Status = message; controller.Log.Write("web-widget-status"); }
    public void Start() => Render();
    internal void LoadSaved(IReadOnlyList<WidgetInstance> widgets){if(Editing)throw new InvalidOperationException("Önce widget taslağını kaydet veya iptal et.");Items=baseline=widgets.ToArray();history.Clear();Selection.Clear();Render();}
    public void ToggleShelf() { shelfVisible = !shelfVisible; UpdateActivity(); }
    public void UpdateActivity()
    {
        foreach(var window in windows)
        {
            var monitor = Monitors.FirstOrDefault(m=>m.Id==window.Model.Placement?.MonitorId) ?? Monitors.First(m=>m.Primary);
            var visible = shelfVisible && window.Model.Visible && !controller.ShouldPauseWidget(monitor);
            if(visible && !window.IsVisible) { window.Show(); Place(window); }
            else if(!visible && window.IsVisible) window.Hide();
        }
    }
    public void ShowEditor()
    {
        if (SafeMode || controller.ReadOnly) { controller.ViewModel.Status = "Güvenli veya salt okunur modda widget düzenleme kapalı."; controller.ShowSettings(); return; }
        if (editor is null)
        {
            Editing = true; shelfVisible = true; baseline = Items.ToArray(); history.Clear();
            Render();
            editor = new(this); editor.Closed += (_, _) => { editor = null; Cancel(); }; editor.Show();
        }
        editor.Activate();
    }
    public void Change(string id, Func<WidgetInstance,WidgetInstance> change) => Mutate(Items.Select(w => w.Id == id && !w.Locked ? change(w) : w).ToArray());
    /// <summary>Content typed on the widget itself (the weather city): a draft change in edit mode, saved at once otherwise.</summary>
    public async Task<bool> SetTextAsync(string id, string text)
    {
        if (Editing) { Change(id, w => w with { Text = text }); return true; }
        var next = Items.Select(w => w.Id == id ? w with { Text = text } : w).ToArray();
        try { WidgetLayout.Validate(next); } catch (InvalidDataException ex) { controller.ViewModel.Status = ex.Message; return false; }
        if (!await controller.SaveWidgetsAsync(next)) return false;
        Items = baseline = next; Render(); return true;
    }
    public void SetLock(string id, bool value) => Mutate(Items.Select(w => w.Id == id ? w with { Locked = value } : w).ToArray());
    public void Add(string kind)
    {
        if (!Editing || Items.Count >= 30) return;
        var name = kind switch { "clock" => "Saat", "calendar" => "Takvim", "note" => "Not", "music" => "Müzik", "weather" => "Hava durumu", "graph" => "Sistem grafiği", "bigclock" => "Büyük saat", _ => "Sistem ve saat" };
        var (width, height) = kind switch { "clock" => (300, 200), "music" => (300, 420), "weather" => (300, 340), "graph" => (320, 430), "bigclock" => (420, 230), _ => (300, 342) };
        var main = controller.ViewModel.Monitors.First(m => m.Primary);
        var item = new WidgetInstance { Kind = kind, Name = name, Width = width, Height = height, Text = kind == "weather" ? "" : new WidgetInstance().Text, Layer = Items.Count, Placement = new(main.Id, 360 + Items.Count * 12, 108 + Items.Count * 12) };
        Selection.Clear(); Selection.Add(item.Id); Mutate([..Items,item]);
    }
    public void Import(string path)
    {
        if (!Editing || Items.Count >= 30) return;
        var package = Packages.Import(path); var main = controller.ViewModel.Monitors.First(m => m.Primary);
        var item = new WidgetInstance { Kind = "web", Name = package.Manifest.Name, WebPackageId = package.Id, Width = package.Manifest.DefaultSizeDip.Width, Height = package.Manifest.DefaultSizeDip.Height, Placement = new(main.Id,360,108), Layer = Items.Count };
        Selection.Clear(); Selection.Add(item.Id); Mutate([..Items,item]);
    }
    public void Remove() => Mutate(Items.Where(w => !Selection.Contains(w.Id) || w.Locked).ToArray());
    public void Duplicate()
    {
        var copies = Items.Where(w => Selection.Contains(w.Id) && !w.Locked).Select(w => w with { Id = Guid.NewGuid().ToString("N"), Name = w.Name[..Math.Min(w.Name.Length,70)] + " kopya", Placement = w.Placement is null ? null : w.Placement with { X = w.Placement.X + 24, Y = w.Placement.Y + 24 } }).ToArray();
        if (Items.Count + copies.Length > 30) throw new InvalidDataException("En fazla 30 widget eklenebilir.");
        Mutate([..Items,..copies]);
    }
    public void Group(bool grouped)
    {
        var id = grouped ? Guid.NewGuid().ToString("N") : null;
        Mutate(Items.Select(w => Selection.Contains(w.Id) && !w.Locked ? w with { GroupId = id } : w).ToArray());
    }
    public void Align(string edge) => Mutate(WidgetLayout.Align(Items,Selection,edge));
    public void Move(WidgetWindow window)
    {
        if (!Editing || window.Model.Locked) { Place(window); return; }
        var target = window.Capture(controller.ViewModel.Monitors);
        if (!Selection.Contains(window.Model.Id)) { Selection.Clear(); Selection.Add(window.Model.Id); }
        if (window.Model.GroupId is { } group) foreach (var item in Items.Where(w => w.GroupId == group)) Selection.Add(item.Id);
        Mutate(WidgetLayout.MoveSelection(Items,Selection,window.Model.Id,target,controller.ViewModel.Monitors,Editing ? Snap : controller.ViewModel.SnapToGrid,controller.ViewModel.Settings.GridSize));
    }
    public void BeginResize(string id) { if (Editing && Items.Any(w => w.Id == id && !w.Locked)) { history.Push(Items); resizing = id; } }
    public void Resize(string id,double width,double height)
    {
        if (resizing != id) return;
        var minimum = Items.Single(w => w.Id == id).Kind == "web" ? Packages.Read(Items.Single(w => w.Id == id).WebPackageId!).MinSizeDip : new WidgetSize(160,120);
        Mutate(Items.Select(w => w.Id == id ? w with { Width = Math.Clamp(width,minimum.Width,1200), Height = Math.Clamp(height,minimum.Height,900) } : w).ToArray(),false);
    }
    public void EndResize() { resizing = null; Changed?.Invoke(); }
    public void Undo() { if (!Editing) return; Items = history.Undo(Items); Render(); }
    public void Redo() { if (!Editing) return; Items = history.Redo(Items); Render(); }
    private void Mutate(IReadOnlyList<WidgetInstance> next,bool remember = true)
    {
        if (!Editing || next.SequenceEqual(Items)) return;
        WidgetLayout.Validate(next); if (remember) history.Push(Items); Items = next; Render();
    }
    public async Task SaveAsync()
    {
        if (!Editing) return;
        if (await controller.SaveWidgetsAsync(Items)) { baseline = Items.ToArray(); history.Clear(); Editing = false; editor?.Close(); }
    }
    public void Cancel() { Items = baseline.ToArray(); Editing = false; history.Clear(); Selection.Clear(); Render(); }
    public void Close() { editor?.Close(); foreach (var window in windows.ToArray()) { controller.RemoveWidgetWindow(window); } windows.Clear(); }
    public void Place(WidgetWindow window)
    {
        var monitor = Monitors.FirstOrDefault(m=> !SafeMode && m.Id==window.Model.Placement?.MonitorId) ?? Monitors.First(m=>m.Primary);
        window.Width = Math.Min(window.Model.Width,monitor.Width/monitor.Scale); window.Height = Math.Min(window.Model.Height,monitor.Height/monitor.Scale);
        window.Place(SafeMode ? null : window.Model.Placement,controller.ViewModel.Monitors,Editing ? Snap : controller.ViewModel.SnapToGrid);
    }
    public void RefreshSelection() { foreach(var window in windows) { window.ApplyAppearance(controller.ViewModel); window.ShowSelection(Editing && Selection.Contains(window.Model.Id)); } }
    public void Render()
    {
        foreach (var window in windows.Where(w => !Items.Any(i => i.Id == w.Model.Id)).ToArray()) { windows.Remove(window); controller.RemoveWidgetWindow(window); }
        foreach (var item in Items.OrderBy(w => w.Layer))
        {
            var window = windows.FirstOrDefault(w => w.Model.Id == item.Id);
            if (window is null) { window = new(controller.ViewModel,this,item); windows.Add(window); controller.AddWidgetWindow(window); }
            window.Update(item); window.ApplyAppearance(controller.ViewModel);
            if (item.Visible && shelfVisible && !controller.ShouldPauseWidget(Monitors.FirstOrDefault(m=>m.Id==item.Placement?.MonitorId) ?? Monitors.First(m=>m.Primary))) { if (!window.IsVisible) window.Show(); Place(window); }
            else window.Hide();
            if (window.IsVisible && Editing) ClaxeDesktop.Platform.Win.WindowPlacement.RaiseWithoutActivation(new System.Windows.Interop.WindowInteropHelper(window).Handle);
        }
        // Store resolved monitor/DPI coordinates in the draft without writing to disk.
        Items = Items.Select(item => (item.Placement is null || Monitors.Any(m=>m.Id==item.Placement.MonitorId)) && windows.FirstOrDefault(w => w.Model.Id == item.Id && w.IsVisible) is { } window ? item with { Placement = window.Capture(controller.ViewModel.Monitors) } : item).ToArray();
        foreach (var window in windows) { window.Update(Items.Single(w => w.Id == window.Model.Id)); if(window.IsVisible) Place(window); }
        RefreshSelection();
        Changed?.Invoke();
    }
}
