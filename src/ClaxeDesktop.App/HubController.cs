using System.Windows;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;

/// <summary>First-run guide, the settings hub (one place with search) and the shortcuts and tips page.</summary>
public sealed partial class DesktopController
{
    private SettingsHubWindow? hub;
    private ShortcutsWindow? shortcutsWindow;
    private FirstRunWindow? firstRun;
    /// <summary>The settings hub: every Noxelyra area as a card, with one search box over all of them.</summary>
    public void ShowHub()
    {
        if (exiting) return;
        if (hub is null) { hub = new(this); hub.Closed += (_, _) => hub = null; hub.Show(); }
        if (hub.WindowState == WindowState.Minimized) hub.WindowState = WindowState.Normal;
        hub.Activate();
    }
    public void ShowShortcuts()
    {
        if (exiting) return;
        if (shortcutsWindow is null) { shortcutsWindow = new(this); shortcutsWindow.Closed += (_, _) => shortcutsWindow = null; shortcutsWindow.Show(); }
        shortcutsWindow.Activate();
    }
    /// <summary>The guide on first start (after the intro); <paramref name="force"/> reopens it from the hub.</summary>
    internal void ShowFirstRun(bool force = false)
    {
        if (exiting || safe || ReadOnly || (!force && committed.FirstRunComplete)) return;
        if (firstRun is null) { firstRun = new(this); firstRun.Closed += (_, _) => firstRun = null; firstRun.Show(); }
        firstRun.Activate();
    }
    internal async Task CompleteFirstRunAsync()
    {
        if (committed.FirstRunComplete || ReadOnly) return;
        await persistenceGate.WaitAsync();
        try { var next = committed with { FirstRunComplete = true }; await store.SaveAsync(next); committed = next; ViewModel.SetAdvanced(next); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { Log.Write("first-run-save-failed", ex); }
        finally { persistenceGate.Release(); }
    }
    /// <summary>Opens the classic settings window already filtered to one setting.</summary>
    internal void ShowSetting(string label)
    {
        ShowSettings();
        if (settingsWindow is not null) settingsWindow.Editor.SearchText = label;
    }
}
