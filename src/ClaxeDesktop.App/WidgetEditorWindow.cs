using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ClaxeDesktop.Core;
using Microsoft.Win32;

namespace ClaxeDesktop.App;
public sealed class WidgetEditorWindow : Window
{
    private readonly WidgetWorkspace workspace;
    private readonly ListBox list = new() { SelectionMode = SelectionMode.Extended, Height = 160 };
    private readonly TextBox note = new() { AcceptsReturn = true, Height = 85, MaxLength = 4000, TextWrapping = TextWrapping.Wrap };
    private readonly TextBox width = new() { Width = 80 };
    private readonly TextBox height = new() { Width = 80 };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0,12,0,12) };
    private bool refreshing;
    private readonly Canvas preview = new() { Width = 660, Height = 400, ClipToBounds = true };
    private readonly ComboBox monitor = new() { MinWidth = 220 };
    private readonly TextBox xPosition = new() { Width = 80 };
    private readonly TextBox yPosition = new() { Width = 80 };
    private bool showGrid = true;
    // Dragging a card in the preview moves the real widget there.
    private Border? dragCard;
    private Point dragStart, dragOrigin;
    private bool dragMoved;
    public WidgetEditorWindow(WidgetWorkspace workspace)
    {
        this.workspace = workspace; Title = "Noxelyra · Widget atölyesi"; Width = 1240; Height = 850; MinWidth = 1000; MinHeight = 650; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty,"Surface"); SetResourceReference(ForegroundProperty,"Text");
        var root = new Grid { Margin = new(24) }; root.ColumnDefinitions.Add(new()); root.ColumnDefinitions.Add(new() { Width = new GridLength(430) }); Content = root;
        var stage = new StackPanel { Margin = new(0,0,24,0) }; root.Children.Add(stage);
        stage.Children.Add(new TextBlock { Text = "Düzenleme modu", FontSize = 28, FontWeight = FontWeights.SemiBold });
        stage.Children.Add(new TextBlock { Text = "Canlı pencere düzeni · ekran başına güvenli alan", Margin = new(0,8,0,18) });
        monitor.ItemsSource = workspace.Monitors; monitor.SelectedIndex = 0; stage.Children.Add(monitor); monitor.SelectionChanged += (_,_) => DrawPreview();
        var gridToggle = new CheckBox { Content = "Izgarayı göster", IsChecked = true, Margin = new(0,16,0,8) }; gridToggle.Click += (_,_) => { showGrid = gridToggle.IsChecked == true; DrawPreview(); }; stage.Children.Add(gridToggle);
        var snap = new CheckBox { Content = "Bu oturumda ızgaraya yapıştır", IsChecked = workspace.Snap, Margin = new(0,0,0,18) }; snap.Click += (_,_) => workspace.Snap = snap.IsChecked == true; stage.Children.Add(snap);
        var border = new Border { BorderThickness = new(1), CornerRadius = new(16), Padding = new(12) }; border.SetResourceReference(Border.BorderBrushProperty,"Line"); border.SetResourceReference(Border.BackgroundProperty,"Canvas"); border.Child = new Viewbox { Child = preview }; stage.Children.Add(border);
        stage.Children.Add(new TextBlock { Text = "Kartlar gerçek masaüstü pencereleridir. Başlıklarından sürükleyebilir veya sağdaki konum alanlarını kullanabilirsin.\n\nWeb paketleri: yerel dosyalar, ağ/cihaz izni yok. Sadece güvendiğin paketleri ekle.", Margin = new(0,20,0,0), TextWrapping = TextWrapping.Wrap, Opacity = .7 });
        var stack = new StackPanel(); var scroll = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetColumn(scroll,1); root.Children.Add(scroll);
        stack.Children.Add(new TextBlock { Text = "Widget atölyesi", FontSize = 26, FontWeight = FontWeights.SemiBold });
        stack.Children.Add(new TextBlock { Text = "Başlıktan sürükle · Alt + yön tuşlarıyla taşı · Köşeden boyutlandır\nCtrl/Shift ile çoklu seç. Değişiklikler Kaydet'e kadar taslaktır.", Margin = new(0,10,0,14), TextWrapping = TextWrapping.Wrap });
        Row(stack,("+ Saat",()=>workspace.Add("clock")),("+ Takvim",()=>workspace.Add("calendar")),("+ Not",()=>workspace.Add("note")),("+ Sistem",()=>workspace.Add("system")));
        Row(stack,("+ Müzik",()=>workspace.Add("music")),("+ Hava durumu",()=>workspace.Add("weather")),("+ Sistem grafiği",()=>workspace.Add("graph")),("+ Büyük saat",()=>workspace.Add("bigclock")));
        Row(stack,("Yerel web paketi ekle",Import)); stack.Children.Add(list);
        list.SelectionChanged += (_,_) => { if (refreshing) return; workspace.Selection.Clear(); foreach (WidgetInstance item in list.SelectedItems) workspace.Selection.Add(item.Id); Populate(); workspace.RefreshSelection(); DrawPreview(); };
        Row(stack,("Çoğalt",workspace.Duplicate),("Kaldır",workspace.Remove),("Kilitle / aç",()=>Each(w=>{ workspace.SetLock(w.Id,!w.Locked); })),("Göster / gizle",()=>Each(w=>workspace.Change(w.Id,x=>x with { Visible = !x.Visible }))));
        Row(stack,("Grupla",()=>workspace.Group(true)),("Grubu çöz",()=>workspace.Group(false)),("Sola hizala",()=>workspace.Align("left")),("Üste hizala",()=>workspace.Align("top")));
        Row(stack,("Öne getir",()=>Each(w=>workspace.Change(w.Id,x=>x with { Layer = Math.Min(1000,workspace.Items.Max(i=>i.Layer)+1) }))), ("Arkaya gönder",()=>Each(w=>workspace.Change(w.Id,x=>x with { Layer = 0 }))));
        var dimensions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0,12,0,8) }; dimensions.Children.Add(new TextBlock { Text = "Genişlik / yükseklik (DIP) ", VerticalAlignment = VerticalAlignment.Center }); dimensions.Children.Add(width); dimensions.Children.Add(height); stack.Children.Add(dimensions);
        var position = new StackPanel { Orientation = Orientation.Horizontal }; position.Children.Add(new TextBlock { Text = "Konum X / Y (DIP) ", VerticalAlignment = VerticalAlignment.Center }); position.Children.Add(xPosition); position.Children.Add(yPosition); stack.Children.Add(position);
        Row(stack,("Seçili ekrana taşı",()=> { if(monitor.SelectedItem is not MonitorInfo target || !double.TryParse(xPosition.Text,out var x) || !double.TryParse(yPosition.Text,out var y)) throw new InvalidDataException("Geçerli konum ve ekran seç."); Each(w=>workspace.Change(w.Id,i=>i with { Placement = new(target.Id,x,y) })); }));
        Row(stack,("Boyutu uygula",()=> { if (!double.TryParse(width.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out var x) || !double.TryParse(height.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out var y)) throw new InvalidDataException("Sayısal boyut gir."); Each(w=>workspace.Change(w.Id,i=>i with { Width = x, Height = y })); }));
        stack.Children.Add(new TextBlock { Text = "Seçili notun metni veya hava durumu şehri", Margin = new(0,8,0,4) }); stack.Children.Add(note);
        Row(stack,("Metni uygula",()=>Each(w=> { if(w.Kind=="note") workspace.Change(w.Id,x=>x with { Text = note.Text }); else if(w.Kind=="weather") workspace.Change(w.Id,x=>x with { Text = OpenMeteo.CleanCity(note.Text) }); })));
        Row(stack,("Geri al",workspace.Undo),("Yinele",workspace.Redo));
        var footer = new StackPanel(); footer.Children.Add(status);
        var save = new Button { Content = "Düzeni kaydet", Margin = new(0,8,0,0) }; save.SetResourceReference(Button.BackgroundProperty,"Accent"); save.Click += async (_,_) => { await workspace.SaveAsync(); status.Text = workspace.Status; }; footer.Children.Add(save);
        var cancel = new Button { Content = "İptal / kayıtlı düzene dön" }; cancel.Click += (_,_) => Close(); footer.Children.Add(cancel);
        root.Children.Remove(scroll); var inspector = new DockPanel(); Grid.SetColumn(inspector,1); root.Children.Add(inspector); DockPanel.SetDock(footer,Dock.Bottom); inspector.Children.Add(footer); inspector.Children.Add(scroll);
        workspace.Changed += Refresh; Closed += (_,_) => workspace.Changed -= Refresh; Refresh();
    }
    private void Each(Action<WidgetInstance> action) { foreach(var item in workspace.Items.Where(w=>workspace.Selection.Contains(w.Id)).ToArray()) action(item); }
    private void Row(Panel parent,params (string Label,Action Action)[] buttons)
    {
        var row = new WrapPanel { Margin = new(0,5,0,0) }; parent.Children.Add(row);
        foreach(var button in buttons) { var control = new Button { Content = button.Label, Margin = new(0,0,6,4), Padding = new(9,6,9,6) }; control.Click += (_,_)=> { try { button.Action(); status.Text = "Önizleme · Kaydet veya İptal."; } catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException) { status.Text = ex.Message; } }; row.Children.Add(control); }
    }
    private void Import() { var picker = new OpenFileDialog { Title = "Yerel widget.json seç · ağ ve cihaz izinleri kapalı", Filter = "Widget manifesti|widget.json", CheckFileExists = true }; if(picker.ShowDialog(this)==true) workspace.Import(picker.FileName); }
    private void Refresh() { refreshing = true; list.ItemsSource = workspace.Items; foreach(var item in workspace.Items.Where(w=>workspace.Selection.Contains(w.Id))) list.SelectedItems.Add(item); refreshing = false; Populate(); DrawPreview(); }
    private void Populate() { var item = list.SelectedItem as WidgetInstance; if(item is null)return; width.Text = item.Width.ToString(CultureInfo.CurrentCulture); height.Text = item.Height.ToString(CultureInfo.CurrentCulture); xPosition.Text = item.Placement?.X.ToString(CultureInfo.CurrentCulture) ?? "0"; yPosition.Text = item.Placement?.Y.ToString(CultureInfo.CurrentCulture) ?? "0"; note.Text = item.Kind=="note" ? item.Text : ""; note.IsEnabled = item.Kind=="note" && !item.Locked; }
    private void DrawPreview()
    {
        preview.Children.Clear(); if(monitor.SelectedItem is not MonitorInfo screen)return;
        var scale = Math.Min(660 / (screen.Width / screen.Scale),400 / (screen.Height / screen.Scale));
        if(showGrid) for(int i=0;i<660;i+=24) { preview.Children.Add(new Line { X1=i,Y1=0,X2=i,Y2=400,Stroke=(Brush)FindResource("Line"),StrokeThickness=.5 }); }
        if(showGrid) for(int i=0;i<400;i+=24) { preview.Children.Add(new Line { X1=0,Y1=i,X2=660,Y2=i,Stroke=(Brush)FindResource("Line"),StrokeThickness=.5 }); }
        foreach(var item in workspace.Items.Where(w=>w.Visible && w.Placement?.MonitorId==screen.Id).OrderBy(w=>w.Layer))
        {
            var card = new Border { Width=item.Width*scale,Height=item.Height*scale,CornerRadius=new(8),BorderThickness=new(workspace.Selection.Contains(item.Id)?2:1), Background=(Brush)FindResource("Card"),BorderBrush=(Brush)FindResource(workspace.Selection.Contains(item.Id)?"Accent":"Line"),Child=new TextBlock { Text=item.Name+(item.Locked?" · kilitli":""),TextWrapping=TextWrapping.Wrap,Margin=new(8),FontSize=12 } };
            Canvas.SetLeft(card,item.Placement!.X*scale); Canvas.SetTop(card,item.Placement.Y*scale); preview.Children.Add(card);
            card.Cursor = item.Locked ? System.Windows.Input.Cursors.No : System.Windows.Input.Cursors.SizeAll;
            card.ToolTip = item.Locked ? "Kilitli · taşımak için önce kilidi aç" : "Sürükleyerek taşı · tıklayarak seç";
            card.MouseLeftButtonDown += (_,e)=> { dragCard = card; dragMoved = false; dragStart = e.GetPosition(preview); dragOrigin = new(Canvas.GetLeft(card), Canvas.GetTop(card)); card.CaptureMouse(); e.Handled = true; };
            card.MouseMove += (_,e)=>
            {
                if (dragCard != card || !card.IsMouseCaptured || item.Locked) return;
                var p = e.GetPosition(preview); var dx = p.X - dragStart.X; var dy = p.Y - dragStart.Y;
                if (!dragMoved && Math.Abs(dx) + Math.Abs(dy) < 3) return;
                dragMoved = true;
                Canvas.SetLeft(card, Math.Clamp(dragOrigin.X + dx, 0, Math.Max(0, preview.Width - card.Width)));
                Canvas.SetTop(card, Math.Clamp(dragOrigin.Y + dy, 0, Math.Max(0, preview.Height - card.Height)));
            };
            card.MouseLeftButtonUp += (_,_)=>
            {
                if (dragCard != card) return;
                card.ReleaseMouseCapture(); dragCard = null;
                if (!dragMoved) { list.SelectedItem = item; return; }
                var x = Math.Round(Canvas.GetLeft(card) / scale); var y = Math.Round(Canvas.GetTop(card) / scale);
                try { workspace.Change(item.Id, w => w with { Placement = new(screen.Id, x, y) }); status.Text = item.Name + " taşındı · Kaydet veya İptal."; }
                catch (Exception ex) when (ex is InvalidDataException or ArgumentException) { status.Text = ex.Message; DrawPreview(); }
            };
        }
    }
}
