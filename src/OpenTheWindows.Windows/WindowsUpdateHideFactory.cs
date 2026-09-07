using System.Runtime.Versioning;
using OpenTheWindows.Core.Updates;
using OpenTheWindows.Windows.Updates;

namespace OpenTheWindows.Windows;

/// <summary>Wires a production <see cref="IUpdateHideService"/> over the real Windows Update Agent.</summary>
[SupportedOSPlatform("windows")]
public static class WindowsUpdateHideFactory
{
    /// <summary>Builds the hide service. Pass <paramref name="auditDirectory"/> to redirect the audit trail (tests).</summary>
    public static IUpdateHideService Create(string? auditDirectory = null)
        => new WindowsUpdateHideService(
            new ComWindowsUpdateAgent(),
            auditDirectory is null ? new WindowsAuditSink() : new WindowsAuditSink(auditDirectory),
            TimeProvider.System);
}
