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

    private DirectoryTrustedKeySource Keys => new(_trustStore);

    private IReadOnlyList<CatalogIssue> Enforce(ProfilePolicy policy)
        => ProfileTrust.Enforce(_profilePath, File.ReadAllText(_profilePath), policy, Keys);

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

        Assert.True(ProfileTrust.VerifyAgainstTrustStore(File.ReadAllText(_profilePath), ReadSignature(), Keys));
    }

    [Fact]
    public void Does_not_verify_against_a_key_absent_from_the_store()
    {
        using ECDsa signer = NewKey();
        using ECDsa other = NewKey();
        Trust(other);
        Sign(signer);

        Assert.False(ProfileTrust.VerifyAgainstTrustStore(File.ReadAllText(_profilePath), ReadSignature(), Keys));
    }

    [Fact]
    public void Policy_off_allows_any_file()
        => Assert.Empty(Enforce(ProfilePolicy.Unrestricted));

    [Fact]
    public void Policy_on_rejects_an_unsigned_file()
    {
        CatalogIssue issue = Assert.Single(Enforce(new ProfilePolicy(true)));
        Assert.Equal("unsigned-profile", issue.Rule);
        Assert.Equal(CatalogIssueSeverity.Error, issue.Severity);
    }

    [Fact]
    public void Policy_on_accepts_a_file_signed_by_a_trusted_key()
    {
        using ECDsa key = NewKey();
        Trust(key);
        Sign(key);

        Assert.Empty(Enforce(new ProfilePolicy(true)));
    }

    [Fact]
    public void Policy_on_rejects_a_file_signed_by_an_untrusted_key()
    {
        using ECDsa signer = NewKey();
        using ECDsa other = NewKey();
        Trust(other);
        Sign(signer);

        Assert.Equal("untrusted-profile", Assert.Single(Enforce(new ProfilePolicy(true))).Rule);
    }

    [Fact]
    public void Policy_on_rejects_a_file_altered_after_signing()
    {
        using ECDsa key = NewKey();
        Trust(key);
        Sign(key);
        File.WriteAllText(_profilePath, ProfileJson.Replace("42", "43", StringComparison.Ordinal));

        Assert.Equal("untrusted-profile", Assert.Single(Enforce(new ProfilePolicy(true))).Rule);
    }

    [Fact]
    public void Enforcement_covers_the_text_given_not_whatever_is_on_disk_later()
    {
        using ECDsa key = NewKey();
        Trust(key);
        Sign(key);
        string altered = ProfileJson.Replace("42", "43", StringComparison.Ordinal);

        // The signed file is still on disk, but the text being used was swapped: it must not verify.
        IReadOnlyList<CatalogIssue> issues = ProfileTrust.Enforce(_profilePath, altered, new ProfilePolicy(true), Keys);

        Assert.Equal("untrusted-profile", Assert.Single(issues).Rule);
    }

    [Fact]
    public void A_signature_over_json_with_a_duplicate_property_never_verifies()
    {
        using ECDsa key = NewKey();
        Trust(key);
        Sign(key);
        string duplicated = ProfileJson.Insert(ProfileJson.IndexOf('{', StringComparison.Ordinal) + 1, "\"id\":\"a\",\"id\":\"b\",");

        Assert.False(ProfileTrust.VerifyAgainstTrustStore(duplicated, ReadSignature(), Keys));
    }
}
