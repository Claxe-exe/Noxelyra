using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using ClaxeDesktop.Core;

namespace ClaxeDesktop.App;
public sealed class SettingRowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DesktopViewModel desktop;
    private object? invalidValue;
    private bool updating;
    private string error = "";
    public SettingDefinition Definition { get; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? ValidationChanged;
    public SettingRowViewModel(DesktopViewModel desktop, SettingDefinition definition) { this.desktop = desktop; Definition = definition; desktop.PropertyChanged += DesktopChanged; }
    public string Error => error;
    public bool HasError => error.Length > 0;
    public string ValueText => Value is double number ? number.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR")) : Convert.ToString(Value, CultureInfo.GetCultureInfo("tr-TR")) ?? "";
    public string Help => Definition.Key == "appearance.surfaceOpacity" ? "Glass ve Neon yüzeylerinin saydamlığı. Flat tamamen opaktır." : Definition.Key == "dock.autoHide" ? "Kenara bağlı dock'u gizler. İmleç veya menü dock üzerindeyken açık kalır." : Definition.Description;
    public bool IsEnabled => Definition.Key switch
    {
        "appearance.surfaceOpacity" => desktop.Settings.Material != "flat",
        "dock.autoHide" => desktop.Settings.DockEdge != "floating",
        "dock.hideDelayMs" => desktop.Settings.DockAutoHide && desktop.Settings.DockEdge != "floating",
        "dock.groupWindows" => desktop.Settings.ShowRunningApps,
        "dock.magnification" or "animations.durationMs" => desktop.Settings.AnimationsEnabled && desktop.Settings.HoverAnimation,
        "animations.hoverEffects" => desktop.Settings.AnimationsEnabled,
        "editor.gridSizeDip" => desktop.Settings.SnapToGrid,
        _ => true
    };
    public object Value
    {
        get => HasError ? invalidValue! : SettingsCatalog.Value(desktop.Settings, Definition.Key);
        set
        {
            if (updating || value is null) return;
            updating = true;
            try { var next = SettingsCatalog.Apply(desktop.Settings, Definition.Key, value); error = ""; invalidValue = null; desktop.Edit(next); }
            catch (InvalidDataException ex) { error = ex.Message; invalidValue = value; }
            finally { updating = false; Refresh(); ValidationChanged?.Invoke(); }
        }
    }
    public void ClearError() { error = ""; invalidValue = null; Refresh(); ValidationChanged?.Invoke(); }
    private void DesktopChanged(object? sender, PropertyChangedEventArgs e) { if (!updating && (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(DesktopViewModel.Settings))) Refresh(); }
    private void Refresh() { foreach (var name in new[] { nameof(Value), nameof(ValueText), nameof(IsEnabled), nameof(Error), nameof(HasError) }) PropertyChanged?.Invoke(this, new(name)); }
    public void Dispose() => desktop.PropertyChanged -= DesktopChanged;
}

