using System.Security.Cryptography;
using System.Text.Json;
using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.Core.Audit;

/// <summary>
/// Integrity for an <see cref="AuditReport"/>: the SHA-256 of its canonical JSON,
/// and verification of a <see cref="SignedAuditReport"/>'s hash and optional
/// ES256 signature. The canonical form (sorted keys, minified) is the same one the
/// profile signer uses, so a signature survives pretty-printing or key re-ordering.
/// </summary>
public static class AuditReportIntegrity
{
    /// <summary>The lower-case hex SHA-256 of the canonical JSON of <paramref name="report"/>.</summary>
    public static string ComputeHash(AuditReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        string json = JsonSerializer.Serialize(report, AuditReportJsonContext.Default.AuditReport);
        return Convert.ToHexStringLower(SHA256.HashData(ProfileSignature.Canonicalize(json)));
    }

    /// <summary>
    /// Verifies <paramref name="signedReport"/>'s hash; when <paramref name="publicKey"/>
    /// is supplied it also requires a valid ES256 signature from that key. Returns
    /// <see langword="false"/> for any mismatch; never throws for bad input.
    /// </summary>
    public static bool Verify(SignedAuditReport signedReport, ECDsa? publicKey)
    {
        ArgumentNullException.ThrowIfNull(signedReport);
        string json = JsonSerializer.Serialize(signedReport.Report, AuditReportJsonContext.Default.AuditReport);
        byte[] canonical = ProfileSignature.Canonicalize(json);
        string hash = Convert.ToHexStringLower(SHA256.HashData(canonical));
        if (!string.Equals(hash, signedReport.Hash, StringComparison.Ordinal))
        {
            return false;
        }

        if (publicKey is null)
        {
            return true;
        }

        return signedReport.Signature is not null && ProfileSignature.Verify(json, signedReport.Signature, publicKey);
    }
}
