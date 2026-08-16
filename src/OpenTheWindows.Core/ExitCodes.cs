namespace OpenTheWindows.Core;

/// <summary>
/// Process exit codes shared by the CLI and by any automation wrapper
/// (Intune remediation scripts, SCCM, scheduled tasks). This is a contract:
/// values never change once released; new ones are appended.
/// </summary>
public static class ExitCodes
{
    /// <summary>Command completed; for <c>scan</c> the machine is compliant.</summary>
    public const int Success = 0;

    /// <summary><c>scan</c> found drift from the requested profile. Intune treats non-zero detection as "remediate".</summary>
    public const int Drift = 1;

    /// <summary>Unexpected failure; details on stderr and in the audit log.</summary>
    public const int Error = 2;

    /// <summary>The OS is not a supported Windows 11 client build (see <c>otw doctor</c>).</summary>
    public const int UnsupportedPlatform = 3;

    /// <summary>Invalid arguments, profile or catalogue input.</summary>
    public const int InvalidInput = 4;

    /// <summary>The command needs an elevated token and the process does not have one.</summary>
    public const int ElevationRequired = 5;

    /// <summary>Changes applied; a reboot is required to complete them (same value Windows Installer uses).</summary>
    public const int RebootRequired = 3010;
}
