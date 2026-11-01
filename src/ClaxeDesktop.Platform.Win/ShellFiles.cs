using System.Diagnostics;
using System.Runtime.InteropServices;
namespace ClaxeDesktop.Platform.Win;

/// <summary>
/// File work through the Windows shell (SHFileOperation): the same progress, conflict and permission dialogs as Explorer,
/// and deleting goes to the Recycle Bin. Each call runs on its own STA thread so a long copy never freezes the interface.
/// </summary>
public static class ShellFiles
{
    private const uint Move = 1, Copy = 2, Delete = 3, RenameItem = 4;
    private const ushort RenameOnCollision = 0x8, NoConfirmation = 0x10, AllowUndo = 0x40, WantNukeWarning = 0x4000;
    public static Task<bool> MoveAsync(IEnumerable<string> paths, string folder, nint owner) => RunAsync(Move, paths, folder, AllowUndo, owner);
    /// <summary>Copies into <paramref name="folder"/>; an item copied onto itself becomes "… - Kopya" instead of asking.</summary>
    public static Task<bool> CopyAsync(IEnumerable<string> paths, string folder, nint owner)
    {
        var list = paths.ToArray();
        var same = list.Any(p => string.Equals(Path.GetDirectoryName(p), folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        return RunAsync(Copy, list, folder, (ushort)(AllowUndo | (same ? RenameOnCollision : 0)), owner);
    }
    /// <summary>Recycle Bin, with Windows' own warning when an item is too big or on a drive without one.</summary>
    public static Task<bool> RecycleAsync(IEnumerable<string> paths, nint owner) => RunAsync(Delete, paths, null, AllowUndo | NoConfirmation | WantNukeWarning, owner);
    public static Task<bool> RenameAsync(string path, string newPath, nint owner) => RunAsync(RenameItem, [path], newPath, AllowUndo, owner);
    private static Task<bool> RunAsync(uint function, IEnumerable<string> paths, string? to, ushort flags, nint owner)
    {
        var from = string.Concat(paths.Select(p => Path.GetFullPath(p) + '\0')) + '\0';
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var operation = new Operation { Owner = owner, Function = function, From = from, To = to is null ? null : to + '\0' + '\0', Flags = flags };
                var result = SHFileOperation(ref operation);
                done.SetResult(result == 0 && !operation.Aborted);
            }
            catch (Exception ex) { done.SetException(ex); }
        }) { IsBackground = true, Name = "Noxelyra shell files" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return done.Task;
    }

    /// <summary>Puts items back from the Recycle Bin by their original path; true when all of them are back.</summary>
    public static Task<bool> RestoreAsync(IReadOnlyList<string> paths)
    {
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var type = Type.GetTypeFromProgID("Shell.Application") ?? throw new InvalidOperationException();
                dynamic shell = Activator.CreateInstance(type)!; dynamic bin = shell.NameSpace(10);
                foreach (var path in paths)
                {
                    dynamic? best = null; var newest = DateTime.MinValue; var folder = Path.GetDirectoryName(path); var stem = Path.GetFileNameWithoutExtension(path); var extension = Path.GetExtension(path);
                    foreach (dynamic item in bin.Items())
                    {
                        string from = item.ExtendedProperty("System.Recycle.DeletedFrom"), name = item.Name, stored = item.Path;
                        if (!string.Equals(from?.TrimEnd('\\'), folder?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) || !string.Equals(Path.GetExtension(stored), extension, StringComparison.OrdinalIgnoreCase)) continue;
                        if (!string.Equals(name, Path.GetFileName(path), StringComparison.OrdinalIgnoreCase) && !string.Equals(name, stem, StringComparison.OrdinalIgnoreCase)) continue;
                        DateTime when = item.ExtendedProperty("System.Recycle.DateDeleted") is DateTime stamp ? stamp : DateTime.MinValue;
                        if (best is null || when >= newest) { best = item; newest = when; }
                    }
                    best?.InvokeVerb("undelete");
                }
                var wait = Stopwatch.StartNew();
                while (wait.ElapsedMilliseconds < 4000 && !paths.All(p => File.Exists(p) || Directory.Exists(p))) Thread.Sleep(50);
                done.SetResult(paths.All(p => File.Exists(p) || Directory.Exists(p)));
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or InvalidCastException) { done.SetResult(false); }
        }) { IsBackground = true, Name = "Noxelyra recycle restore" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return done.Task;
    }
    /// <summary>A Windows shortcut (.lnk) to <paramref name="target"/>.</summary>
    public static void CreateShortcut(string target, string link)
    {
        var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows Komut Dosyası Ana Bilgisayarı yok.");
        dynamic shell = Activator.CreateInstance(type)!; dynamic shortcut = shell.CreateShortcut(link);
        shortcut.TargetPath = target; shortcut.WorkingDirectory = Path.GetDirectoryName(target) ?? ""; shortcut.Save();
        Marshal.ReleaseComObject(shortcut); Marshal.ReleaseComObject(shell);
    }
    /// <summary>Windows Terminal in <paramref name="folder"/>, or PowerShell where it is not installed.</summary>
    public static void OpenTerminal(string folder)
    {
        try { Process.Start(new ProcessStartInfo("wt.exe") { ArgumentList = { "-d", folder }, UseShellExecute = true }); return; }
        catch (System.ComponentModel.Win32Exception) { }
        Process.Start(new ProcessStartInfo("powershell.exe") { WorkingDirectory = folder, UseShellExecute = true });
    }

    // ---- Recycle Bin ----
    /// <summary>Items in the Recycle Bin on every drive, or null when Windows does not say.</summary>
    public static long? RecycleBinItems()
    {
        var info = new BinInfo { Size = Marshal.SizeOf<BinInfo>() };
        return SHQueryRecycleBin(null, ref info) == 0 ? info.Items : null;
    }
    public static void OpenRecycleBin() => Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), "shell:RecycleBinFolder") { UseShellExecute = false });
    /// <summary>Empties the Recycle Bin after Windows' own confirmation dialog.</summary>
    public static void EmptyRecycleBin(nint owner) => _ = SHEmptyRecycleBin(owner, null, 0);

    // ---- Shell verbs ----
    public static bool Properties(string path, nint owner) => Verb("properties", path, owner);
    public static bool OpenWith(string path, nint owner) => Verb("openas", path, owner);
    private static bool Verb(string verb, string path, nint owner)
    {
        var info = new ExecuteInfo { Size = Marshal.SizeOf<ExecuteInfo>(), Mask = 0x0C, Owner = owner, Verb = verb, File = path, Show = 5 };
        return ShellExecuteEx(ref info);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct Operation { public nint Owner; public uint Function; public string From; public string? To; public ushort Flags; [MarshalAs(UnmanagedType.Bool)] public bool Aborted; public nint Mappings; public string? Title; }
    [StructLayout(LayoutKind.Sequential)] private struct BinInfo { public int Size; public long Bytes; public long Items; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct ExecuteInfo { public int Size; public uint Mask; public nint Owner; public string Verb; public string File; public string? Parameters; public string? Directory; public int Show; public nint Instance; public nint IdList; public string? ClassName; public nint ClassKey; public uint HotKey; public nint Icon; public nint Process; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperationW")] private static extern int SHFileOperation(ref Operation operation);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHQueryRecycleBinW")] private static extern int SHQueryRecycleBin(string? root, ref BinInfo info);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHEmptyRecycleBinW")] private static extern int SHEmptyRecycleBin(nint owner, string? root, uint flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "ShellExecuteExW", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShellExecuteEx(ref ExecuteInfo info);
}
