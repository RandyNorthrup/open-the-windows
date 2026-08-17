using System.Text.Json.Serialization;

namespace OpenTheWindows.Core.Profiles;

/// <summary>
/// A detached profile signature (the content of a <c>&lt;profile&gt;.sig</c> file).
/// </summary>
/// <param name="Algorithm">Signature algorithm; always <c>ES256</c> (ECDSA P-256 / SHA-256).</param>
/// <param name="KeyId">Lower-case hex SHA-256 of the signer's SubjectPublicKeyInfo (identifies the public key).</param>
/// <param name="Signature">Base64 of the DER-encoded ECDSA signature over the canonical profile bytes.</param>
public sealed record ProfileSignatureDocument(
    [property: JsonPropertyName("alg")] string Algorithm,
    [property: JsonPropertyName("keyId")] string KeyId,
    [property: JsonPropertyName("signature")] string Signature);
