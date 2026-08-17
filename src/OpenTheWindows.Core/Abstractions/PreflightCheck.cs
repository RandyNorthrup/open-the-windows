namespace OpenTheWindows.Core.Abstractions;

/// <summary>The individual pre-flight conditions checked before an apply run.</summary>
public enum PreflightCheck
{
    /// <summary>The process holds an elevated administrator token.</summary>
    Elevation = 0,

    /// <summary>The OS is a supported Windows 11 client build.</summary>
    SupportedOs = 1,

    /// <summary>A reboot is already pending from earlier servicing.</summary>
    PendingReboot = 2,

    /// <summary>Windows servicing (an update install) is currently in progress.</summary>
    ServicingInProgress = 3,

    /// <summary>The machine is in the out-of-box experience (OOBE) and not ready for changes.</summary>
    Oobe = 4,

    /// <summary>The machine is running in Safe Mode.</summary>
    SafeMode = 5,
}
