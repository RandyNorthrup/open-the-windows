using System.Security.Cryptography;
using System.Text.Json;
using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.Core.Audit;

/// <summary>
/// Writes an audit report as indented JSON wrapped in a <see cref="SignedAuditReport"/>:
/// the report, its integrity hash, and — when a signing key is supplied — an ES256
/// signature. Source-generated (trim/AOT safe).
/// </summary>
public sealed class AuditJsonWriter(ECDsa? signingKey = null) : IAuditReportWriter
{
    /// <inheritdoc />
    public string Extension => "json";

    /// <inheritdoc />
    public void Write(AuditReport report, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(destination);

        string reportJson = JsonSerializer.Serialize(report, AuditReportJsonContext.Default.AuditReport);
        string hash = AuditReportIntegrity.ComputeHash(report);
        ProfileSignatureDocument? signature = signingKey is null ? null : ProfileSignature.Sign(reportJson, signingKey);

        var signed = new SignedAuditReport(report, hash, signature);
        using var writer = new Utf8JsonWriter(destination, new JsonWriterOptions { Indented = true });
        JsonSerializer.Serialize(writer, signed, AuditReportJsonContext.Default.SignedAuditReport);
    }
}
