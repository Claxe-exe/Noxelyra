using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using ClaxeDesktop.Core;
using ClaxeDesktop.WebWidgets;

namespace ClaxeDesktop.App;
public partial class WidgetWindow : FloatingWindow
{
    private readonly DesktopViewModel vm;
    private readonly WidgetWorkspace workspace;
    private OfflineWidgetHost? web;
    internal OfflineWidgetHost? WebHost => web;
    private readonly Grid body = new();
    private readonly Thumb grip = new();
    private DateTime calendarDay = DateTime.Today;
    public WidgetInstance Model { get; private set; }
    protected override bool CanMove => workspace.Editing && !Model.Locked;
    public WidgetWindow(DesktopViewModel vm, WidgetWorkspace workspace, WidgetInstance model) : base("Widget", Glass.Wanted(vm.Settings))
    {
        this.vm = vm; this.workspace = workspace; Model = model;
        InitializeComponent(); DataContext = vm; Frame.Padding = new(16);
        var layout = new DockPanel();
        var header = new TextBlock { Text = model.Name, FontSize = 12, Margin = new(0,0,0,12) };
        header.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); MakeDragHandle(header);
        DockPanel.SetDock(header, Dock.Top); layout.Children.Add(header);
        grip.Width = 18; grip.Height = 12; grip.HorizontalAlignment = HorizontalAlignment.Right; grip.Cursor = System.Windows.Input.Cursors.SizeNWSE; grip.ToolTip = "Düzenleme modunda boyutlandır";
        var factory = new FrameworkElementFactory(typeof(Border)); factory.SetValue(Border.CornerRadiusProperty,new CornerRadius(3)); factory.SetResourceReference(Border.BackgroundProperty,"Accent"); grip.Template = new ControlTemplate(typeof(Thumb)) { VisualTree = factory };
        DockPanel.SetDock(grip, Dock.Bottom); layout.Children.Add(grip);
        grip.DragStarted += (_, _) => workspace.BeginResize(Model.Id);
        grip.DragDelta += (_, e) => { if (CanMove) workspace.Resize(Model.Id, Width + e.HorizontalChange, Height + e.VerticalChange); };
        grip.DragCompleted += (_, _) => workspace.EndResize();
        layout.Children.Add(body); Frame.Child = layout;
        var menu = new ContextMenu(); var edit = new MenuItem { Header = "Widget'ları düzenle" }; edit.Click += (_, _) => workspace.ShowEditor(); menu.Items.Add(edit); ContextMenu = menu;
        IsVisibleChanged += (_, _) => { if (IsVisible) BuildContent(); else StopWeb(); };
        vm.PropertyChanged += ClockChanged;
        Closed += (_, _) => { vm.PropertyChanged -= ClockChanged; StopWeb(); }; Update(model);
    }
    public void Update(WidgetInstance model)
    {
        var contentChanged = Model.Text != model.Text || Model.Kind != model.Kind;
        Model = model; Title = "Noxelyra · " + model.Name; Width = model.Width; Height = model.Height;
        grip.Visibility = CanMove ? Visibility.Visible : Visibility.Collapsed;
        if (contentChanged) BuildContent();
    }
    private void ClockChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e) { if (e.PropertyName == "Time" && IsVisible && liveTick is not null) LiveTick(); if(Model.Kind=="calendar" && e.PropertyName=="Date" && calendarDay!=DateTime.Today) { calendarDay=DateTime.Today; if(IsVisible)BuildContent(); } }
    public void ShowSelection(bool selected)
    {
        if(selected) { Frame.SetResourceReference(Border.BorderBrushProperty,"Accent"); Frame.BorderThickness = new(2); }
    }
    private void StopWeb() { web?.Dispose(); web = null; StopLive(); }
    private void BuildContent()
    {
        StopWeb(); body.Children.Clear(); var stack = new StackPanel(); body.Children.Add(new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        if (Model.Kind is "system" or "clock")
        {
            stack.Children.Add(Text("Date", 11, "Muted")); stack.Children.Add(Text("Time", 46)); stack.Children.Add(Text("Greeting", 17));
        }
        if (Model.Kind == "system")
        {
            stack.Children.Add(new TextBlock { Text = "Sistem durumu", FontSize = 16, Margin = new(0,18,0,12) });
            AddMetric(stack, "İşlemci", "CpuText", "CpuValue", "Accent"); AddMetric(stack, "Bellek", "MemoryText", "MemoryValue", "Secondary"); stack.Children.Add(Text("MemoryDetail", 11, "Muted"));
        }
        if (Model.Kind == "calendar")
        {
            stack.Children.Add(Text("Date", 17));
            var today = DateTime.Today; var first = new DateTime(today.Year, today.Month, 1);
            var grid = new UniformGrid { Columns = 7, Margin = new(0,12,0,0) };
            foreach (var day in new[] { "Pt", "Sa", "Ça", "Pe", "Cu", "Ct", "Pa" }) grid.Children.Add(new TextBlock { Text = day, Margin = new(2), Opacity = .6 });
            for (int i = 0; i < ((int)first.DayOfWeek + 6) % 7; i++) grid.Children.Add(new TextBlock());
            for (int i = 1; i <= DateTime.DaysInMonth(today.Year, today.Month); i++) { var label = new TextBlock { Text = i.ToString(), Margin = new(3), FontWeight = i == today.Day ? FontWeights.Bold : FontWeights.Normal }; if (i == today.Day) label.SetResourceReference(TextBlock.ForegroundProperty, "Accent"); grid.Children.Add(label); }
            stack.Children.Add(grid);
        }
        if (IsLive(Model.Kind)) { if (IsVisible) BuildLive(stack); return; }
        if (Model.Kind == "note") stack.Children.Add(new TextBlock { Text = Model.Text, TextWrapping = TextWrapping.Wrap, FontSize = 17 });
        if (Model.Kind != "web") return;
        if (workspace.SafeMode) { stack.Children.Add(new TextBlock { Text = "Güvenli modda web widget kapalı.", TextWrapping = TextWrapping.Wrap }); return; }
        if (!IsVisible) return;
        try
        {
            var manifest = workspace.Packages.Read(Model.WebPackageId!);
            web = new(); body.Children.Clear(); body.Children.Add(web);
            web.Failed += message =>
            {
                workspace.WebStatus(message);
                StopWeb(); body.Children.Clear(); var failure = new StackPanel(); body.Children.Add(failure);
                failure.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
                var retry = new Button { Content = "Yeniden başlat", Margin = new(0,8,0,0) }; retry.Click += (_, _) => BuildContent(); failure.Children.Add(retry);
                var hide = new Button { Content = "Widget'ı gizle" }; hide.Click += (_, _) => { workspace.ShowEditor(); workspace.Change(Model.Id, w => w with { Visible = false }); }; failure.Children.Add(hide);
            };
            _ = web.StartAsync(workspace.Packages.DirectoryFor(Model.WebPackageId!), manifest.Entry, workspace.UserData(Model.Id), () => { var p = ThemePresets.Resolve(vm.Settings); return new { surface = p.Surface, text = p.Text, accent = p.Accent }; });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { stack.Children.Add(new TextBlock { Text = "Paket okunamadı. Editörden kaldırıp yeniden içe aktarabilirsin.", TextWrapping = TextWrapping.Wrap }); }
    }
    private TextBlock Text(string binding, double size, string brush = "Text") { var text = new TextBlock { FontSize = size }; text.SetResourceReference(TextBlock.ForegroundProperty, brush); text.SetBinding(TextBlock.TextProperty, new Binding(binding)); return text; }
    private void AddMetric(Panel stack, string title, string textBinding, string valueBinding, string brush)
    {
        var row = new DockPanel { Margin = new(0,0,0,6) }; var value = Text(textBinding,12); DockPanel.SetDock(value,Dock.Right); row.Children.Add(value); row.Children.Add(new TextBlock { Text = title }); stack.Children.Add(row);
        var bar = new ProgressBar { Maximum = 100, Margin = new(0,0,0,12) }; bar.SetResourceReference(ProgressBar.ForegroundProperty,brush); bar.SetBinding(ProgressBar.ValueProperty,new Binding(valueBinding) { Mode = BindingMode.OneWay }); stack.Children.Add(bar);
    }
}
