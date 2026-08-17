using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenTheWindows.Core.Profiles;

/// <summary>
/// Detached ES256 (ECDSA P-256 / SHA-256) signing and verification for profile
/// files (decision D11: .NET 10 has no standalone Ed25519). Signatures are taken
/// over a <em>canonical</em> form of the profile JSON — object keys sorted by
/// ordinal, no insignificant whitespace, comments and trailing commas removed —
/// so a signature survives pretty-printing or re-ordering that does not change
/// the document's meaning. The signer's identity is a <c>keyId</c>: the lower-case
/// hex SHA-256 of its SubjectPublicKeyInfo.
/// </summary>
public static class ProfileSignature
{
    /// <summary>The only supported algorithm identifier.</summary>
    public const string Algorithm = "ES256";

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Signs <paramref name="profileJson"/> with <paramref name="privateKey"/> (must hold the private key).</summary>
    public static ProfileSignatureDocument Sign(string profileJson, ECDsa privateKey)
    {
        ArgumentNullException.ThrowIfNull(profileJson);
        ArgumentNullException.ThrowIfNull(privateKey);

        byte[] canonical = Canonicalize(profileJson);
        byte[] signature = privateKey.SignData(canonical, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        return new ProfileSignatureDocument(Algorithm, ComputeKeyId(privateKey), Convert.ToBase64String(signature));
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="signature"/> is an
    /// <c>ES256</c> signature over <paramref name="profileJson"/> that was made
    /// with the private key matching <paramref name="publicKey"/>. Never throws for
    /// malformed input — a bad algorithm, key-id mismatch or unparseable signature
    /// simply returns <see langword="false"/>.
    /// </summary>
    public static bool Verify(string profileJson, ProfileSignatureDocument signature, ECDsa publicKey)
    {
        ArgumentNullException.ThrowIfNull(profileJson);
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(publicKey);

        if (!string.Equals(signature.Algorithm, Algorithm, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(signature.KeyId, ComputeKeyId(publicKey), StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryDecodeBase64(signature.Signature, out byte[] raw))
        {
            return false;
        }

        byte[] canonical = Canonicalize(profileJson);
        return publicKey.VerifyData(canonical, raw, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
    }

    /// <summary>
    /// Verifies <paramref name="signature"/> against the first trusted key whose
    /// key-id matches; returns <see langword="true"/> only when that key verifies
    /// the signature. An empty trust set never verifies.
    /// </summary>
    public static bool VerifyAgainstTrustStore(string profileJson, ProfileSignatureDocument signature, IEnumerable<ECDsa> trustedKeys)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(trustedKeys);

        ECDsa? match = trustedKeys.FirstOrDefault(
            key => string.Equals(signature.KeyId, ComputeKeyId(key), StringComparison.Ordinal));
        return match is not null && Verify(profileJson, signature, match);
    }

    /// <summary>The key-id (lower-case hex SHA-256 of the SubjectPublicKeyInfo) of a key.</summary>
    public static string ComputeKeyId(ECDsa key)
    {
        ArgumentNullException.ThrowIfNull(key);
        byte[] spki = key.ExportSubjectPublicKeyInfo();
        return Convert.ToHexStringLower(SHA256.HashData(spki));
    }

    /// <summary>The canonical UTF-8 bytes a signature is taken over (sorted keys, minified).</summary>
    public static byte[] Canonicalize(string profileJson)
    {
        ArgumentNullException.ThrowIfNull(profileJson);
        var node = JsonNode.Parse(profileJson, documentOptions: DocumentOptions);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonical(node, writer);
        }

        return stream.ToArray();
    }

    private static void WriteCanonical(JsonNode? node, Utf8JsonWriter writer)
    {
        switch (node)
        {
            case null:
                writer.WriteNullValue();
                break;
            case JsonObject obj:
                writer.WriteStartObject();
                foreach (KeyValuePair<string, JsonNode?> property in obj.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Key);
                    WriteCanonical(property.Value, writer);
                }

                writer.WriteEndObject();
                break;
            case JsonArray array:
                writer.WriteStartArray();
                foreach (JsonNode? item in array)
                {
                    WriteCanonical(item, writer);
                }

                writer.WriteEndArray();
                break;
            default:
                // A scalar (string / number / bool): re-emit its JSON value verbatim.
                node.WriteTo(writer);
                break;
        }
    }

    private static bool TryDecodeBase64(string value, out byte[] bytes)
    {
        byte[] buffer = new byte[(value.Length + 3) / 4 * 3];
        if (Convert.TryFromBase64String(value, buffer, out int written))
        {
            bytes = buffer[..written];
            return true;
        }

        bytes = [];
        return false;
    }
}
