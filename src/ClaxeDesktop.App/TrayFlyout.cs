using System.Windows.Automation;
namespace ClaxeDesktop.App;

/// <summary>
/// Opens the notification area's "show hidden icons" panel with UI Automation, the documented accessibility interface.
/// Other apps' tray icons cannot be listed or redrawn by Noxelyra (Windows has no API for that), so it opens Windows' own panel.
/// </summary>
internal static class TrayFlyout
{
    internal static bool Open()
    {
        try
        {
            var taskbar = AutomationElement.RootElement.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "Shell_TrayWnd"));
            if (taskbar is null) return false;
            var chevron = taskbar.FindFirst(TreeScope.Descendants, new OrCondition(
                new PropertyCondition(AutomationElement.AutomationIdProperty, "SystemTrayIcon"),
                new PropertyCondition(AutomationElement.AutomationIdProperty, "NotifyItemIcon")));
            if (chevron is null || !chevron.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern)) return false;
            ((InvokePattern)pattern).Invoke();
            return true;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or System.Runtime.InteropServices.COMException) { return false; }
    }
}
