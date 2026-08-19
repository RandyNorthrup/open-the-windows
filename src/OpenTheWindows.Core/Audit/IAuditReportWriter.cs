namespace OpenTheWindows.Core.Audit;

/// <summary>
/// Serialises an <see cref="AuditReport"/> to a stream in one export format.
/// Implementations never close the destination stream (the caller owns it).
/// </summary>
public interface IAuditReportWriter
{
    /// <summary>File extension for this format, without the dot (e.g. <c>json</c>).</summary>
    string Extension { get; }

    /// <summary>Writes <paramref name="report"/> to <paramref name="destination"/>.</summary>
    void Write(AuditReport report, Stream destination);
}
