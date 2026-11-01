using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

/// <summary>Quick look for the desktop: Space opens the selected file read-only, Space or Esc closes, the arrow keys walk through the other files.</summary>
internal sealed class FilePreviewWindow : Window
{
    private const int MaxText = 256 * 1024;
    private const long MaxImage = 64 * 1024 * 1024;
    private static readonly string[] TextTypes = [".txt", ".md", ".log", ".json", ".xml", ".csv", ".ini", ".cfg", ".yml", ".yaml", ".cs", ".js", ".ts", ".py", ".html", ".css", ".bat", ".ps1"];
    private static readonly string[] VideoTypes = [".mp4", ".m4v", ".wmv", ".mp3", ".wav"];
    private static readonly string[] ImageTypes = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp"];
    private readonly ContentControl stage = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly TextBlock name = new() { FontSize = 14, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock facts = BarPopup.Muted("", 11);
    private readonly ShellIconService icons;
    private readonly Func<DesktopFile, int, DesktopFile?> neighbour;
    private DesktopFile file;
    private MediaElement? playing;
    internal FilePreviewWindow(DesktopFile file, ShellIconService icons, Func<DesktopFile, int, DesktopFile?> neighbour, DesktopViewModel model)
    {
        this.file = file; this.icons = icons; this.neighbour = neighbour;
        AltTab.Hide(this); Title = "Noxelyra · Önizleme"; Width = 860; Height = 620; MinWidth = 420; MinHeight = 300; WindowStartupLocation = WindowStartupLocation.CenterScreen; ShowInTaskbar = false; Topmost = true;
        SetResourceReference(BackgroundProperty, "Canvas"); SetResourceReference(ForegroundProperty, "Text"); Glass.PrepareFramed(this, model.Settings);
        var root = new DockPanel { Margin = new(14) }; Content = root;
        var head = new StackPanel { Margin = new(0, 0, 0, 10) }; head.Children.Add(name); head.Children.Add(facts); DockPanel.SetDock(head, Dock.Top); root.Children.Add(head);
        root.Children.Add(stage);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Escape or Key.Space) { Close(); e.Handled = true; }
            else if (e.Key is Key.Right or Key.Down && neighbour(this.file, 1) is { } next) { Show(next); e.Handled = true; }
            else if (e.Key is Key.Left or Key.Up && neighbour(this.file, -1) is { } before) { Show(before); e.Handled = true; }
        };
        Closed += (_, _) => StopMedia();
        Loaded += (_, _) => { Show(this.file); Activate(); Focus(); };
    }
    private void StopMedia() { if (playing is null) return; playing.Stop(); playing.Close(); playing = null; }
    private void Show(DesktopFile next)
    {
        file = next; StopMedia(); stage.Content = null;
        name.Text = next.Directory ? next.Name : next.Name + next.Extension;
        facts.Text = next.Directory ? "Klasör" : $"{NetworkMeter.Size(next.Size)} · {next.Modified:d MMMM yyyy HH:mm}";
        var type = next.Extension.ToLowerInvariant();
        try
        {
            if (next.Directory) ShowFolder(next);
            else if (ImageTypes.Contains(type) && next.Size <= MaxImage) ShowImage(next.Path);
            else if (TextTypes.Contains(type)) ShowText(next.Path);
            else if (VideoTypes.Contains(type)) ShowMedia(next.Path);
            else _ = ShowIconAsync(next.Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or InvalidOperationException or FileFormatException)
        { stage.Content = new TextBlock { Text = "Bu dosya önizlenemedi: " + ex.Message, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; }
    }
    private void ShowImage(string path)
    {
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 1600; image.UriSource = new Uri(path); image.EndInit(); image.Freeze();
        stage.Content = new Image { Source = image, Stretch = Stretch.Uniform };
    }
    private void ShowText(string path)
    {
        var bytes = new byte[MaxText]; int count;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) count = stream.Read(bytes, 0, bytes.Length);
        var text = Encoding.UTF8.GetString(bytes, 0, count);
        if (count == MaxText) text += "\n\n… dosyanın gerisi önizlemede yok.";
        var box = new TextBox { Text = text, IsReadOnly = true, IsReadOnlyCaretVisible = false, Focusable = false, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 12, BorderThickness = new(0), Background = Brushes.Transparent };
        stage.Content = box;
    }
    private void ShowMedia(string path)
    {
        playing = new MediaElement { LoadedBehavior = System.Windows.Controls.MediaState.Manual, UnloadedBehavior = System.Windows.Controls.MediaState.Close, Source = new Uri(path), Stretch = Stretch.Uniform };
        stage.Content = playing; playing.Play();
    }
    private void ShowFolder(DesktopFile folder)
    {
        var entries = Directory.EnumerateFileSystemEntries(folder.Path, "*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System }).OrderBy(e => !Directory.Exists(e)).ThenBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase).Take(201).ToArray();
        facts.Text = entries.Length > 200 ? "Klasör · 200'den fazla öğe" : $"Klasör · {entries.Length} öğe";
        var list = new StackPanel();
        foreach (var entry in entries.Take(200)) list.Children.Add(new TextBlock { Text = Path.GetFileName(entry) + (Directory.Exists(entry) ? "\\" : ""), FontSize = 12, Margin = new(0, 1, 0, 1), TextTrimming = TextTrimming.CharacterEllipsis });
        stage.Content = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false };
    }
    private async Task ShowIconAsync(string path)
    {
        var image = new Image { Width = 96, Height = 96 }; stage.Content = image;
        var asset = await icons.GetAsync(path); if (stage.Content == image) image.Source = asset.Icon;
    }
}
