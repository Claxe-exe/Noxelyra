namespace ClaxeDesktop.Core;

/// <summary>A key combination as modifiers plus a Windows virtual-key code. Stored as text such as "Ctrl+Shift+#221".</summary>
public readonly record struct HotkeyGesture(bool Ctrl, bool Alt, bool Shift, bool Win, int Key)
{
    public const int LeftWin = 0x5B, RightWin = 0x5C;
    public bool HasModifier => Ctrl || Alt || Shift || Win;
    public override string ToString() => string.Join('+', new[] { Ctrl ? "Ctrl" : null, Alt ? "Alt" : null, Shift ? "Shift" : null, Win ? "Win" : null, "#" + Key }.Where(p => p is not null));
    public static HotkeyGesture Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 40) throw new InvalidDataException("Kısayol boş veya çok uzun.");
        bool ctrl = false, alt = false, shift = false, win = false; int? key = null;
        foreach (var part in text.Split('+'))
        {
            switch (part)
            {
                case "Ctrl" when !ctrl: ctrl = true; break;
                case "Alt" when !alt: alt = true; break;
                case "Shift" when !shift: shift = true; break;
                case "Win" when !win: win = true; break;
                default:
                    if (key is not null || part.Length < 2 || part[0] != '#' || !int.TryParse(part[1..], out var code)) throw new InvalidDataException("Kısayol biçimi geçersiz: " + text);
                    key = code; break;
            }
        }
        var gesture = new HotkeyGesture(ctrl, alt, shift, win, key ?? 0);
        gesture.Validate(); return gesture;
    }
    public void Validate()
    {
        // Modifier keys alone, mouse buttons and Win itself are not usable as the main key.
        if (Key is < 0x08 or > 0xFE || Key is 0x10 or 0x11 or 0x12 or LeftWin or RightWin or (>= 0xA0 and <= 0xA5)) throw new InvalidDataException("Kısayolun ana tuşu geçersiz.");
        if (!HasModifier) throw new InvalidDataException("Kısayolda en az bir Ctrl, Alt, Shift veya Win olmalı.");
    }
}

public sealed record HotkeyBinding(string Action, string Gesture);

/// <summary>Keyboard takeovers and every Noxelyra shortcut. Win key, Alt+Tab and tiling change Windows behaviour, so they default off.</summary>
public sealed record KeyboardOptions
{
    public bool SuperLauncher { get; init; }
    public bool AltTab { get; init; }
    public bool Tiling { get; init; }
    public int TilingGap { get; init; } = 10;
    public string LauncherStyle { get; init; } = "center";
    public List<HotkeyBinding> Bindings { get; init; } = [];

