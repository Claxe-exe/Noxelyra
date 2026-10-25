using System.Windows;
using System.Windows.Controls;
using ClaxeDesktop.Core;
using Microsoft.Win32;

namespace ClaxeDesktop.App;
public partial class SettingsWindow : Window
{
    private readonly DesktopController controller;
    internal SettingsEditorViewModel Editor { get; }
    public SettingsWindow(DesktopController controller)
    {
        this.controller = controller; Editor = new(controller); InitializeComponent(); Glass.PrepareFramed(this, controller.ViewModel.Settings); Motion.Attach(this, controller.ViewModel, () => controller.IsExiting); DataContext = Editor;
        StartupCheck.IsChecked = controller.Startup.Enabled;
        Closing += (_, _) => { Editor.ClearErrors(); controller.RevertPreview(); };
        Closed += (_, _) => Editor.Dispose();
    }
    private async void Save(object sender, RoutedEventArgs e) { if (Editor.CanSave) await controller.SaveAsync(); }
    private void Revert(object sender, RoutedEventArgs e) { Editor.ClearErrors(); controller.RevertPreview(); }
    private void Widgets(object sender, RoutedEventArgs e) => controller.Widgets.ShowEditor();
    private void Controls(object sender, RoutedEventArgs e) => controller.ShowFullControls();
    private void Applications(object sender, RoutedEventArgs e) => controller.ShowLauncher();
    private void Undo(object sender, RoutedEventArgs e) => Editor.Undo();
    private void Redo(object sender, RoutedEventArgs e) => Editor.Redo();
    private void SelectTheme(object sender, RoutedEventArgs e) { if (sender is Button { Tag: string id }) Editor.SelectTheme(id); }
    private void ResetCategory(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Görünen ayarları varsayılanlarına döndürmek istiyor musun? Kaydetmeden önce geri alabilirsin.", "Ayarları sıfırla", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes) Editor.ResetVisible();
    }
    private async void ImportTheme(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "Noxelyra teması seç", Filter = "Noxelyra teması|*.claxetheme.json;*.json", CheckFileExists = true };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(picker.FileName).Length > 32768) throw new InvalidDataException("Tema dosyası 32 KB sınırını aşıyor.");
            PreviewThemeText(await File.ReadAllTextAsync(picker.FileName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { controller.ViewModel.Status = "Tema yüklenemedi. Desteklenen ve geçerli bir Noxelyra tema dosyası seç."; controller.Log.Write("theme-import-rejected", ex); }
    }
    internal void PreviewThemeText(string text)
    {
        var theme = ThemeFiles.Parse(text); var next = theme.ApplyTo(controller.ViewModel.Settings);
        Editor.ClearErrors(); controller.ViewModel.Edit(next); controller.ViewModel.Status = "İçe aktarılan tema önizleniyor. Kaydet veya Geri al.";
    }
    private async void ExportTheme(object sender, RoutedEventArgs e)
    {
        if (!Editor.CanSave) { controller.ViewModel.Status = "Dışa aktarmadan önce geçersiz değerleri düzelt."; return; }
        var picker = new SaveFileDialog { Title = "Noxelyra temasını kaydet", Filter = "Noxelyra teması|*.claxetheme.json", FileName = "Benim-Noxelyra-Temam.claxetheme.json", AddExtension = true };
        if (picker.ShowDialog(this) != true) return;
        try { await File.WriteAllTextAsync(picker.FileName, ThemeFiles.Serialize(controller.ViewModel.Settings)); controller.ViewModel.Status = "Tema dışa aktarıldı. Yalnız görsel ayarlar içerir."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { controller.Log.Write("theme-export-failed", ex); controller.ViewModel.Status = "Tema kaydedilemedi. Dosya konumunu kontrol et."; }
    }
    private void ResetLayout(object sender, RoutedEventArgs e) => controller.ResetLayout();
    private void RefreshMonitors(object sender, RoutedEventArgs e) => controller.RefreshMonitors();
    private void ExitApp(object sender, RoutedEventArgs e) => controller.Exit();
    private void AddShortcut(object sender, RoutedEventArgs e)
    {
        if (controller.ViewModel.Shortcuts.Count >= 24) { controller.ViewModel.Status = "En fazla 24 kısayol eklenebilir."; return; }
        var picker = new OpenFileDialog { Title = "Dock uygulaması seç", Filter = "Uygulama veya kısayol|*.exe;*.lnk", DereferenceLinks = false, CheckFileExists = true };
        if (picker.ShowDialog(this) == true)
        {
            if (!LaunchPolicy.IsAllowed(picker.FileName)) { controller.ViewModel.Status = "Yalnızca yerel uygulamalar destekleniyor."; return; }
            controller.ViewModel.Add(new(Path.GetFileNameWithoutExtension(picker.FileName), picker.FileName)); controller.ApplyAppearance();
        }
    }
    private void RemoveShortcut(object sender, RoutedEventArgs e) { if (ShortcutList.SelectedItem is Shortcut item) { controller.ViewModel.Remove(item); controller.ApplyAppearance(); } }
    private void LaunchShortcut(object sender, RoutedEventArgs e) { if (ShortcutList.SelectedItem is Shortcut item) controller.Launch(item); }
    private void ShortcutSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RemoveShortcutButton is null) return;
        RemoveShortcutButton.IsEnabled = LaunchShortcutButton.IsEnabled = ShortcutList.SelectedItem is Shortcut;
    }
    private void ChangeStartup(object sender, RoutedEventArgs e)
    {
        try { controller.Startup.SetEnabled(StartupCheck.IsChecked == true); controller.ViewModel.Status = "Windows başlangıç tercihi uygulandı."; }
        catch (Exception ex) { controller.Log.Write("startup-setting-failed", ex); controller.ViewModel.Status = "Başlangıç ayarı uygulanamadı; mevcut tercih korundu."; }
        StartupCheck.IsChecked = controller.Startup.Enabled;
    }
}
