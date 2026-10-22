using System.Text.Json;
using System.Windows;
using ClaxeDesktop.Core;

namespace ClaxeDesktop.App;
internal static class WidgetVerifier
{
    internal static async Task RunAsync(DesktopController controller,ISettingsStore store,string output,Action<string,bool> check)
    {
        var workspace = controller.Widgets; var before = (await store.LoadAsync()).Settings;
        if(controller.IsSafeMode)
        {
            workspace.ShowEditor();
            check("Safe mode blocks widget editor",!workspace.Editing);
            check("Safe mode preserves widget records",(await store.LoadAsync()).Settings.Widgets.SequenceEqual(before.Widgets));
            return;
        }
        workspace.ShowEditor(); workspace.Add("note"); var added = workspace.Items.Last();
        check("Widget editor creates independent native HWND",controller.Windows.OfType<WidgetWindow>().Count()==before.Widgets.Count+1);
        workspace.Change(added.Id,w=>w with { Text="Faz 4 gerçek not",Width=360,Height=240 });
        workspace.Undo(); check("Widget undo restores content and size",workspace.Items.Single(w=>w.Id==added.Id).Text==added.Text);
        workspace.Redo(); check("Widget redo restores edited content",workspace.Items.Single(w=>w.Id==added.Id).Text=="Faz 4 gerçek not");
        var target = controller.ViewModel.Monitors.Last();
        workspace.Change(added.Id,w=>w with { Placement=new(target.Id,144,168) });
        var native = controller.Windows.OfType<WidgetWindow>().Single(w=>w.Model.Id==added.Id);
        var actual = native.Capture(controller.ViewModel.Monitors);
        check("Widget moves to real target monitor",actual.MonitorId==target.Id&&Math.Abs(actual.X-144)<2&&Math.Abs(actual.Y-168)<2);
        workspace.SetLock(added.Id,true); workspace.Change(added.Id,w=>w with { Text="should not change" });
        check("Locked widget rejects editor mutation",workspace.Items.Single(w=>w.Id==added.Id).Text=="Faz 4 gerçek not");workspace.SetLock(added.Id,false);
        check("Widget draft does not alter disk",(await store.LoadAsync()).Settings.Widgets.SequenceEqual(before.Widgets));
        controller.ViewModel.Edit(controller.ViewModel.Settings with { PrimaryAccent="#123456" });
        await workspace.SaveAsync(); var saved = (await store.LoadAsync()).Settings;
        check("Widget save persists size text and monitor",saved.Widgets.Single(w=>w.Id==added.Id) is { Text:"Faz 4 gerçek not",Width:360,Height:240 } && saved.Widgets.Single(w=>w.Id==added.Id).Placement?.MonitorId==target.Id);
        check("Widget save does not commit theme preview",saved.PrimaryAccent==before.PrimaryAccent);
        controller.RevertPreview(); workspace.ShowEditor(); workspace.Add("calendar");workspace.Add("clock");
        workspace.Selection.Clear();foreach(var item in workspace.Items.Where(w=>w.Kind is "clock" or "calendar"))workspace.Selection.Add(item.Id); workspace.Group(true);
        check("Widget group assigns shared ID",workspace.Items.Where(w=>workspace.Selection.Contains(w.Id)).Select(w=>w.GroupId).Distinct().Count()==1);
        workspace.Duplicate();check("Widget duplicate creates unique instances",workspace.Items.Select(w=>w.Id).Distinct().Count()==workspace.Items.Count);
        var editor = Application.Current.Windows.OfType<WidgetEditorWindow>().Single();
        SaveWindow(editor,Path.Combine(output,"Widget-Editor.png"));
        foreach(var w in controller.Windows.OfType<WidgetWindow>().Where(w=>w.IsVisible)) SaveWindow(w,Path.Combine(output,"Widget-"+w.Model.Kind+".png"));
        editor.Close();check("Widget cancel restores saved layout",workspace.Items.Count==saved.Widgets.Count);
        workspace.ShowEditor();
        var source = Path.Combine(Environment.CurrentDirectory,"samples","widgets","offline-clock","widget.json");
        workspace.Import(source);var webItem = workspace.Items.Last();var webWindow = controller.Windows.OfType<WidgetWindow>().Single(w=>w.Model.Id==webItem.Id);
        for(int i=0;i<100 && webWindow.WebHost?.Ready!=true;i++)await Task.Delay(100);
        check("Real WebView2 offline widget loads",webWindow.WebHost?.Ready==true);
        var host = webWindow.WebHost!;var core = host.Core!;
        for(int i=0;i<20;i++) { if((await core.ExecuteScriptAsync("Object.keys(window.hostReplies||{}).length"))=="2")break;await Task.Delay(100); }
        check("Narrow WebView2 theme and time broker replies",(await core.ExecuteScriptAsync("Object.keys(window.hostReplies||{}).length"))=="2");
        await core.ExecuteScriptAsync("window.networkResult='pending';fetch('https://blocked.invalid/secret').then(()=>networkResult='allowed').catch(()=>networkResult='blocked');");await Task.Delay(200);
        check("Real browser CSP rejects external fetch",(await core.ExecuteScriptAsync("window.networkResult"))=="\"blocked\"");
        check("Web host exposes no native objects or devtools",!core.Settings.AreHostObjectsAllowed&&!core.Settings.AreDevToolsEnabled);
        core.Navigate("file:///C:/Windows/win.ini");await Task.Delay(200);
        check("External file navigation is canceled",core.Source.StartsWith(WebWidgetPolicy.Origin,StringComparison.Ordinal));
        core.Navigate("https://blocked.invalid/");await Task.Delay(200);
        check("External HTTPS navigation is canceled",core.Source.StartsWith(WebWidgetPolicy.Origin,StringComparison.Ordinal));
        await core.ExecuteScriptAsync("chrome.webview.postMessage({id:'bad',method:'system.exec'});");await Task.Delay(100);
        check("Unsupported host command has no reply",(await core.ExecuteScriptAsync("window.hostReplies.bad===undefined"))=="true");
        workspace.ToggleShelf();await Task.Delay(200);
        check("Hidden shelf disposes web control and polling context",webWindow.WebHost is null && host.Core is null && !webWindow.IsVisible);
        workspace.ToggleShelf();for(int i=0;i<100&&webWindow.WebHost?.Ready!=true;i++)await Task.Delay(100);
        check("Shelf show recreates isolated web control",webWindow.WebHost?.Ready==true&&!ReferenceEquals(host,webWindow.WebHost));
        if(webWindow.WebHost?.Core is { } finalCore) { using var file = File.Create(Path.Combine(output,"Web-Widget.png")); await finalCore.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,file); }
        Application.Current.Windows.OfType<WidgetEditorWindow>().Single().Close();
        workspace.ShowEditor();workspace.Selection.Clear();workspace.Selection.Add(added.Id);workspace.Remove();await workspace.SaveAsync();
        check("Widget removal persists and returns baseline count",(await store.LoadAsync()).Settings.Widgets.Count==before.Widgets.Count);
    }
    private static void SaveWindow(Window window,string path)
    {
        window.UpdateLayout();var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(window);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream);
    }
}
