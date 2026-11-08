using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Security;
using System.Text;
using System.Text.Json;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Windows.Security;

namespace OpenTheWindows.Windows;

/// <summary>
/// Writes audit events to the Windows Event Log (log <c>OpenTheWindows</c>,
/// source <c>OTW</c>) and to a monthly JSONL file under
/// <c>%ProgramData%\OpenTheWindows\audit</c>. Event Log writing is best-effort:
/// if the source cannot be created (no elevation on first run) the JSONL trail is
/// still written, and the failure is never silent about which sink was used.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsAuditSink : IAuditSink
{
    private const string LogName = WindowsEventLogInstaller.DefaultLogName;
    private const string SourceName = WindowsEventLogInstaller.DefaultSourceName;

    private readonly string _auditDirectory;
    private readonly ISecureDirectory? _guard;

    /// <summary>
    /// Creates a sink writing to the default ProgramData audit directory, which is kept
    /// administrator-owned and administrator-writable so a standard user can neither
    /// forge audit lines nor redirect the privileged append elsewhere.
    /// </summary>
    public WindowsAuditSink()
        : this(WindowsDataPaths.Audit, new WindowsSecureDirectory())
    {
    }

    /// <summary>Creates a sink writing to <paramref name="auditDirectory"/> (a per-user trail, or tests).</summary>
    public WindowsAuditSink(string auditDirectory)
        : this(auditDirectory, null)
    {
    }

    private WindowsAuditSink(string auditDirectory, ISecureDirectory? guard)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(auditDirectory);
        _auditDirectory = auditDirectory;
        _guard = guard;
        EventLogAvailable = new WindowsEventLogInstaller().TryEnsureRegistered();
    }

    /// <summary>The per-user audit folder (<c>%LocalAppData%\OpenTheWindows\audit</c>) used by unelevated per-user runs.</summary>
    public static string UserDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenTheWindows", "audit");

    /// <summary><see langword="true"/> when the Event Log source is available for writing.</summary>
    public bool EventLogAvailable { get; }

    /// <inheritdoc />
    public void Write(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        WriteEventLog(auditEvent);
        WriteJsonl(auditEvent);
    }

    private void WriteEventLog(AuditEvent auditEvent)
    {
        if (!EventLogAvailable)
        {
            return;
        }

        try
        {
            using var log = new EventLog(LogName) { Source = SourceName };
            log.WriteEntry(auditEvent.Message, EntryType(auditEvent.Id), (int)auditEvent.Id);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or SecurityException)
        {
            // The Event Log is unavailable (full, disabled or access denied); the JSONL trail still records the event.
        }
    }

    private void WriteJsonl(AuditEvent auditEvent)
    {
        if (_guard is null)
        {
            Directory.CreateDirectory(_auditDirectory);
        }
        else
        {
            _guard.EnsureSafeForWrite(_auditDirectory);
        }

        string month = auditEvent.At.UtcDateTime.ToString("yyyyMM", CultureInfo.InvariantCulture);
        string file = Path.Combine(_auditDirectory, string.Create(CultureInfo.InvariantCulture, $"otw-{month}.jsonl"));
        string line = JsonSerializer.Serialize(auditEvent, AuditJsonContext.Default.AuditEvent);
        File.AppendAllText(file, line + "\n", Encoding.UTF8);
    }

    private static EventLogEntryType EntryType(AuditEventId id) => id switch
    {
        AuditEventId.Error => EventLogEntryType.Error,
        AuditEventId.TweakFailed or AuditEventId.RunRolledBack or AuditEventId.Rollback
            or AuditEventId.Refusal or AuditEventId.BreakGlass => EventLogEntryType.Warning,
        _ => EventLogEntryType.Information,
    };
}
