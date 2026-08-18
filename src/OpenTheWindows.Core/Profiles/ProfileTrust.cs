using System.Security.Cryptography;
using System.Text.Json;
using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Profiles;

/// <summary>
/// Decides whether a profile <em>file</em> is trusted enough to use, and holds the
/// shared trust-store verification the <c>profile verify</c> command and the
/// <see cref="ProfilePolicy.RequireSignedProfiles"/> gate both rely on. A file is
/// trusted when a detached signature (<c>&lt;file&gt;.sig</c>) verifies against a
/// public key pinned in the machine trust store. Built-in profiles ship inside the
/// product and are never subject to this check.
/// </summary>
public static class ProfileTrust
{
    /// <summary>The suffix of a profile's detached signature file.</summary>
    public const string SignatureSuffix = ".sig";

    /// <summary>
    /// The machine trust store: every <c>*.pem</c> public key here is a signer the
    /// machine trusts (<c>%ProgramData%\OpenTheWindows\trusted-keys</c>).
    /// </summary>
    public static string DefaultTrustStoreDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "OpenTheWindows",
        "trusted-keys");

    /// <summary>
    /// Enforces <paramref name="policy"/> for the profile file at
    /// <paramref name="profilePath"/>. Returns an empty list when the file may be
    /// used (the policy is off, or the file carries a valid trusted signature) and
    /// one <see cref="CatalogIssueSeverity.Error"/> issue explaining why not when it
    /// may not. The caller is expected to have confirmed the file exists.
    /// </summary>
    public static IReadOnlyList<CatalogIssue> Enforce(string profilePath, ProfilePolicy policy, string trustStoreDirectory)
    {
        ArgumentNullException.ThrowIfNull(profilePath);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(trustStoreDirectory);

        if (!policy.RequireSignedProfiles)
        {
            return [];
        }

        string signaturePath = profilePath + SignatureSuffix;
        if (!File.Exists(signaturePath))
        {
            return [Reject(profilePath, "unsigned-profile",
                $"policy {ProfilePolicy.RequireSignedProfilesValueName} is enabled but no signature file '{signaturePath}' accompanies this profile.")];
        }

        if (!TryReadSignature(signaturePath, out ProfileSignatureDocument signature))
        {
            return [Reject(profilePath, "unreadable-signature",
                $"signature file '{signaturePath}' is not a valid signature document.")];
        }

        string profileJson;
        try
        {
            profileJson = File.ReadAllText(profilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [Reject(profilePath, "signature-read-failed", $"could not read the profile to verify its signature: {ex.Message}")];
        }

        return VerifyAgainstTrustStore(profileJson, signature, trustStoreDirectory)
            ? []
            : [Reject(profilePath, "untrusted-profile",
                $"the signature does not verify against any key in the trust store '{trustStoreDirectory}'.")];
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="signature"/> verifies
    /// <paramref name="profileJson"/> against some <c>*.pem</c> key in
    /// <paramref name="trustStoreDirectory"/>. A missing directory or an empty store
    /// never verifies; an unreadable key file is skipped, not fatal.
    /// </summary>
    public static bool VerifyAgainstTrustStore(string profileJson, ProfileSignatureDocument signature, string trustStoreDirectory)
    {
        ArgumentNullException.ThrowIfNull(profileJson);
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(trustStoreDirectory);

        if (!Directory.Exists(trustStoreDirectory))
        {
            return false;
        }

        foreach (string pem in Directory.EnumerateFiles(trustStoreDirectory, "*.pem"))
        {
            using var key = ECDsa.Create();
            try
            {
                key.ImportFromPem(File.ReadAllText(pem));
            }
            catch (Exception ex) when (ex is ArgumentException or CryptographicException or IOException)
            {
                continue;
            }

            if (ProfileSignature.Verify(profileJson, signature, key))
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

    private static CatalogIssue Reject(string profilePath, string rule, string message)
        => new(CatalogIssueSeverity.Error, profilePath, "/", rule, message);
}
