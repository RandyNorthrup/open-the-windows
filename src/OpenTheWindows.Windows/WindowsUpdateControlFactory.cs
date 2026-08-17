using System.Runtime.Versioning;
using OpenTheWindows.Core.Updates;
using OpenTheWindows.Windows.Readers;
using OpenTheWindows.Windows.Writers;

namespace OpenTheWindows.Windows;

/// <summary>
/// Wires a production <see cref="UpdateControlService"/> over the real apply
/// engine and Windows adapters. Shared by the CLI's service composition so the
/// wiring exists in exactly one place.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public static class WindowsUpdateControlFactory
{
    /// <summary>
    /// Builds the update-control service. Pass a <paramref name="journalDirectory"/>
    /// / <paramref name="auditDirectory"/> to redirect the journal and audit trail
    /// (tests); leave them null to use the default ProgramData locations.
    /// </summary>
    public static UpdateControlService Create(string? journalDirectory = null, string? auditDirectory = null)
        => new(
            WindowsApplyEngineFactory.Create(journalDirectory, auditDirectory),
            new WindowsRegistryReader(),
            new WindowsCommandRunner(),
            auditDirectory is null ? new WindowsAuditSink() : new WindowsAuditSink(auditDirectory),
            new WindowsOperatingSystemInfo(),
            EndOfServicingCalendar.LoadBuiltIn(),
            TimeProvider.System);
}
