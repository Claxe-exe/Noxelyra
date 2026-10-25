using System.Windows;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

/// <summary>Shortcuts, the low-level keyboard hook (Win key, Win+D, Alt+Tab, tiling keys) and the emergency exit.</summary>
public sealed partial class DesktopController
{
    private KeyboardHook? keyboardHook;
    private EmergencyHotkey? emergency;
    private bool emergencyExit;
    internal bool EmergencyRegistered => emergency is { Error: null };
    internal KeyboardOptions Keyboard => committed.Keyboard;
    /// <summary>The gesture in use. An untouched emergency shortcut follows the active layout so it is always Ctrl+Shift+Ü where Ü exists.</summary>
    internal HotkeyGesture Gesture(string action)
    {
        var gesture = committed.Keyboard.Gesture(action);
        if (action == "emergency" && !committed.Keyboard.IsCustom(action) && HotkeyText.KeyFor('ü') is { } key) gesture = gesture with { Key = key };
        return gesture;
    }
    internal string GestureText(string action) => HotkeyText.Format(Gesture(action));
    internal async Task<string> SetKeyboardAsync(Func<KeyboardOptions, KeyboardOptions> change)
    {
        if (safe || ReadOnly) return "Güvenli/salt okunur modda kısayollar değiştirilmez.";
        await persistenceGate.WaitAsync();
        try
        {
            var next = committed with { Keyboard = change(committed.Keyboard) }; next.Validate();
            await store.SaveAsync(next); committed = next; ViewModel.SetAdvanced(next);
        }
        catch (InvalidDataException ex) { return ex.Message; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log.Write("keyboard-save-failed", ex); return "Kaydedilemedi: " + ex.Message; }
        finally { persistenceGate.Release(); }
        return ApplyHotkeys() ?? "Kısayollar kaydedildi.";
    }
    /// <summary>(Re)registers every shortcut from the saved options. Returns the first conflict message, if any.</summary>
    private string? ApplyHotkeys()
    {
        string? problem = null;
        void Try(Action action) { try { action(); } catch (InvalidOperationException ex) { problem ??= ex.Message; Log.Write("hotkey-conflict", ex); } }
        emergency?.Dispose(); emergency = null;
        if (!exiting) { emergency = new(Gesture("emergency"), EmergencyPressed); if (emergency.Error is { } error) problem ??= error; }
        if (dockShortcut is not null) Try(() => dockShortcut.Register(Gesture("dock")));
        if (layoutShortcut is not null) Try(() => layoutShortcut.Configure(!safe && committed.LayoutShortcut, Gesture("layout")));
        if (desktopBindings is not null && !safe) Try(() => desktopBindings.Configure(committed.IntegratedDesktop, committed.GlobalShortcuts, ViewModel.PanelHeight, Gesture("launcher"), Gesture("controls")));
        ConfigureKeyboard();
        if (problem is not null) { ViewModel.Status = problem; Notify("Kısayol kullanılamıyor", problem, "warning"); }
        return problem;
    }
    private void ConfigureKeyboard()
    {
        ConfigureTiling();
        var options = committed.Keyboard;
        var showDesktop = !safe && committed.DesktopSpace.Enabled && committed.DesktopSpace.CaptureShowDesktop;
        var wanted = !safe && !exiting && (showDesktop || options.SuperLauncher || options.AltTab || options.Tiling);
        if (!wanted) { keyboardHook?.Dispose(); keyboardHook = null; return; }
        if (keyboardHook is null)
        {
            try { keyboardHook = new(action => Application.Current.Dispatcher.BeginInvoke(() => OnKeyAction(action))); Log.Write("keyboard-hook-installed"); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { Log.Write("keyboard-hook-failed", ex); ViewModel.Status = "Klavye kancası kurulamadı; Windows'un kendi kısayolları sürüyor."; return; }
        }
        var bindings = options.Tiling ? KeyboardOptions.TilingActions.ToDictionary(Gesture, a => a) : new Dictionary<HotkeyGesture, string>();
        lock (keyboardHook.Router)
        {
            keyboardHook.Router.ShowDesktop = showDesktop; keyboardHook.Router.SuperLauncher = options.SuperLauncher;
            keyboardHook.Router.AltTab = options.AltTab; keyboardHook.Router.WinBindings = bindings;
        }
    }
    private void OnKeyAction(string action)
    {
        if (exiting) return;
        if (action == "showDesktop") ToggleNoxelyraDesktop();
        else if (action == "superLauncher") ToggleLauncher();
        else if (action.StartsWith("switcher.", StringComparison.Ordinal)) Switcher(action);
        else if (action.StartsWith("tile.", StringComparison.Ordinal)) TileCommand(action);
    }
    internal void SwitcherClosed() { if (keyboardHook is not null) lock (keyboardHook.Router) keyboardHook.Router.CloseSwitcher(); }
    // Emergency: ask the UI thread for a fast clean exit; if it does not answer in 2.5 s, restore Windows from here and end the process.
    private void EmergencyPressed()
    {
        Log.Write("emergency-exit-requested");
        try { Application.Current.Dispatcher.BeginInvoke(EmergencyExit); } catch (InvalidOperationException) { }
        if (Application.Current.Dispatcher.Thread.Join(2500)) return;
        try { TaskbarWindows.SetVisible(true); } catch (Exception) { }
        try { taskbarSession.RestoreAsync(taskbarOwner).Wait(2000); } catch (Exception) { }
        try { wallpaperSession.Restore(); } catch (Exception) { }
        try { recovery.Complete(); } catch (Exception) { }
        Log.Write("emergency-exit-forced");
        Environment.Exit(3);
    }
    /// <summary>Immediate exit without the power-off animation; Windows state is still restored.</summary>
    internal void EmergencyExit() { emergencyExit = true; Exit(); }
    private void StopKeyboard() { keyboardHook?.Dispose(); keyboardHook = null; emergency?.Dispose(); emergency = null; tiling?.Dispose(); tiling = null; switcher?.Close(); keyboardWindow?.Close(); }
    private KeyboardWindow? keyboardWindow;
    public void ShowKeyboard()
    {
        if (exiting) return;
        if (keyboardWindow is null) { keyboardWindow = new(this); keyboardWindow.Closed += (_, _) => keyboardWindow = null; keyboardWindow.Show(); }
        keyboardWindow.Activate();
    }
}