public sealed class SettingsEditorViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DesktopController controller;
    private string category = "Görünüm";
    private string search = "";
    public event PropertyChangedEventHandler? PropertyChanged;
    public DesktopViewModel Desktop => controller.ViewModel;
    public ObservableCollection<SettingRowViewModel> Rows { get; }
    public ICollectionView VisibleRows { get; }
    public IReadOnlyList<string> Categories { get; } = ["Görünüm", "Dock", "Paneller", "Animasyonlar", "Yerleşim", "Uygulama kısayolları", "Ekranlar ve oturum"];
    public IReadOnlyList<ThemePalette> Themes => ThemePresets.All;
    public string Status => Desktop.Status;
    public bool CanSave => !controller.ReadOnly && !Rows.Any(r => r.HasError);
    public bool CanUndo => Desktop.CanUndo;
    public bool CanRedo => Desktop.CanRedo;
    public bool CanReset => VisibleRows.Cast<object>().Any();
    public string ActiveThemeName => ThemePresets.All.Single(t => t.Id == Desktop.Settings.ThemeId).Name;
    public string ErrorSummary => Rows.Any(r => r.HasError) ? "Geçersiz değerleri düzelt veya Geri al ile son kayda dön." : "";
    public string Footer => $"{Rows.Count} çalışan ayar · değişiklikler canlı önizlenir";
    public bool ShowRows => SearchText.Length > 0 || !Categories.Skip(5).Contains(SelectedCategory);
    public bool ShowThemes => SearchText.Length == 0 && SelectedCategory == "Görünüm";
    public bool ShowShortcuts => SearchText.Length == 0 && SelectedCategory == "Uygulama kısayolları";
    public bool ShowMonitors => SearchText.Length == 0 && SelectedCategory == "Ekranlar ve oturum";
    public string Title => SearchText.Length > 0 ? "Arama sonuçları" : SelectedCategory;
    public string Subtitle => SearchText.Length > 0 ? $"“{SearchText}” için {VisibleRows.Cast<object>().Count()} ayar bulundu." : SelectedCategory switch
    {
        "Görünüm" => "Bir tema seç, masaüstünde önizle. Hazır olduğunda kaydet.",
        "Dock" => "Uygulamaların, pencere grupların ve kenar davranışları.",
        "Paneller" => "Panel ölçüleri ve saatin görünümü.",
        "Animasyonlar" => "Hareketin hızını ve yoğunluğunu belirle.",
        "Yerleşim" => "Panel, dock ve saat kartının hizalaması.",
        "Uygulama kısayolları" => "Dock'ta görmek istediğin yerel uygulamaları seç.",
        _ => "Bağlı ekranlar ve Windows başlangıç tercihi."
    };
    public string SelectedCategory { get => category; set { if (category == value) return; category = value; SearchText = ""; RefreshFilter(); } }
    public string SearchText { get => search; set { if (search == value) return; search = value.Length > 200 ? value[..200] : value; RefreshFilter(); } }
    public SettingsEditorViewModel(DesktopController controller)
    {
        this.controller = controller;
        Rows = new(SettingsCatalog.Supported.Select(d => new SettingRowViewModel(Desktop, d)));
        foreach (var row in Rows) row.ValidationChanged += RefreshState;
        VisibleRows = CollectionViewSource.GetDefaultView(Rows); VisibleRows.Filter = value => value is SettingRowViewModel row && (SearchText.Length > 0 ? row.Definition.Matches(SearchText) : row.Definition.Category == SelectedCategory);
        Desktop.PropertyChanged += DesktopChanged;
    }
    private void RefreshFilter()
    {
        VisibleRows.Refresh(); foreach (var name in new[] { nameof(SelectedCategory), nameof(SearchText), nameof(Title), nameof(Subtitle), nameof(ShowRows), nameof(ShowThemes), nameof(ShowShortcuts), nameof(ShowMonitors), nameof(CanReset) }) Notify(name);
    }
    private void RefreshState() { foreach (var name in new[] { nameof(CanSave), nameof(CanUndo), nameof(CanRedo), nameof(Status), nameof(ErrorSummary), nameof(ActiveThemeName) }) Notify(name); }
    private void DesktopChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is nameof(DesktopViewModel.Settings) or nameof(DesktopViewModel.Status) or nameof(DesktopViewModel.CanUndo) or nameof(DesktopViewModel.CanRedo)) RefreshState();
    }
    public void ClearErrors() { foreach (var row in Rows) row.ClearError(); }
    public void SelectTheme(string id) { ClearErrors(); Desktop.Edit(SettingsCatalog.Apply(Desktop.Settings, "themes.activeTheme", id)); }
    public void ResetVisible() { var keys = VisibleRows.Cast<SettingRowViewModel>().Select(r => r.Definition.Key).ToArray(); ClearErrors(); Desktop.Edit(SettingsCatalog.Reset(Desktop.Settings, keys)); }
    public void Undo() { ClearErrors(); Desktop.Undo(); }
    public void Redo() { ClearErrors(); Desktop.Redo(); }
    public void Dispose() { Desktop.PropertyChanged -= DesktopChanged; foreach (var row in Rows) { row.ValidationChanged -= RefreshState; row.Dispose(); } }
    private void Notify([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new(name));
}
