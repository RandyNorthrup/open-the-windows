using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>Audit sink stub collecting every event for assertions.</summary>
public sealed class FakeAuditSink : IAuditSink
{
    /// <summary>Every event written, in order.</summary>
    public IList<AuditEvent> Events { get; } = [];

    /// <inheritdoc />
    public void Write(AuditEvent auditEvent)
    {
        if (FailWrites)
        {
            throw new IOException("Injected audit failure.");
        }

        Events.Add(auditEvent);
    }

    /// <summary>When set, every write throws, as an unavailable audit trail would.</summary>
    public bool FailWrites { get; set; }

    /// <summary>Returns whether an event with the given id was written.</summary>
    public bool Has(AuditEventId id) => Events.Any(e => e.Id == id);
}
