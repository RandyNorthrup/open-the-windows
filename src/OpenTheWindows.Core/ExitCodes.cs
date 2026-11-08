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

    /// <summary>
    /// Nothing was changed because the setting is managed by MDM or Group Policy (for
    /// example a Windows Update pause on a policy-managed machine). Distinct from success
    /// so automation never mistakes "skipped" for "done".
    /// </summary>
    public const int Managed = 6;

    /// <summary>Changes applied; a reboot is required to complete them (same value Windows Installer uses).</summary>
    public const int RebootRequired = 3010;

    /// <summary>The exit-code contract in one line, for <c>--help</c>.</summary>
    public const string Summary =
        "Exit codes: 0 success (scan: compliant), 1 drift found, 2 error, 3 unsupported OS, 4 invalid input, "
        + "5 elevation required, 6 managed by policy (nothing changed), 3010 reboot required.";
}
