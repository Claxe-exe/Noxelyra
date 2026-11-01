using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

/// <summary>Selection and file work on the Noxelyra desktop: everything goes through the Windows shell, so deleting lands in the Recycle Bin and conflicts ask like Explorer.</summary>
internal sealed partial class DesktopSpaceWindow
{
    private readonly HashSet<string> selection = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> cutPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Button> tiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Rectangle band = new() { StrokeThickness = 1, RadiusX = 4, RadiusY = 4, Visibility = Visibility.Collapsed, IsHitTestVisible = false };
    private Point bandStart;
    private bool banding, ownDrag, renderWaiting;
    private string? renaming, renameNext, dropTarget;
    private sealed record UndoStep(string Label, Func<Task> Run);
    private readonly Stack<UndoStep> undo = new();
    private FilePreviewWindow? preview;
    private nint Handle => new WindowInteropHelper(this).Handle;
    private double IconPixels => controller.ViewModel.Settings.DesktopSpace.IconSize;
    private double CellWidth => IconPixels + 52;
    private double CellHeight => IconPixels + 60;
    private double TileWidth => CellWidth - 8;
    private double TileHeight => CellHeight - 6;
    private string? UserDesktop => DesktopFiles.Roots.FirstOrDefault();

    private void WireFileHandling()
    {
        band.SetResourceReference(Shape.StrokeProperty, "Accent"); band.SetResourceReference(Shape.FillProperty, "Selection"); band.Opacity = .55;
        icons.Focusable = true; icons.FocusVisualStyle = null;
        icons.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource != icons) return; icons.Focus(); if (Keyboard.Modifiers != ModifierKeys.Control) { selection.Clear(); PaintSelection(); } bandStart = e.GetPosition(icons); banding = true; icons.CaptureMouse(); };
        icons.MouseMove += (_, e) =>
        {
            if (!banding) return;
            var now = e.GetPosition(icons); var area = new Rect(bandStart, now);
            if (area.Width + area.Height < 6) return;
            Canvas.SetLeft(band, area.X); Canvas.SetTop(band, area.Y); band.Width = area.Width; band.Height = area.Height; band.Visibility = Visibility.Visible;
            foreach (var (path, tile) in tiles)
            {
                var hit = area.IntersectsWith(new Rect(Canvas.GetLeft(tile), Canvas.GetTop(tile), tile.Width, tile.Height));
                if (hit) selection.Add(path); else if (Keyboard.Modifiers != ModifierKeys.Control) selection.Remove(path);
            }
            PaintSelection();
        };
        icons.MouseLeftButtonUp += (_, _) => { if (!banding) return; banding = false; band.Visibility = Visibility.Collapsed; icons.ReleaseMouseCapture(); };
        // Files dragged in from Explorer: into the folder under the pointer, else onto the desktop where they were dropped.
        AllowDrop = true;
        DragOver += (_, e) => { e.Effects = !ownDrag && e.Data.GetDataPresent(DataFormats.FileDrop) ? (Keyboard.Modifiers == ModifierKeys.Control ? DragDropEffects.Copy : DragDropEffects.Move) : DragDropEffects.None; e.Handled = true; };
        Drop += async (_, e) =>
        {
            e.Handled = true;
            if (ownDrag || e.Data.GetData(DataFormats.FileDrop) is not string[] dropped || dropped.Length == 0) return;
            var point = e.GetPosition(icons); var destination = FolderAt(point, null)?.Path ?? UserDesktop; if (destination is null) return;
            var copy = Keyboard.Modifiers == ModifierKeys.Control || !SameDrive(dropped[0], destination);
            await TransferAsync(dropped, destination, !copy);
            if (destination == UserDesktop && dropped.Length == 1) { positions[System.IO.Path.Combine(destination, System.IO.Path.GetFileName(dropped[0]))] = new(System.IO.Path.Combine(destination, System.IO.Path.GetFileName(dropped[0])), Math.Max(0, point.X - TileWidth / 2), Math.Max(headerInset, point.Y - TileHeight / 2)); saveLayout.Stop(); saveLayout.Start(); }
        };
    }
    private static bool SameDrive(string a, string b) => string.Equals(System.IO.Path.GetPathRoot(a), System.IO.Path.GetPathRoot(b), StringComparison.OrdinalIgnoreCase);
    private DesktopFile? FolderAt(Point point, IReadOnlySet<string>? except) =>
        files.FirstOrDefault(f => f.Directory && (except is null || !except.Contains(f.Path)) && tiles.TryGetValue(f.Path, out var t) && new Rect(Canvas.GetLeft(t), Canvas.GetTop(t), t.Width, t.Height).Contains(point));

    // ---- Selection ----
    /// <summary>What a command acts on: the selection, or the tile that has keyboard focus.</summary>
    private List<DesktopFile> Targets()
    {
        var chosen = files.Where(f => selection.Contains(f.Path)).ToList();
        return chosen.Count > 0 ? chosen : Keyboard.FocusedElement is Button { Tag: DesktopFile focused } ? [focused] : [];
    }
    private void Select(string path, bool toggle)
    {
        if (!toggle) selection.Clear();
        if (!selection.Add(path) && toggle) selection.Remove(path);
        PaintSelection();
    }
    private void PaintSelection()
    {
        Brush fill = Brushes.Transparent, edge = Brushes.Transparent, drop = Brushes.Transparent;
        if (selection.Count > 0 || dropTarget is not null)
        {
            if (SystemParameters.HighContrast) { fill = SystemColors.HighlightBrush; edge = SystemColors.HighlightTextBrush; drop = edge; }
            else
            {
                var accent = (Color)ColorConverter.ConvertFromString(ThemePresets.Resolve(controller.ViewModel.Settings).Accent);
                fill = new SolidColorBrush(Color.FromArgb(0x4A, accent.R, accent.G, accent.B)); edge = new SolidColorBrush(Color.FromArgb(0xB0, accent.R, accent.G, accent.B)); drop = new SolidColorBrush(accent);
            }
        }
        foreach (var (path, tile) in tiles)
        {
            var on = selection.Contains(path);
            tile.Background = on ? fill : Brushes.Transparent; tile.BorderBrush = path == dropTarget ? drop : on ? edge : Brushes.Transparent;
            tile.Opacity = cutPaths.Contains(path) ? .5 : 1;
        }
    }
    // Arrow keys walk to the nearest tile in that direction.
    private void MoveFocus(Key key)
    {
        var all = tiles.Values.ToArray(); if (all.Length == 0) return;
        if (Keyboard.FocusedElement is not Button { Tag: DesktopFile } current) { all[0].Focus(); Select(((DesktopFile)all[0].Tag).Path, false); return; }
        double x = Canvas.GetLeft(current), y = Canvas.GetTop(current);
        var next = all.Where(t => t != current).Select(t => (Tile: t, Dx: Canvas.GetLeft(t) - x, Dy: Canvas.GetTop(t) - y))
            .Where(c => key switch { Key.Left => c.Dx < -1, Key.Right => c.Dx > 1, Key.Up => c.Dy < -1, _ => c.Dy > 1 })
            .OrderBy(c => key is Key.Left or Key.Right ? Math.Abs(c.Dx) + Math.Abs(c.Dy) * 3 : Math.Abs(c.Dy) + Math.Abs(c.Dx) * 3).Select(c => c.Tile).FirstOrDefault();
        if (next is null) return;
        next.Focus(); Select(((DesktopFile)next.Tag).Path, false);
    }
    /// <summary>Delete, F2, Ctrl+A/C/X/V, Ctrl+Shift+N and arrow keys. Text boxes keep their own keys.</summary>
    private bool FileKey(KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox) return false;
        var mods = Keyboard.Modifiers;
        switch (e.Key)
        {
            case Key.Delete when mods == ModifierKeys.None: _ = RecycleAsync(); return true;
            case Key.F2 when mods == ModifierKeys.None: if (Targets() is [var one] && tiles.TryGetValue(one.Path, out var tile)) BeginRename(one, tile); return true;
            case Key.A when mods == ModifierKeys.Control: foreach (var path in tiles.Keys) selection.Add(path); PaintSelection(); return true;
            case Key.C when mods == ModifierKeys.Control: ClipFiles(false); return true;
            case Key.X when mods == ModifierKeys.Control: ClipFiles(true); return true;
            case Key.V when mods == ModifierKeys.Control: _ = PasteAsync(UserDesktop); return true;
            case Key.N when mods == (ModifierKeys.Control | ModifierKeys.Shift): _ = NewItemAsync("Yeni klasör", ""); return true;
            case Key.Z when mods == ModifierKeys.Control: _ = UndoAsync(); return true;
            case Key.Space when mods == ModifierKeys.None: if (Targets() is [var single]) ShowPreview(single); return true;
            case Key.Left or Key.Right or Key.Up or Key.Down when mods == ModifierKeys.None: MoveFocus(e.Key); return true;
            default: return false;
        }
    }

    // ---- Undo and preview ----
    private void PushUndo(string label, Func<Task> run) { undo.Push(new(label, run)); while (undo.Count > 20) { var keep = undo.ToArray(); undo.Clear(); foreach (var step in keep.Take(20).Reverse()) undo.Push(step); } }
    private async Task UndoAsync()
    {
        if (!undo.TryPop(out var step)) { status.Text = "Geri alınacak bir işlem yok."; return; }
        try { await step.Run(); status.Text = "Geri alındı: " + step.Label; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { status.Text = "Geri alınamadı: " + ex.Message; }
        await RefreshFilesAsync();
    }
    private async Task RecycleCreatedAsync(IReadOnlyList<string> paths) { if (!await ShellFiles.RecycleAsync(paths, Handle)) throw new IOException("Windows silmeyi tamamlamadı."); }
    private void ShowPreview(DesktopFile file)
    {
        preview?.Close();
        preview = new(file, shellIcons, (at, step) => { var index = files.ToList().FindIndex(f => f.Path == at.Path); var next = index + step; if (index < 0 || next < 0 || next >= files.Count) return null; Select(files[next].Path, false); return files[next]; }, controller.ViewModel) { Owner = this };
        preview.Closed += (_, _) => { preview = null; Activate(); };
        preview.Show();
    }

    // ---- Zip, shortcut, terminal ----
    private async Task ZipAsync()
    {
        var chosen = Targets(); if (chosen.Count == 0 || System.IO.Path.GetDirectoryName(chosen[0].Path) is not { } folder) return;
        var stem = chosen.Count == 1 ? chosen[0].Name : "Arşiv";
        var zip = System.IO.Path.Combine(folder, FileNameRules.Unique(stem, ".zip", n => File.Exists(System.IO.Path.Combine(folder, n))));
        status.Text = "Sıkıştırılıyor…"; var paths = chosen.Select(f => f.Path).ToArray();
        try
        {
            await Task.Run(() => ZipFiles.Create(paths, zip));
            status.Text = System.IO.Path.GetFileName(zip) + " oluşturuldu."; PushUndo("sıkıştırma", () => RecycleCreatedAsync([zip]));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { status.Text = "Sıkıştırılamadı: " + ex.Message; }
        await RefreshFilesAsync();
    }
    private async Task UnzipAsync(DesktopFile file)
    {
        if (System.IO.Path.GetDirectoryName(file.Path) is not { } parent) return;
        var target = System.IO.Path.Combine(parent, FileNameRules.Unique(file.Name, "", n => Directory.Exists(System.IO.Path.Combine(parent, n)) || File.Exists(System.IO.Path.Combine(parent, n))));
        status.Text = "Arşiv açılıyor…";
        try
        {
            await Task.Run(() => ZipFiles.Extract(file.Path, target));
            status.Text = System.IO.Path.GetFileName(target) + " klasörüne çıkarıldı."; PushUndo("arşiv açma", () => RecycleCreatedAsync([target]));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException) { status.Text = "Arşiv açılamadı: " + ex.Message; }
        await RefreshFilesAsync();
    }
    private async Task CreateShortcutAsync(DesktopFile file)
    {
        if (UserDesktop is not { } desktop) return;
        var link = System.IO.Path.Combine(desktop, FileNameRules.Unique(file.Name + " - Kısayol", ".lnk", n => File.Exists(System.IO.Path.Combine(desktop, n))));
        try { ShellFiles.CreateShortcut(file.Path, link); status.Text = System.IO.Path.GetFileNameWithoutExtension(link) + " oluşturuldu."; PushUndo("kısayol", () => RecycleCreatedAsync([link])); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException) { status.Text = "Kısayol oluşturulamadı: " + ex.Message; }
        await RefreshFilesAsync();
    }
    private void Terminal(string? folder)
    {
        if (folder is null) return;
        try { ShellFiles.OpenTerminal(folder); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { status.Text = "Terminal açılamadı: " + ex.Message; }
    }

    // ---- Commands ----
    private async Task NewItemAsync(string stem, string extension)
    {
        if (UserDesktop is not { } desktop) return;
        var name = FileNameRules.Unique(stem, extension, n => File.Exists(System.IO.Path.Combine(desktop, n)) || Directory.Exists(System.IO.Path.Combine(desktop, n)));
        var path = System.IO.Path.Combine(desktop, name);
        try { if (extension.Length == 0) Directory.CreateDirectory(path); else await File.WriteAllBytesAsync(path, []); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { status.Text = "Oluşturulamadı: " + ex.Message; return; }
        renameNext = path; selection.Clear(); selection.Add(path);
        PushUndo(extension.Length == 0 ? "yeni klasör" : "yeni dosya", () => RecycleCreatedAsync([path]));
        await RefreshFilesAsync();
    }
    private async Task RecycleAsync()
    {
        var chosen = Targets(); if (chosen.Count == 0) return;
        var moved = await ShellFiles.RecycleAsync(chosen.Select(f => f.Path), Handle);
        var gone = chosen.Where(f => !File.Exists(f.Path) && !Directory.Exists(f.Path)).Select(f => f.Path).ToArray();
        if (gone.Length > 0) PushUndo("silme", async () => { if (!await ShellFiles.RestoreAsync(gone)) throw new IOException("Geri dönüşüm kutusunda bulunamadı."); });
        foreach (var file in chosen) { if (!File.Exists(file.Path) && !Directory.Exists(file.Path)) { positions.Remove(file.Path); selection.Remove(file.Path); } }
        status.Text = moved ? (chosen.Count == 1 ? $"\"{chosen[0].Name}\" geri dönüşüm kutusuna taşındı." : $"{chosen.Count} öğe geri dönüşüm kutusuna taşındı.") : "Silme iptal edildi veya tamamlanamadı.";
        saveLayout.Stop(); saveLayout.Start();
        await RefreshFilesAsync();
    }
    private void ClipFiles(bool cut)
    {
        var chosen = Targets(); if (chosen.Count == 0) return;
        var list = new StringCollection(); foreach (var file in chosen) list.Add(file.Path);
        try
        {
            var data = new DataObject(); data.SetFileDropList(list);
            // Explorer reads this format too: 2 means move, 5 means copy.
            data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(cut ? 2 : 5)));
            Clipboard.SetDataObject(data, true);
            cutPaths.Clear(); if (cut) foreach (var file in chosen) cutPaths.Add(file.Path);
            PaintSelection();
            status.Text = (cut ? "Kesildi" : "Kopyalandı") + $": {chosen.Count} öğe.";
        }
        catch (System.Runtime.InteropServices.COMException) { status.Text = "Pano meşgul; yeniden dene."; }
    }
    private static bool ClipboardHasFiles() { try { return Clipboard.ContainsFileDropList(); } catch (System.Runtime.InteropServices.COMException) { return false; } }
    private async Task PasteAsync(string? destination)
    {
        if (destination is null) return;
        string[] paths; var move = false;
        try
        {
            if (!Clipboard.ContainsFileDropList()) { status.Text = "Panoda dosya yok."; return; }
            paths = Clipboard.GetFileDropList().Cast<string>().ToArray();
            if (Clipboard.GetData("Preferred DropEffect") is MemoryStream effect && effect.Length >= 4) { var bytes = new byte[4]; effect.Position = 0; effect.ReadExactly(bytes); move = (BitConverter.ToInt32(bytes) & 2) != 0; }
        }
        catch (System.Runtime.InteropServices.COMException) { status.Text = "Pano meşgul; yeniden dene."; return; }
        if (paths.Length == 0) return;
        if (await TransferAsync(paths, destination, move) && move) { try { Clipboard.Clear(); } catch (System.Runtime.InteropServices.COMException) { } cutPaths.Clear(); }
    }
    private async Task<bool> TransferAsync(IReadOnlyList<string> paths, string destination, bool move)
    {
        var items = paths.Where(p => !(move && string.Equals(System.IO.Path.GetDirectoryName(p), destination.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) && !string.Equals(p, destination, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (items.Length == 0) return false;
        static HashSet<string> Listing(string folder) { try { return Directory.EnumerateFileSystemEntries(folder).ToHashSet(StringComparer.OrdinalIgnoreCase); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; } }
        var before = Listing(destination);
        var done = move ? await ShellFiles.MoveAsync(items, destination, Handle) : await ShellFiles.CopyAsync(items, destination, Handle);
        // Undo needs the real results: Windows may have renamed a copy or skipped a conflict.
        if (move)
        {
            var moved = items.Select(p => (From: p, To: System.IO.Path.Combine(destination, System.IO.Path.GetFileName(p)))).Where(m => !File.Exists(m.From) && !Directory.Exists(m.From) && (File.Exists(m.To) || Directory.Exists(m.To))).ToArray();
            if (moved.Length > 0) PushUndo("taşıma", async () => { foreach (var group in moved.GroupBy(m => System.IO.Path.GetDirectoryName(m.From)!)) await ShellFiles.MoveAsync(group.Select(m => m.To).ToArray(), group.Key, Handle); });
        }
        else
        {
            var created = Listing(destination).Except(before, StringComparer.OrdinalIgnoreCase).ToArray();
            if (created.Length > 0) PushUndo("kopyalama", () => RecycleCreatedAsync(created));
        }
        status.Text = done ? $"{items.Length} öğe {(move ? "taşındı" : "kopyalandı")}." : "İşlem iptal edildi veya tamamlanamadı.";
        await RefreshFilesAsync();
        return done;
    }
    private void BeginRename(DesktopFile file, Button tile)
    {
        if (renaming is not null || tile.Content is not StackPanel { Children: [_, TextBlock label] } stack) return;
        renaming = file.Path;
        var box = new TextBox { Text = file.Name, FontSize = 12, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxHeight = 40, Padding = new(1), BorderThickness = new(1) };
        System.Windows.Automation.AutomationProperties.SetName(box, "Yeni ad");
        stack.Children.Remove(label); stack.Children.Add(box);
        var finished = false;
        async void Finish(bool commit)
        {
            if (finished) return; finished = true;
            var name = box.Text.Trim();
            stack.Children.Remove(box); stack.Children.Add(label); renaming = null;
            if (commit && name != file.Name) await RenameAsync(file, name);
            if (renderWaiting) { renderWaiting = false; RenderIcons(); }
            tiles.GetValueOrDefault(file.Path)?.Focus();
        }
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { Finish(true); e.Handled = true; }
            else if (e.Key == Key.Escape) { Finish(false); e.Handled = true; }
        };
        box.LostKeyboardFocus += (_, _) => Finish(true);
        box.Focus(); box.SelectAll();
    }
    private async Task RenameAsync(DesktopFile file, string name)
    {
        var whole = file.Directory ? name : name + file.Extension;
        if (FileNameRules.Problem(whole) is { } problem) { status.Text = problem; return; }
        var target = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(file.Path)!, whole);
        if (!await ShellFiles.RenameAsync(file.Path, target, Handle)) { status.Text = "Yeniden adlandırılamadı."; return; }
        if (positions.Remove(file.Path, out var old)) { positions[target] = old with { Path = target }; saveLayout.Stop(); saveLayout.Start(); }
        var original = file.Path; PushUndo("yeniden adlandırma", async () => { if (!await ShellFiles.RenameAsync(target, original, Handle)) throw new IOException("Eski ad geri verilemedi."); });
        if (selection.Remove(file.Path)) selection.Add(target);
        status.Text = $"Yeni ad: {name}";
        await RefreshFilesAsync();
    }
    private async Task SetIconViewAsync(string sort, int size)
    {
        var options = controller.ViewModel.Settings.DesktopSpace; var resort = sort != options.IconSort;
        if (resort) { positions.Clear(); saveLayout.Stop(); }
        if (!await controller.SetDesktopIconViewAsync(sort, size, resort)) return;
        files = DesktopFiles.Sort(files, sort); RenderIcons();
    }

    // ---- Menus ----
    private void AddFileCommands(ContextMenu menu)
    {
        var options = controller.ViewModel.Settings.DesktopSpace;
        if (undo.TryPeek(out var last)) Item(menu, $"Geri al: {last.Label}  ·  Ctrl+Z", () => _ = UndoAsync());
        var fresh = new MenuItem { Header = "Yeni" };
        Item(fresh, "Klasör  ·  Ctrl+Shift+N", () => _ = NewItemAsync("Yeni klasör", ""));
        Item(fresh, "Metin belgesi", () => _ = NewItemAsync("Yeni metin belgesi", ".txt"));
        menu.Items.Add(fresh);
        Item(menu, "Yapıştır  ·  Ctrl+V", () => _ = PasteAsync(UserDesktop), ClipboardHasFiles());
        var sort = new MenuItem { Header = "Sırala" };
        foreach (var (id, label) in new[] { ("name", "Ada göre"), ("type", "Türe göre"), ("date", "Tarihe göre"), ("size", "Boyuta göre") })
        {
            var item = new MenuItem { Header = label, IsChecked = options.IconSort == id };
            var value = id; item.Click += (_, _) => _ = SetIconViewAsync(value, options.IconSize); sort.Items.Add(item);
        }
        menu.Items.Add(sort);
        var size = new MenuItem { Header = "Simge boyutu" };
        foreach (var (pixels, label) in new[] { (36, "Küçük"), (44, "Orta"), (64, "Büyük") })
        {
            var item = new MenuItem { Header = label, IsChecked = options.IconSize == pixels };
            var value = pixels; item.Click += (_, _) => _ = SetIconViewAsync(options.IconSort, value); size.Items.Add(item);
        }
        menu.Items.Add(size);
        Item(menu, "Terminalde aç", () => Terminal(UserDesktop));
        Item(menu, "Geri dönüşüm kutusunu aç", () => { try { ShellFiles.OpenRecycleBin(); } catch (System.ComponentModel.Win32Exception ex) { status.Text = "Açılamadı: " + ex.Message; } });
        menu.Items.Add(new Separator());
    }
    private ContextMenu TileMenu(DesktopFile file, Button tile, Action open)
    {
        var chosen = Targets(); var single = chosen.Count <= 1; var menu = new ContextMenu();
        Item(menu, chosen.Count > 1 ? $"{chosen.Count} öğeyi aç" : "Aç", () => { if (single) open(); else foreach (var item in chosen) { try { DesktopFiles.Open(item); } catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException) { status.Text = "Öğe açılamadı: " + ex.Message; } } });
        Item(menu, "Önizle  ·  Boşluk", () => ShowPreview(file), single);
        if (!file.Directory) Item(menu, "Birlikte aç…", () => { if (!ShellFiles.OpenWith(file.Path, Handle)) status.Text = "Windows \"Birlikte aç\" penceresini açmadı."; }, single);
        if (single && DesktopSpaceOptions.ImageExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase)) Item(menu, "Arka plan yap", async () => { await controller.SetDesktopImageAsync(file.Path); LoadOptions(); status.Text = controller.ViewModel.Status; });
        if (single && LaunchPolicy.IsAllowed(file.Path)) Item(menu, controller.IsPinned(file.Path) ? "Dock'tan kaldır" : "Dock'a sabitle", () => controller.TogglePin(new(file.Name, file.Path)));
        menu.Items.Add(new Separator());
        Item(menu, "Kes  ·  Ctrl+X", () => ClipFiles(true));
        Item(menu, "Kopyala  ·  Ctrl+C", () => ClipFiles(false));
        if (file.Directory) Item(menu, "Buraya yapıştır", () => _ = PasteAsync(file.Path), ClipboardHasFiles());
        Item(menu, "Yeniden adlandır  ·  F2", () => BeginRename(file, tile), single);
        Item(menu, "Zip olarak sıkıştır", () => _ = ZipAsync());
        if (single && string.Equals(file.Extension, ".zip", StringComparison.OrdinalIgnoreCase)) Item(menu, "Buraya çıkar", () => _ = UnzipAsync(file));
        if (single) Item(menu, "Kısayol oluştur", () => _ = CreateShortcutAsync(file));
        if (single && file.Directory) Item(menu, "Terminalde aç", () => Terminal(file.Path));
        Item(menu, "Geri dönüşüm kutusuna at  ·  Del", () => _ = RecycleAsync());
        menu.Items.Add(new Separator());
        Item(menu, "Dosya konumunu göster", () => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe") { ArgumentList = { "/select," + file.Path }, UseShellExecute = false }); } catch (Exception ex) { status.Text = "Explorer açılamadı: " + ex.Message; } }, single);
        Item(menu, "Otomatik yerleşime al", () => { foreach (var item in chosen) positions.Remove(item.Path); saveLayout.Stop(); saveLayout.Start(); RenderIcons(); }, chosen.Any(c => positions.ContainsKey(c.Path)));
        Item(menu, "Özellikler", () => { if (!ShellFiles.Properties(file.Path, Handle)) status.Text = "Özellikler penceresi açılamadı."; }, single);
        return menu;
    }

    // ---- Dragging tiles ----
    /// <summary>Starts the real Windows drag when a tile reaches the dock, so it can be pinned or dropped on the Recycle Bin.</summary>
    private bool OverDock()
    {
        var pointer = System.Windows.Forms.Cursor.Position;
        foreach (var dock in controller.Windows.OfType<DockWindow>().Where(d => d.IsVisible))
        {
            if (WindowOps.Read(new WindowInteropHelper(dock).Handle)?.Bounds is { } b && pointer.X >= b.X && pointer.X < b.X + b.Width && pointer.Y >= b.Y && pointer.Y < b.Y + b.Height) return true;
        }
        return false;
    }
    private void DragOutside(Button tile, IEnumerable<string> paths)
    {
        ownDrag = true;
        try { DragDrop.DoDragDrop(tile, new DataObject(DataFormats.FileDrop, paths.ToArray()), DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link); }
        catch (System.Runtime.InteropServices.COMException ex) { status.Text = "Sürükleme başlatılamadı: " + ex.Message; }
        finally { ownDrag = false; }
    }
}