    /// <summary>Shortcuts registered with RegisterHotKey (work everywhere, even over elevated windows).</summary>
    public static readonly string[] SystemActions = ["emergency", "island", "dock", "launcher", "controls", "layout"];
    /// <summary>Win-key shortcuts handled by the keyboard hook, active only while tiling is on.</summary>
    public static readonly string[] TilingActions = ["tile.float", "tile.close", "tile.fullscreen", "tile.focusLeft", "tile.focusRight", "tile.focusUp", "tile.focusDown", "tile.moveLeft", "tile.moveRight", "tile.moveUp", "tile.moveDown"];
    public static IEnumerable<string> Actions => SystemActions.Concat(TilingActions);
    /// <summary>Default gestures. The emergency key is Ctrl+Shift+Ü; on a Turkish Q keyboard Ü is VK_OEM_6, the app re-resolves it for the active layout.</summary>
    public static IReadOnlyDictionary<string, string> Defaults { get; } = new Dictionary<string, string>
    {
        ["emergency"] = "Ctrl+Shift+#221", ["island"] = "Alt+#48", ["dock"] = "Alt+#75", ["launcher"] = "Ctrl+Alt+#32", ["controls"] = "Ctrl+Alt+#81", ["layout"] = "Ctrl+Alt+#76",
        ["tile.float"] = "Win+#84", ["tile.close"] = "Win+#81", ["tile.fullscreen"] = "Win+#70",
        // Win+arrows (Snap) and Shift+Win+arrows (move to another monitor) stay with Windows.
        ["tile.focusLeft"] = "Ctrl+Alt+Win+#37", ["tile.focusUp"] = "Ctrl+Alt+Win+#38", ["tile.focusRight"] = "Ctrl+Alt+Win+#39", ["tile.focusDown"] = "Ctrl+Alt+Win+#40",
        ["tile.moveLeft"] = "Ctrl+Alt+Shift+Win+#37", ["tile.moveUp"] = "Ctrl+Alt+Shift+Win+#38", ["tile.moveRight"] = "Ctrl+Alt+Shift+Win+#39", ["tile.moveDown"] = "Ctrl+Alt+Shift+Win+#40",
    };
    public static string Label(string action) => action switch
    {
        "emergency" => "Acil çıkış (Noxelyra'yı hemen kapatır)", "island" => "Dinamik ada kartı (her yerde, oyunda da)", "dock" => "Dock'u öne getir", "launcher" => "Başlatıcı", "controls" => "Hızlı ayarlar", "layout" => "Pencere düzenleri",
        "tile.float" => "Pencereyi döşemeden çıkar / geri al", "tile.close" => "Odaktaki pencereyi kapat", "tile.fullscreen" => "Tam ekran / döşemeye dön",
        "tile.focusLeft" => "Soldaki pencereye geç", "tile.focusRight" => "Sağdaki pencereye geç", "tile.focusUp" => "Üstteki pencereye geç", "tile.focusDown" => "Alttaki pencereye geç",
        "tile.moveLeft" => "Pencereyi sola taşı", "tile.moveRight" => "Pencereyi sağa taşı", "tile.moveUp" => "Pencereyi yukarı taşı", "tile.moveDown" => "Pencereyi aşağı taşı",
        _ => action,
    };
    /// <summary>The effective gesture for an action: the user's binding or the default.</summary>
    public HotkeyGesture Gesture(string action) => HotkeyGesture.Parse(Bindings.FirstOrDefault(b => b.Action == action)?.Gesture ?? Defaults[action]);
    public bool IsCustom(string action) => Bindings.Any(b => b.Action == action);
    public KeyboardOptions With(string action, HotkeyGesture? gesture)
    {
        var list = Bindings.Where(b => b.Action != action).ToList();
        if (gesture is { } value && value.ToString() != Defaults[action]) list.Add(new(action, value.ToString()));
        var next = this with { Bindings = list }; next.Validate(); return next;
    }
    public void Validate()
    {
        if (Bindings is null || Bindings.Count > 64) throw new InvalidDataException("Kısayol listesi geçersiz.");
        if (TilingGap is < 0 or > 40 || LauncherStyle is not ("center" or "grid")) throw new InvalidDataException("Klavye/döşeme tercihi geçersiz.");
        if (Bindings.Any(b => b is null || !Defaults.ContainsKey(b.Action)) || Bindings.Select(b => b.Action).Distinct().Count() != Bindings.Count) throw new InvalidDataException("Bilinmeyen veya tekrarlanan kısayol.");
        var gestures = Actions.Select(a => (Action: a, Gesture: Gesture(a))).ToList();
        foreach (var (action, gesture) in gestures)
        {
            if (SystemActions.Contains(action) && gesture.Win) throw new InvalidDataException(Label(action) + " için Win tuşu kullanılamaz; Windows bu kısayolları ayırır.");
            if (TilingActions.Contains(action) && !gesture.Win) throw new InvalidDataException(Label(action) + " kısayolu Win tuşuyla başlamalı.");
        }
        var clash = gestures.GroupBy(g => g.Gesture).FirstOrDefault(g => g.Count() > 1);
        if (clash is not null) throw new InvalidDataException("Aynı kısayol iki işe atanmış: " + string.Join(", ", clash.Select(c => Label(c.Action))));
        // Win+D and Alt+Tab belong to their own features.
        if (gestures.Any(g => g.Gesture == new HotkeyGesture(false, false, false, true, 0x44) || g.Gesture == new HotkeyGesture(false, true, false, false, 0x09))) throw new InvalidDataException("Win+D ve Alt+Tab kendi özelliklerine ayrılmıştır.");
    }
}

