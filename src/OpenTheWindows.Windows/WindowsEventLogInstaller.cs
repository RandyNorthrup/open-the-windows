using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows;

/// <summary>
/// Registers the Open the Windows Event Log source (<c>OTW</c>) and its dedicated
/// custom log (<c>OpenTheWindows</c>) using the Windows Event Log APIs. Creating or
/// deleting an event source writes under
/// <c>HKLM\SYSTEM\CurrentControlSet\Services\EventLog</c> and therefore needs an
/// elevated token; the caller (the CLI <c>eventlog</c> command, or the MSI's deferred
/// custom action) is responsible for holding one. The audit sink uses the same log
/// and source names, so this type is their single source of truth.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsEventLogInstaller : IEventLogInstaller
{
    /// <summary>The custom Event Log Open the Windows writes into.</summary>
    public const string DefaultLogName = "OpenTheWindows";

    /// <summary>The Event Log source audit entries are written under.</summary>
    public const string DefaultSourceName = "OTW";

    /// <inheritdoc />
    public string LogName => DefaultLogName;

    /// <inheritdoc />
    public string SourceName => DefaultSourceName;

    /// <inheritdoc />
    public bool SourceExists() => EventLog.SourceExists(DefaultSourceName);

    /// <inheritdoc />
    public bool Install()
    {
        if (EventLog.SourceExists(DefaultSourceName))
        {
            return false;
        }

        EventLog.CreateEventSource(new EventSourceCreationData(DefaultSourceName, DefaultLogName));
        return true;
    }

    /// <inheritdoc />
    public bool Uninstall()
    {
        bool removed = false;

        if (EventLog.SourceExists(DefaultSourceName))
        {
            EventLog.DeleteEventSource(DefaultSourceName);
            removed = true;
        }

        if (EventLog.Exists(DefaultLogName))
        {
            EventLog.Delete(DefaultLogName);
            removed = true;
        }

        return removed;
    }

    /// <summary>
    /// Ensures the source is registered, swallowing the elevation/access failures that occur on a
    /// first non-elevated run. Returns whether the source is available for writing afterwards. Used
    /// by the audit sink, which must degrade to its JSONL trail rather than throw. Note that on a
    /// non-elevated process <see cref="EventLog.SourceExists(string)"/> itself throws (it cannot read
    /// the Security log to be certain), so any failure here means "not available", not "absent".
    /// </summary>
    public bool TryEnsureRegistered()
    {
        try
        {
            Install();
            return true;
        }
        catch (Exception ex) when (ex is SecurityException or InvalidOperationException or Win32Exception or UnauthorizedAccessException)
        {
            // Registration (or even probing) needs elevation the first time; degrade to the JSONL trail.
            return false;
        }
    }
}
