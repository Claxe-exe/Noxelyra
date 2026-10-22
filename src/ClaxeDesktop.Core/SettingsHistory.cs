namespace ClaxeDesktop.Core;
public sealed class SettingsHistory(int capacity = 50)
{
    private readonly List<Settings> undo = [];
    private readonly List<Settings> redo = [];
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public void Push(Settings current)
    {
        undo.Add(current); if (undo.Count > capacity) undo.RemoveAt(0); redo.Clear();
    }
    public Settings Undo(Settings current)
    {
        if (!CanUndo) return current;
        redo.Add(current); var previous = undo[^1]; undo.RemoveAt(undo.Count - 1); return previous;
    }
    public Settings Redo(Settings current)
    {
        if (!CanRedo) return current;
        undo.Add(current); var next = redo[^1]; redo.RemoveAt(redo.Count - 1); return next;
    }
    public void Clear() { undo.Clear(); redo.Clear(); }
}
