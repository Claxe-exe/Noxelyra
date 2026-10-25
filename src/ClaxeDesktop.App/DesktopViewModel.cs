using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using ClaxeDesktop.Core;

namespace ClaxeDesktop.App;
public sealed class DesktopViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? AppearanceChanged;
    private Settings settings;
    private string status = "Hazır";
    private readonly SettingsHistory history = new();
    public bool CanUndo => history.CanUndo;
    public bool CanRedo => history.CanRedo;
    public DesktopViewModel(Settings initial) { settings = initial; RefreshShortcuts(); }
    public Settings Settings => settings;
    public string ThemeName => ThemePresets.Resolve(settings).Name;
    public ObservableCollection<Shortcut> Shortcuts { get; } = [];
    public ObservableCollection<MonitorInfo> Monitors { get; } = [];
    public string Status { get => status; set { status = value; Notify(); } }
    public double SurfaceOpacity { get => settings.Opacity; set { settings = settings with { Opacity = value }; Changed(); } }
    public int IconSize { get => settings.IconSize; set { settings = settings with { IconSize = value }; Changed(); } }
    public int PanelWidth { get => settings.PanelWidth; set { settings = settings with { PanelWidth = value }; Changed(); } }
    public int PanelHeight { get => settings.PanelHeight; set { settings = settings with { PanelHeight = value }; Changed(); } }
    public bool ShowSeconds { get => settings.ShowSeconds; set { settings = settings with { ShowSeconds = value }; Changed(); } }
    public bool SnapToGrid { get => settings.SnapToGrid; set { settings = settings with { SnapToGrid = value }; Changed(); } }
    public bool ShowRunningApps { get => settings.ShowRunningApps; set { settings = settings with { ShowRunningApps = value }; Changed(); } }
    public bool GroupWindows { get => settings.GroupWindows; set { settings = settings with { GroupWindows = value }; Changed(); } }
    public string DockEdge { get => settings.DockEdge; set { settings = settings with { DockEdge = value }; Changed(); } }
    public bool DockAutoHide { get => settings.DockAutoHide; set { settings = settings with { DockAutoHide = value }; Changed(); } }
    public bool DockOnTop { get => settings.DockOnTop; set { settings = settings with { DockOnTop = value }; Changed(); } }
    public bool HoverAnimation { get => settings.HoverAnimation; set { settings = settings with { HoverAnimation = value }; Changed(); } }
    public bool ClockOnLeft { get => settings.ClockOnLeft; set { settings = settings with { ClockOnLeft = value }; Changed(); } }
    public string Time { get; private set; } = "";
    public string Date { get; private set; } = "";
    public string ShortDate { get; private set; } = "";
    public string Greeting { get; private set; } = "";
    public string CpuText { get; private set; } = "Ölçülüyor…";
    public string MemoryText { get; private set; } = "Ölçülüyor…";
    public string MemoryDetail { get; private set; } = "";
    public double CpuValue { get; private set; }
    public double MemoryValue { get; private set; }
    public void Tick(SystemSample sample)
    {
        var now = DateTime.Now;
        var format = settings.ClockFormat == "12h" ? (ShowSeconds ? "hh:mm:ss tt" : "hh:mm tt") : (ShowSeconds ? "HH:mm:ss" : "HH:mm");
        Time = now.ToString(format, CultureInfo.GetCultureInfo("tr-TR"));
        Date = now.ToString("dddd · d MMMM", CultureInfo.GetCultureInfo("tr-TR")).ToUpper(CultureInfo.GetCultureInfo("tr-TR"));
        ShortDate = now.ToString("dddd d MMMM", CultureInfo.GetCultureInfo("tr-TR"));
        Greeting = now.Hour is >= 6 and < 12 ? "Günaydın." : now.Hour is >= 12 and < 18 ? "İyi günler." : "İyi akşamlar.";
        CpuText = sample.Cpu is { } cpu ? $"%{cpu:0}" : "Ölçülüyor / erişilemiyor";
        MemoryText = sample.Memory is { } memory ? $"%{memory:0}" : "Erişilemiyor";
        MemoryDetail = sample.TotalGb is { } total ? $"{sample.UsedGb:0.0} / {total:0.0} GB kullanımda" : "Bellek bilgisi alınamadı";
        CpuValue = sample.Cpu ?? 0; MemoryValue = sample.Memory ?? 0;
        foreach (var name in new[] { nameof(Time), nameof(Date), nameof(ShortDate), nameof(Greeting), nameof(CpuText), nameof(MemoryText), nameof(MemoryDetail), nameof(CpuValue), nameof(MemoryValue) }) Notify(name);
    }
    public void Replace(Settings value) { history.Clear(); Set(value); }
    internal void SetAdvanced(Settings value){settings=settings with{DesktopSpace=value.DesktopSpace,ActiveProfileId=value.ActiveProfileId,ProfileManualUntil=value.ProfileManualUntil,LayoutShortcut=value.LayoutShortcut,Mascot=value.Mascot,Keyboard=value.Keyboard};Notify(nameof(Settings));}
    internal void SetWidgets(IReadOnlyList<WidgetInstance> widgets) => settings = settings with { Widgets = widgets.ToList() };
    internal void SetDesktopSettings(Settings value) { settings=settings with { IntegratedDesktop=value.IntegratedDesktop,HideWindowsTaskbar=value.HideWindowsTaskbar,GlobalShortcuts=value.GlobalShortcuts,WallpaperEnabled=value.WallpaperEnabled,Wallpapers=value.Wallpapers,WallpaperRotations=value.WallpaperRotations }; Notify(nameof(Settings)); }
    public void Edit(Settings value) { value.Validate(); if (value == settings) return; history.Push(settings); status = "Değişiklikler önizleniyor. Kalıcılaştırmak için Kaydet."; Set(value); }
    private void Set(Settings value)
    {
        var changed = !settings.Shortcuts.SequenceEqual(value.Shortcuts); settings = value;
        if (changed) RefreshShortcuts(); Notify(""); AppearanceChanged?.Invoke();
    }
    private Settings PreserveIndependent(Settings value) => value with { DesktopSpace=settings.DesktopSpace,Panel=settings.Panel,Dock=settings.Dock,Widget=settings.Widget,Widgets=settings.Widgets,IntegratedDesktop=settings.IntegratedDesktop,HideWindowsTaskbar=settings.HideWindowsTaskbar,GlobalShortcuts=settings.GlobalShortcuts,WallpaperEnabled=settings.WallpaperEnabled,Wallpapers=settings.Wallpapers,WallpaperRotations=settings.WallpaperRotations,ActiveProfileId=settings.ActiveProfileId,ProfileManualUntil=settings.ProfileManualUntil,LayoutShortcut=settings.LayoutShortcut,Mascot=settings.Mascot,Keyboard=settings.Keyboard };
    public void Undo() { if (history.CanUndo) Set(PreserveIndependent(history.Undo(settings))); }
    public void Redo() { if (history.CanRedo) Set(PreserveIndependent(history.Redo(settings))); }
    public void AcceptChanges() { history.Clear(); Notify(nameof(CanUndo)); Notify(nameof(CanRedo)); }
    public void UpdatePlacement(string kind, Placement value) => settings = kind switch { "Panel" => settings with { Panel = value }, "Dock" => settings with { Dock = value }, _ => settings with { Widget = value } };
    public void Add(Shortcut shortcut) { settings = settings with { Shortcuts = [.. settings.Shortcuts, shortcut] }; RefreshShortcuts(); }
    public void Remove(Shortcut shortcut) { settings = settings with { Shortcuts = settings.Shortcuts.Where(s => s != shortcut).ToList() }; RefreshShortcuts(); }
    public void MoveShortcut(Shortcut shortcut, int direction)
    {
        var list = settings.Shortcuts.ToList(); var index = list.IndexOf(shortcut); var next = index + direction;
        if (index < 0 || next < 0 || next >= list.Count) return;
        (list[index], list[next]) = (list[next], list[index]); settings = settings with { Shortcuts = list }; RefreshShortcuts();
    }
    private void RefreshShortcuts() { Shortcuts.Clear(); foreach (var item in settings.Shortcuts) Shortcuts.Add(item); }
    private void Changed([CallerMemberName] string name = "") { Notify(name); Notify(nameof(Settings)); AppearanceChanged?.Invoke(); }
    private void Notify([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new(name));
}



