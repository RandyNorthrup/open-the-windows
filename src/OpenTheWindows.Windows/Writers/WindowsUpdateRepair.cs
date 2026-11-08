using System.ComponentModel;
using System.Globalization;
using System.Runtime.Versioning;
using System.ServiceProcess;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows.Writers;

/// <summary>
/// Repairs a stuck Windows Update client the supported way: the update services are
/// stopped only for the repair (their start types are never touched — that is the
/// line this product never crosses), <c>SoftwareDistribution</c> and <c>catroot2</c> are
/// renamed aside with a timestamp (never deleted), and every service that was running
/// is started again, also when a step fails. Audited as Event 4005.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsUpdateRepair : IUpdateRepair
{
    // Stop order: the orchestrator and client first, then transfer, delivery and crypto.
    private static readonly string[] Services = ["UsoSvc", "wuauserv", "BITS", "DoSvc", "CryptSvc"];
    private static readonly TimeSpan ServiceTimeout = TimeSpan.FromSeconds(60);
    private const string StampFormat = "yyyyMMddHHmmss";

    private readonly IAuditSink _audit;
    private readonly TimeProvider _time;

    /// <summary>Creates the repair over the audit sink and clock.</summary>
    public WindowsUpdateRepair(IAuditSink audit, TimeProvider time)
    {
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    private static string[] Caches { get; } =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution"),
        Path.Combine(Environment.SystemDirectory, "catroot2"),
    ];

    /// <inheritdoc />
    public UpdateRepairResult Repair(bool whatIf)
    {
        string stamp = _time.GetUtcNow().UtcDateTime.ToString(StampFormat, CultureInfo.InvariantCulture);
        if (whatIf)
        {
            return new UpdateRepairResult(true, Plan(stamp), [], null);
        }

        List<string> steps = [];
        List<string> setAside = [];
        List<string> wereRunning = [];
        string? error = null;
        try
        {
            StopAll(wereRunning, steps);
            foreach (string cache in Caches.Where(Directory.Exists))
            {
                string target = string.Create(CultureInfo.InvariantCulture, $"{cache}.otw-{stamp}");
                Directory.Move(cache, target);
                setAside.Add(target);
                steps.Add(string.Create(CultureInfo.InvariantCulture, $"Set {cache} aside as {target}."));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception
            or System.ServiceProcess.TimeoutException)
        {
            error = ex.Message;
        }
        finally
        {
            error ??= StartAll(wereRunning, steps);
        }

        _audit.Write(new AuditEvent(
            AuditEventId.UpdateRepaired,
            _time.GetUtcNow(),
            string.Create(CultureInfo.InvariantCulture, $"Windows Update repair {(error is null ? "completed" : "failed: " + error)}. Set aside: {string.Join(", ", setAside)}."),
            null,
            null));
        return new UpdateRepairResult(error is null, steps, setAside, error);
    }

    private static List<string> Plan(string stamp)
        => [
            .. Services.Select(s => string.Create(CultureInfo.InvariantCulture, $"Would stop {s} if running (start type unchanged).")),
            .. Caches.Where(Directory.Exists).Select(c => string.Create(CultureInfo.InvariantCulture, $"Would set {c} aside as {c}.otw-{stamp}.")),
            "Would start again every service that was running.",
        ];

    private static void StopAll(List<string> wereRunning, List<string> steps)
    {
        foreach (string name in Services)
        {
            using var controller = new ServiceController(name);
            if (controller.Status is ServiceControllerStatus.Stopped)
            {
                continue;
            }

            wereRunning.Add(name);
            controller.Stop();
            controller.WaitForStatus(ServiceControllerStatus.Stopped, ServiceTimeout);
            steps.Add(string.Create(CultureInfo.InvariantCulture, $"Stopped {name}."));
        }
    }

    /// <summary>Starts every service that was running (reverse order); returns the first failure, if any.</summary>
    private static string? StartAll(List<string> wereRunning, List<string> steps)
    {
        string? failure = null;
        foreach (string name in Enumerable.Reverse(wereRunning))
        {
            try
            {
                using var controller = new ServiceController(name);
                controller.Start();
                controller.WaitForStatus(ServiceControllerStatus.Running, ServiceTimeout);
                steps.Add(string.Create(CultureInfo.InvariantCulture, $"Started {name}."));
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or System.ServiceProcess.TimeoutException)
            {
                failure ??= string.Create(CultureInfo.InvariantCulture, $"Could not start {name}: {ex.Message}");
            }
        }

        return failure;
    }
}
