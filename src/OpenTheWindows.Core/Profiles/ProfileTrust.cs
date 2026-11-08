using System.Security.Cryptography;
using System.Text.Json;
using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Profiles;

/// <summary>
/// Decides whether a signed JSON file (a profile, or an operator catalogue file) is
/// trusted enough to use, and holds the shared trust-store verification the
/// <c>profile verify</c> command and the <see cref="ProfilePolicy.RequireSignedProfiles"/>
/// gate both rely on. A file is trusted when a detached signature
/// (<c>&lt;file&gt;.sig</c>) verifies against a key from the machine's
/// <see cref="ITrustedKeySource"/>. Callers pass the text they already read and then
/// parse that same text, so a file cannot be swapped between verification and use.
/// Built-in profiles ship inside the product and are never subject to this check.
/// </summary>
public static class ProfileTrust
{
    /// <summary>The suffix of a file's detached signature file.</summary>
    public const string SignatureSuffix = ".sig";

    /// <summary>
    /// Enforces <paramref name="policy"/> for the file at <paramref name="path"/>,
    /// whose already-read content is <paramref name="text"/>. Returns an empty list when
    /// the text may be used (the policy is off, or a valid trusted signature covers
    /// exactly this text) and one <see cref="CatalogIssueSeverity.Error"/> issue
    /// explaining why not when it may not.
    /// </summary>
    public static IReadOnlyList<CatalogIssue> Enforce(string path, string text, ProfilePolicy policy, ITrustedKeySource keys)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(keys);

        if (!policy.RequireSignedProfiles)
        {
            return [];
        }

        string signaturePath = path + SignatureSuffix;
        if (!File.Exists(signaturePath))
        {
            return [Reject(path, "unsigned-profile",
                $"policy {ProfilePolicy.RequireSignedProfilesValueName} is enabled but no signature file '{signaturePath}' accompanies this file.")];
        }

        if (!TryReadSignature(signaturePath, out ProfileSignatureDocument signature))
        {
            return [Reject(path, "unreadable-signature",
                $"signature file '{signaturePath}' is not a valid signature document.")];
        }

        return VerifyAgainstTrustStore(text, signature, keys)
            ? []
            : [Reject(path, "untrusted-profile",
                $"the signature does not verify against any key in the trust store '{keys.Description}'.")];
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="signature"/> verifies
    /// <paramref name="json"/> against some key from <paramref name="keys"/>. An empty
    /// store never verifies; an unparseable key is skipped, not fatal.
    /// </summary>
    public static bool VerifyAgainstTrustStore(string json, ProfileSignatureDocument signature, ITrustedKeySource keys)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(keys);

        foreach (string pem in keys.ReadPublicKeysPem())
        {
            using var key = ECDsa.Create();
            try
            {
                key.ImportFromPem(pem);
            }
            catch (Exception ex) when (ex is ArgumentException or CryptographicException)
            {
                continue;
            }

            if (ProfileSignature.Verify(json, signature, key))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reads a detached signature document from <paramref name="signaturePath"/>; false on missing/empty/invalid JSON.</summary>
    public static bool TryReadSignature(string signaturePath, out ProfileSignatureDocument signature)
    {
        ArgumentNullException.ThrowIfNull(signaturePath);
        signature = null!;
        try
        {
            ProfileSignatureDocument? parsed = JsonSerializer.Deserialize(
                File.ReadAllText(signaturePath), ProfileJsonContext.Default.ProfileSignatureDocument);
            if (parsed is null)
            {
                return false;
            }

            signature = parsed;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static CatalogIssue Reject(string path, string rule, string message)
        => new(CatalogIssueSeverity.Error, path, "/", rule, message);
}
