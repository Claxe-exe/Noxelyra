using System.Runtime.InteropServices;
using ClaxeDesktop.Core;
using Microsoft.Win32;
namespace ClaxeDesktop.Platform.Win;

/// <summary>Per-user personalization values only (HKCU); never machine-wide or shell registration keys.</summary>
public sealed class WindowsLookService : IWindowsLookService
{
    public LookValue Read(string key, string name)
    {
        using var registry = Registry.CurrentUser.OpenSubKey(key);
        return registry?.GetValue(name) switch
        {
            int number => new(key, name, true, unchecked((uint)number)),
            byte[] bytes => new(key, name, true, Binary: bytes),
            _ => new(key, name, false),
        };
    }
    public void Write(LookValue value)
    {
        using var registry = Registry.CurrentUser.CreateSubKey(value.Key, true);
        if (!value.Exists) registry.DeleteValue(value.Name, false);
        else if (value.Binary is { } bytes) registry.SetValue(value.Name, bytes, RegistryValueKind.Binary);
        else registry.SetValue(value.Name, unchecked((int)value.Dword), RegistryValueKind.DWord);
    }
    // Same notification the Settings app sends, so open windows switch theme without a sign-out.
    public void Broadcast() => SendMessageTimeout(0xFFFF, 0x001A, 0, "ImmersiveColorSet", 0x0002, 1000, out _);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint SendMessageTimeout(nint window, uint message, nint wParam, string lParam, uint flags, uint timeout, out nint result);
}
