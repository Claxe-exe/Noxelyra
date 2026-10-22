using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Controls;
using ClaxeDesktop.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ClaxeDesktop.WebWidgets;

// This host deliberately exposes no native objects or network permissions.
public sealed class OfflineWidgetHost : Grid, IDisposable
{
    private WebView2CompositionControl? view;
    private bool disposed;
    public int BlockedRequests { get; private set; }
    internal CoreWebView2? Core => view?.CoreWebView2;
    public bool Ready { get; private set; }
    public event Action<string>? Failed;
    public async Task StartAsync(string root, string entry, string userData, Func<object> tokens)
    {
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(null, userData);
            if (disposed) return;
            view = new(); Children.Add(view);
            await view.EnsureCoreWebView2Async(environment);
            if (disposed) return;
            var core = view.CoreWebView2;
            core.NavigationCompleted += (_, e) => Ready = e.IsSuccess;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.AreDefaultScriptDialogsEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.PermissionRequested += (_, e) => { e.State = CoreWebView2PermissionState.Deny; e.Handled = true; };
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.DownloadStarting += (_, e) => e.Cancel = true;
            core.NavigationStarting += (_, e) => { if (!WebWidgetPolicy.IsLocalRequest(e.Uri, out var path) || path != entry) e.Cancel = true; };
            core.FrameNavigationStarting += (_, e) => e.Cancel = true;
            core.ProcessFailed += (_, e) => Failed?.Invoke("Widget işlemi durdu (" + e.ProcessFailedKind + "). Yeniden başlatabilir veya gizleyebilirsin.");
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All);
            core.WebResourceRequested += (_, e) =>
            {
                try
                {
                    if (e.Request.Method != "GET" || !WebWidgetPolicy.IsLocalRequest(e.Request.Uri, out var relative)) throw new InvalidDataException();
                    var file = WebWidgetPolicy.ResolveFile(root, relative);
                    var bytes = File.ReadAllBytes(file);
                    if (bytes.Length > 4 * 1024 * 1024) throw new InvalidDataException();
                    var mime = Path.GetExtension(file).ToLowerInvariant() switch { ".html" => "text/html; charset=utf-8", ".js" => "text/javascript; charset=utf-8", ".css" => "text/css; charset=utf-8", ".json" => "application/json", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", ".woff2" => "font/woff2", _ => "text/plain" };
                    const string csp = "default-src 'none'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'none'; frame-src 'none'; worker-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'";
                    e.Response = environment.CreateWebResourceResponse(new MemoryStream(bytes), 200, "OK", $"Content-Type: {mime}\r\nContent-Security-Policy: {csp}\r\nX-Content-Type-Options: nosniff\r\nCache-Control: no-store");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                { BlockedRequests++; e.Response = environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes("Blocked")), 403, "Blocked", "Content-Type: text/plain"); }
            };
            core.WebMessageReceived += (_, e) =>
            {
                try
                {
                    var request = WebWidgetPolicy.ParseMessage(e.Source, e.WebMessageAsJson);
                    object result = request.Method == "theme.getTokens" ? tokens() : new { local = DateTimeOffset.Now.ToString("O") };
                    core.PostWebMessageAsJson(JsonSerializer.Serialize(new { id = request.Id, result }));
                }
                catch (Exception ex) when (ex is InvalidDataException or ArgumentException) { /* Invalid messages have no host effect. */ }
            };
            core.Navigate(WebWidgetPolicy.Origin + "/" + entry);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { if (!disposed) Failed?.Invoke("Yerel web widget başlatılamadı. WebView2 Runtime ve paketi kontrol et."); }
    }
    public void Dispose()
    {
        disposed = true; Ready = false; var previous = view; view = null;
        try { previous?.Dispose(); }
        catch(Exception ex) when(ex is InvalidOperationException or System.Runtime.InteropServices.COMException or FileNotFoundException) { }
        finally { Children.Clear(); }
    }
}