public enum KeyDecision { Pass, Swallow, Trigger }

/// <summary>What the low-level keyboard hook should do with one keystroke. Pass with an Action lets the key through and still runs the action.</summary>
public readonly record struct KeyRoute(KeyDecision Decision, string? Action = null, bool MaskStart = false, bool ReplayWinUp = false);

/// <summary>
/// Routes raw keystrokes for the takeovers: Win tapped alone, Win+D, Win+&lt;tiling key&gt; and Alt+Tab.
/// Everything not claimed passes through untouched, including every other Win shortcut and Win+L.
/// </summary>
public sealed class KeyRouter
{
    public const int Tab = 0x09, Escape = 0x1B, D = 0x44, Left = 0x25, Up = 0x26, Right = 0x27, Down = 0x28, Return = 0x0D;
    public bool SuperLauncher { get; set; }
    public bool ShowDesktop { get; set; }
    public bool AltTab { get; set; }
    public IReadOnlyDictionary<HotkeyGesture, string> WinBindings { get; set; } = new Dictionary<HotkeyGesture, string>();
    private bool winDown, winUsed, switcher;
    private readonly HashSet<int> swallowed = [];
    public bool SwitcherOpen => switcher;
    public KeyRoute Route(int key, bool down, bool injected, bool ctrl, bool alt, bool shift)
    {
        if (injected) return new(KeyDecision.Pass);
        if (key is HotkeyGesture.LeftWin or HotkeyGesture.RightWin)
        {
            if (down) { if (!winDown) { winDown = true; winUsed = false; } return new(KeyDecision.Pass); }
            winDown = false;
            // A clean tap of Win opens the launcher; the Win-up is replayed after a masking key so Start stays shut.
            if (!winUsed && SuperLauncher && !ctrl && !alt && !shift) return new(KeyDecision.Trigger, "superLauncher", ReplayWinUp: true);
            return new(KeyDecision.Pass);
        }
        if (!down)
        {
            if (swallowed.Remove(key)) return new(KeyDecision.Swallow);
            // Alt-up must reach Windows (or Alt stays logically held); it only also commits the switcher.
            if (switcher && key is 0x12 or 0xA4 or 0xA5) { switcher = false; return new(KeyDecision.Pass, "switcher.commit"); }
            return new(KeyDecision.Pass);
        }
        if (winDown)
        {
            winUsed = true;
            var gesture = new HotkeyGesture(ctrl, alt, shift, true, key);
            string? action = ShowDesktop && key == D && !ctrl && !alt && !shift ? "showDesktop" : WinBindings.TryGetValue(gesture, out var bound) ? bound : null;
            if (action is null) return new(KeyDecision.Pass);
            swallowed.Add(key); return new(KeyDecision.Trigger, action, MaskStart: true);
        }
        if (switcher)
        {
            string? command = key switch { Tab => shift ? "switcher.prev" : "switcher.next", Left => "switcher.prev", Right => "switcher.next", Escape => "switcher.cancel", Return => "switcher.commit", _ => null };
            if (command is null) return new(KeyDecision.Pass);
            if (command is "switcher.cancel" or "switcher.commit") switcher = false;
            swallowed.Add(key); return new(KeyDecision.Trigger, command);
        }
        if (AltTab && key == Tab && alt && !ctrl)
        {
            switcher = true; swallowed.Add(key);
            // Mask now, while Alt is still held, so releasing Alt does not open the focused app's menu bar.
            return new(KeyDecision.Trigger, shift ? "switcher.openPrev" : "switcher.open", MaskStart: true);
        }
        return new(KeyDecision.Pass);
    }
    /// <summary>Called when the switcher closes by other means (click, focus loss).</summary>
    public void CloseSwitcher() => switcher = false;
}
