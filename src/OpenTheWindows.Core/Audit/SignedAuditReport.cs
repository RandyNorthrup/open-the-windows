using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.Core.Audit;

/// <summary>
/// An <see cref="AuditReport"/> with an integrity hash and an optional signature,
/// so a fleet collector can detect tampering. <see cref="Hash"/> is the lower-case
/// hex SHA-256 of the <em>canonical</em> JSON of <see cref="Report"/> (object keys
/// sorted, no insignificant whitespace); <see cref="Signature"/>, when present, is
/// an ES256 signature over the same canonical bytes (reusing the profile-signing
/// infrastructure, decision D11).
/// </summary>
/// <param name="Report">The audit report.</param>
/// <param name="Hash">Lower-case hex SHA-256 of the canonical JSON of <paramref name="Report"/>.</param>
/// <param name="Signature">Optional ES256 signature over the same canonical bytes.</param>
public sealed record SignedAuditReport(
    AuditReport Report,
    string Hash,
    ProfileSignatureDocument? Signature);
