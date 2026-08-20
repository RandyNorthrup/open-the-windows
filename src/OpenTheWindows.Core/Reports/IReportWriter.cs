using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.Core.Reports;

/// <summary>
/// Serialises a <see cref="ScanReport"/> to a stream in one export format.
/// Implementations never close the destination stream (the caller owns it).
/// </summary>
public interface IReportWriter
{
    /// <summary>File extension for this format, without the dot (e.g. <c>json</c>).</summary>
    string Extension { get; }

    /// <summary>Writes <paramref name="report"/> to <paramref name="destination"/>.</summary>
    void Write(ScanReport report, Stream destination);
}
