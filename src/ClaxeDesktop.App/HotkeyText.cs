using System.Runtime.InteropServices;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;

/// <summary>Human-readable names for gestures in the active keyboard layout (Ü shows as Ü on a Turkish keyboard) and RegisterHotKey flags.</summary>
internal static class HotkeyText
{
    internal static string Format(HotkeyGesture gesture)
    {
        var parts = new List<string>();
        if (gesture.Ctrl) parts.Add("Ctrl"); if (gesture.Alt) parts.Add("Alt"); if (gesture.Shift) parts.Add("Shift"); if (gesture.Win) parts.Add("Win");
        parts.Add(Key(gesture.Key));
        return string.Join('+', parts);
    }
    internal static string Key(int vk) => vk switch
    {
        0x20 => "Boşluk", 0x0D => "Enter", 0x09 => "Tab", 0x1B => "Esc", 0x08 => "Geri", 0x2E => "Delete", 0x2D => "Insert",
        0x25 => "←", 0x26 => "↑", 0x27 => "→", 0x28 => "↓", 0x24 => "Home", 0x23 => "End", 0x21 => "PageUp", 0x22 => "PageDown",
        >= 0x70 and <= 0x87 => "F" + (vk - 0x6F),
        _ => MapVirtualKey((uint)vk, 2) is var c and > 0 && c < 0x10000 ? char.ToUpper((char)c, new System.Globalization.CultureInfo("tr-TR")).ToString() : "#" + vk,
    };
    /// <summary>RegisterHotKey modifier flags, always with MOD_NOREPEAT.</summary>
    internal static uint Modifiers(HotkeyGesture gesture) => 0x4000u | (gesture.Alt ? 1u : 0) | (gesture.Ctrl ? 2u : 0) | (gesture.Shift ? 4u : 0) | (gesture.Win ? 8u : 0);
    /// <summary>The virtual key that types <paramref name="character"/> in the active layout, or null when the layout lacks it.</summary>
    internal static int? KeyFor(char character) { var scan = VkKeyScan(character); return scan == -1 ? null : scan & 0xFF; }
    [DllImport("user32.dll")] private static extern uint MapVirtualKey(uint code, uint mapType);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern short VkKeyScan(char character);
}
