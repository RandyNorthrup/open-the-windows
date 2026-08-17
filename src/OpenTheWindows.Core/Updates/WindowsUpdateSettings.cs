namespace OpenTheWindows.Core.Updates;

/// <summary>
/// Registry locations and value names Windows Update honours, taken verbatim
/// from research 03 (§5.1 pause values, §5.2 Windows Update client policies).
/// Centralised so the pause calculator, the update-control service and the
/// status dashboard all agree on the exact strings the OS reads.
/// </summary>
public static class WindowsUpdateSettings
{
    /// <summary>
    /// The Settings-app "Update UX" key
    /// (<c>HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings</c>) that the Pause
    /// button writes to (research 03 §5.1).
    /// </summary>
    public const string UxSettingsPath = @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";

    /// <summary>
    /// The Windows Update client-policy (WUfB) key
    /// (<c>HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate</c>, research 03 §5.2).
    /// </summary>
    public const string PolicyPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";

    /// <summary>Pause "start from" value (ISO-8601 UTC string).</summary>
    public const string PauseUpdatesStartTime = "PauseUpdatesStartTime";

    /// <summary>Pause "paused until" value the Settings UI shows (ISO-8601 UTC string).</summary>
    public const string PauseUpdatesExpiryTime = "PauseUpdatesExpiryTime";

    /// <summary>Feature-update pause start (ISO-8601 UTC string).</summary>
    public const string PauseFeatureUpdatesStartTime = "PauseFeatureUpdatesStartTime";

    /// <summary>Feature-update pause end (ISO-8601 UTC string).</summary>
    public const string PauseFeatureUpdatesEndTime = "PauseFeatureUpdatesEndTime";

    /// <summary>Quality-update pause start (ISO-8601 UTC string).</summary>
    public const string PauseQualityUpdatesStartTime = "PauseQualityUpdatesStartTime";

    /// <summary>Quality-update pause end (ISO-8601 UTC string).</summary>
    public const string PauseQualityUpdatesEndTime = "PauseQualityUpdatesEndTime";

    /// <summary><c>TargetReleaseVersion</c> enable flag (DWORD 1) under <see cref="PolicyPath"/>.</summary>
    public const string TargetReleaseVersion = "TargetReleaseVersion";

    /// <summary><c>TargetReleaseVersionInfo</c>: the pinned release, e.g. "24H2".</summary>
    public const string TargetReleaseVersionInfo = "TargetReleaseVersionInfo";

    /// <summary><c>ProductVersion</c>: the product paired with the pin, e.g. "Windows 11".</summary>
    public const string ProductVersion = "ProductVersion";
}
