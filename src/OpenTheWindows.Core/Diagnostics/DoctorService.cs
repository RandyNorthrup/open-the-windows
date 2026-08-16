using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Core.Diagnostics;

/// <summary>
/// Pre-flight check every front end runs before offering to change anything.
/// Pure composition over the OS abstractions, so it is fully unit-testable.
/// </summary>
public sealed class DoctorService
{
    private readonly IOperatingSystemInfo _operatingSystemInfo;
    private readonly IElevationContext _elevationContext;

    public DoctorService(IOperatingSystemInfo operatingSystemInfo, IElevationContext elevationContext)
    {
        ArgumentNullException.ThrowIfNull(operatingSystemInfo);
        ArgumentNullException.ThrowIfNull(elevationContext);
        _operatingSystemInfo = operatingSystemInfo;
        _elevationContext = elevationContext;
    }

    /// <summary>Builds the report for the current machine and process.</summary>
    public SystemReport Run()
    {
        OperatingSystemFacts facts = _operatingSystemInfo.GetFacts();
        List<string> notes = [];

        SupportStatus status;
        if (facts.IsServer)
        {
            status = SupportStatus.UnsupportedServerSku;
            notes.Add("Windows Server is not supported. This product targets Windows 11 client editions only.");
        }
        else if (facts.Build < OperatingSystemFacts.FirstWindows11Build)
        {
            status = SupportStatus.UnsupportedWindowsVersion;
            notes.Add(
                $"Windows build {facts.FullBuild} is older than Windows 11 (build {OperatingSystemFacts.FirstWindows11Build}). " +
                "Upgrade to Windows 11 to use this product.");
        }
        else
        {
            status = SupportStatus.Supported;
        }

        if (!_elevationContext.IsElevated)
        {
            notes.Add("Not running elevated. Read-only operations (scan, report) work; apply and revert require administrator rights.");
        }

        return new SystemReport(
            AppInfo.Version,
            facts,
            _elevationContext.IsElevated,
            _elevationContext.ProcessUserName,
            status,
            notes);
    }
}
