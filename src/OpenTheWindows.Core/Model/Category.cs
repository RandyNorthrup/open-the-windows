namespace OpenTheWindows.Core.Model;

/// <summary>
/// Top-level grouping of catalogue entries. A profile sets one <see cref="Level"/>
/// per category. The set is closed on purpose: it is the primary navigation of
/// the GUI and the primary filter of the CLI.
/// </summary>
public enum Category
{
    /// <summary>Diagnostic data, feedback, advertising ID, activity history, cloud content.</summary>
    Privacy = 0,

    /// <summary>Windows Update pause / defer / target release / driver and optional-update control.</summary>
    Updates = 1,

    /// <summary>Hardening: credential protection, network, malware defence, audit, policy.</summary>
    Security = 2,

    /// <summary>Measurable performance: services, startup, power, visual effects.</summary>
    Performance = 3,

    /// <summary>Inbox app removal, optional features, third-party stubs, Edge/OneDrive/Copilot.</summary>
    Debloat = 4,

    /// <summary>Shell and UI preferences: Explorer, taskbar, Start, notifications.</summary>
    Shell = 5,
}
