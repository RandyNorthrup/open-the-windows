using System.Security.Cryptography;
using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.Core.Tests.Profiles;

public sealed class ProfileSignatureTests
{
    private const string ProfileJson =
        """{ "id": "sig-test", "name": "Sig Test", "levels": { "Privacy": "Basic" }, "scope": "User" }""";

    // Same content, keys reordered and whitespace/pretty-printing changed.
    private const string SameProfileReordered = """
        {
            "scope": "User",
            "name": "Sig Test",
            "levels": { "Privacy": "Basic" },
            "id": "sig-test"
        }
        """;

    private const string TamperedProfile =
        """{ "id": "sig-test", "name": "Sig Test", "levels": { "Privacy": "Strict" }, "scope": "User" }""";

    private static ECDsa NewKey() => ECDsa.Create(ECCurve.NamedCurves.nistP256);

    private static ECDsa PublicOnly(ECDsa key)
    {
        var copy = ECDsa.Create();
        copy.ImportSubjectPublicKeyInfo(key.ExportSubjectPublicKeyInfo(), out _);
        return copy;
    }

    [Fact]
    public void A_signature_verifies_with_the_matching_public_key()
    {
        using ECDsa signer = NewKey();
        using ECDsa verifier = PublicOnly(signer);

        ProfileSignatureDocument signature = ProfileSignature.Sign(ProfileJson, signer);

        Assert.Equal(ProfileSignature.Algorithm, signature.Algorithm);
        Assert.True(ProfileSignature.Verify(ProfileJson, signature, verifier));
    }

    [Fact]
    public void A_signature_fails_with_a_different_key()
    {
        using ECDsa signer = NewKey();
        using ECDsa other = NewKey();

        ProfileSignatureDocument signature = ProfileSignature.Sign(ProfileJson, signer);

        Assert.False(ProfileSignature.Verify(ProfileJson, signature, other));
    }

    [Fact]
    public void A_tampered_profile_fails_verification()
    {
        using ECDsa signer = NewKey();

        ProfileSignatureDocument signature = ProfileSignature.Sign(ProfileJson, signer);

        Assert.False(ProfileSignature.Verify(TamperedProfile, signature, signer));
    }

    [Fact]
    public void Verification_survives_reordering_and_pretty_printing()
    {
        using ECDsa signer = NewKey();

        // Sign the compact form; verify the reordered, pretty-printed same content.
        ProfileSignatureDocument signature = ProfileSignature.Sign(ProfileJson, signer);

        Assert.True(ProfileSignature.Verify(SameProfileReordered, signature, signer));
    }

    [Fact]
    public void A_wrong_algorithm_is_rejected()
    {
        using ECDsa signer = NewKey();
        ProfileSignatureDocument signature = ProfileSignature.Sign(ProfileJson, signer);

        var tampered = signature with { Algorithm = "RS256" };

        Assert.False(ProfileSignature.Verify(ProfileJson, tampered, signer));
    }

    [Fact]
    public void A_corrupt_signature_returns_false_not_throws()
    {
        using ECDsa signer = NewKey();
        ProfileSignatureDocument signature = ProfileSignature.Sign(ProfileJson, signer);

        var corrupt = signature with { Signature = "not+valid+base64!!" };

        Assert.False(ProfileSignature.Verify(ProfileJson, corrupt, signer));
    }

    [Fact]
    public void Key_id_is_the_same_for_the_private_key_and_its_public_half()
    {
        using ECDsa signer = NewKey();
        using ECDsa verifier = PublicOnly(signer);

        Assert.Equal(ProfileSignature.ComputeKeyId(signer), ProfileSignature.ComputeKeyId(verifier));
    }

    [Fact]
    public void Trust_store_verification_matches_only_the_signer()
    {
        using ECDsa signer = NewKey();
        using ECDsa signerPublic = PublicOnly(signer);
        using ECDsa strangerPublic = NewKey();
        ProfileSignatureDocument signature = ProfileSignature.Sign(ProfileJson, signer);

        Assert.True(ProfileSignature.VerifyAgainstTrustStore(ProfileJson, signature, [strangerPublic, signerPublic]));
        Assert.False(ProfileSignature.VerifyAgainstTrustStore(ProfileJson, signature, [strangerPublic]));
        Assert.False(ProfileSignature.VerifyAgainstTrustStore(ProfileJson, signature, []));
    }
}
