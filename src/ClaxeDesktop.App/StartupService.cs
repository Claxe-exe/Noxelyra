using System.Runtime.InteropServices;
namespace ClaxeDesktop.App;
public sealed class StartupService
{
    private readonly string folder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
    private static bool Managed(string description) => description is "Claxe Desktop managed startup" or "Noxelyra Desktop managed startup" or "Noxelyra managed startup";
    private IEnumerable<string> Paths => new[] { "Noxelyra.lnk", "Noxelyra Desktop.lnk", "Claxe Desktop.lnk" }.Select(n => Path.Combine(folder, n));
    public bool Enabled
    {
        get
        {
            dynamic? shell = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
                foreach (var path in Paths.Where(File.Exists))
                {
                    dynamic link = shell!.CreateShortcut(path);
                    try { if (Managed((string)link.Description)) return true; }
                    finally { Marshal.FinalReleaseComObject(link); }
                }
                return false;
            }
            catch { return false; }
            finally { if (shell is not null) Marshal.FinalReleaseComObject(shell); }
        }
    }
    public void SetEnabled(bool enabled)
    {
        var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows kısayol servisi bulunamadı.");
        dynamic shell = Activator.CreateInstance(type)!;
        var target = Path.Combine(folder, "Noxelyra.lnk");
        try
        {
            if (enabled)
            {
                dynamic link = shell.CreateShortcut(target);
                try
                {
                    if (File.Exists(target) && !Managed((string)link.Description)) throw new IOException("Aynı adlı farklı bir başlangıç kısayolu var.");
                    link.TargetPath = Environment.ProcessPath!; link.WorkingDirectory = AppContext.BaseDirectory; link.Description = "Noxelyra managed startup"; link.Save();
                }
                finally { Marshal.FinalReleaseComObject(link); }
            }
            foreach (var path in Paths.Where(File.Exists))
            {
                if (enabled && path == target) continue;
                dynamic old = shell.CreateShortcut(path);
                try { if (Managed((string)old.Description)) File.Delete(path); }
                finally { Marshal.FinalReleaseComObject(old); }
            }
        }
        finally { Marshal.FinalReleaseComObject(shell); }
    }
}
