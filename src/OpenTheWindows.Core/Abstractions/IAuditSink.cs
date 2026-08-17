namespace OpenTheWindows.Core.Abstractions;

/// <summary>Receives audit events. Implementations write to the Windows Event Log and to a monthly JSONL file.</summary>
public interface IAuditSink
{
    /// <summary>Writes one audit event.</summary>
    void Write(AuditEvent auditEvent);
}
