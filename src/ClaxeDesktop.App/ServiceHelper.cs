using System.Security.Principal;
using ClaxeDesktop.Core;
using ClaxeDesktop.Platform.Win;
namespace ClaxeDesktop.App;

/// <summary>Short-lived elevated process. It accepts only allow-listed services and exits; the normal app never runs elevated.</summary>
internal static class ServiceHelper
{
    internal const int Ok = 0, Invalid = 2, Partial = 3, NotElevated = 4;
    internal static int Run(string[] args)
    {
        if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) return NotElevated;
        (bool Restore, string Journal, IReadOnlyList<string> Names) request;
        try { request = ServiceTweaks.Parse(args); } catch (InvalidDataException) { return Invalid; }
        var log = new LocalLog(Path.Combine(Path.GetDirectoryName(request.Journal)!, "logs"));
        try
        {
            var session = new ServiceSession(new WindowsServiceControl(), request.Journal);
            var failed = request.Restore ? session.Restore() : session.Disable(request.Names);
            log.Write(request.Restore ? "services-restored" : "services-disabled");
            return failed.Count == 0 ? Ok : Partial;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException) { log.Write("services-helper-failed", ex); return Partial; }
    }
}
