using System.Security.Cryptography;
using System.Text.Json;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.Core.Tests.Profiles;

/// <summary>
/// Trust-store verification and the <see cref="ProfilePolicy.RequireSignedProfiles"/>
/// gate. Every case uses a throwaway trust store under the temp directory so the
/// machine store is never touched. The signed document is arbitrary JSON — the gate
/// verifies bytes, it does not parse a profile.
/// </summary>
public sealed class ProfileTrustTests : IDisposable
{
    private const string ProfileJson = """{ "schemaVersion": 1, "id": "trust-test", "value": 42 }""";

    private readonly string _root;
    private readonly string _trustStore;
    private readonly string _profilePath;

    public ProfileTrustTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "otw-trust-" + Guid.NewGuid().ToString("N"));
        _trustStore = Path.Combine(_root, "trusted-keys");
        Directory.CreateDirectory(_trustStore);
        _profilePath = Path.Combine(_root, "profile.json");
        File.WriteAllText(_profilePath, ProfileJson);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static ECDsa NewKey() => ECDsa.Create(ECCurve.NamedCurves.nistP256);

    private void Trust(ECDsa key, string fileName = "signer.pem")
        => File.WriteAllText(Path.Combine(_trustStore, fileName), key.ExportSubjectPublicKeyInfoPem());

    private void Sign(ECDsa key)
    {
        ProfileSignatureDocument signature = ProfileSignature.Sign(File.ReadAllText(_profilePath), key);
        File.WriteAllText(
            _profilePath + ProfileTrust.SignatureSuffix,
            JsonSerializer.Serialize(signature, ProfileJsonContext.Default.ProfileSignatureDocument));
    }

    private ProfileSignatureDocument ReadSignature()
    {
        Assert.True(ProfileTrust.TryReadSignature(_profilePath + ProfileTrust.SignatureSuffix, out ProfileSignatureDocument signature));
        return signature;
    }

    [Fact]
    public void Verifies_against_a_trusted_key()
    {
        using ECDsa key = NewKey();
        Trust(key);
        Sign(key);

        Assert.True(ProfileTrust.VerifyAgainstTrustStore(File.ReadAllText(_profilePath), ReadSignature(), _trustStore));
    }

    [Fact]
    public void Does_not_verify_against_a_key_absent_from_the_store()
    {
        using ECDsa signer = NewKey();
        using ECDsa other = NewKey();
        Trust(other);
        Sign(signer);

        Assert.False(ProfileTrust.VerifyAgainstTrustStore(File.ReadAllText(_profilePath), ReadSignature(), _trustStore));
    }

    [Fact]
    public void Policy_off_allows_any_file()
        => Assert.Empty(ProfileTrust.Enforce(_profilePath, ProfilePolicy.Unrestricted, _trustStore));

    [Fact]
    public void Policy_on_rejects_an_unsigned_file()
    {
        CatalogIssue issue = Assert.Single(ProfileTrust.Enforce(_profilePath, new ProfilePolicy(true), _trustStore));
        Assert.Equal("unsigned-profile", issue.Rule);
        Assert.Equal(CatalogIssueSeverity.Error, issue.Severity);
    }

    [Fact]
    public void Policy_on_accepts_a_file_signed_by_a_trusted_key()
    {
        using ECDsa key = NewKey();
        Trust(key);
        Sign(key);

        Assert.Empty(ProfileTrust.Enforce(_profilePath, new ProfilePolicy(true), _trustStore));
    }

    [Fact]
    public void Policy_on_rejects_a_file_signed_by_an_untrusted_key()
    {
        using ECDsa signer = NewKey();
        using ECDsa other = NewKey();
        Trust(other);
        Sign(signer);

        Assert.Equal("untrusted-profile", Assert.Single(ProfileTrust.Enforce(_profilePath, new ProfilePolicy(true), _trustStore)).Rule);
    }

    [Fact]
    public void Policy_on_rejects_a_file_altered_after_signing()
    {
        using ECDsa key = NewKey();
        Trust(key);
        Sign(key);
        File.WriteAllText(_profilePath, ProfileJson.Replace("42", "43", StringComparison.Ordinal));

        Assert.Equal("untrusted-profile", Assert.Single(ProfileTrust.Enforce(_profilePath, new ProfilePolicy(true), _trustStore)).Rule);
    }
}
