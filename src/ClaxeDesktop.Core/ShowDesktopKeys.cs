namespace ClaxeDesktop.Core;

public enum KeyDecision { Pass, Swallow, Trigger }

/// <summary>Decides which keystrokes belong to Win+D. Everything else, including Win alone and Win+Shift+D, passes through untouched.</summary>
public sealed class ShowDesktopKeys
{
    public const int LeftWin = 0x5B, RightWin = 0x5C, D = 0x44;
    private bool leftWin, rightWin, holdingD;
    public KeyDecision Handle(int key, bool down, bool injected, bool otherModifiers)
    {
        if (injected) return KeyDecision.Pass;
        if (key == LeftWin) { leftWin = down; return KeyDecision.Pass; }
        if (key == RightWin) { rightWin = down; return KeyDecision.Pass; }
        if (key != D) return KeyDecision.Pass;
        if (!down) { if (!holdingD) return KeyDecision.Pass; holdingD = false; return KeyDecision.Swallow; }
        if (holdingD) return KeyDecision.Swallow; // auto-repeat while held
        if (!(leftWin || rightWin) || otherModifiers) return KeyDecision.Pass;
        holdingD = true; return KeyDecision.Trigger;
    }
}
