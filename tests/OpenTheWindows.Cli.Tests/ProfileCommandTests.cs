using System.CommandLine;
using System.Security.Cryptography;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Cli.Tests;

public sealed class ProfileCommandTests
{
    private const string ValidFileProfile = ProfileFixtures.ValidProfile;

    private const string UnknownIncludeProfile = """
    {
      "schemaVersion": 1,
      "id": "bad-include",
      "name": "Bad Include",
      "description": "References a catalogue id that does not exist.",
      "levels": { "Privacy": "Off", "Updates": "Off", "Security": "Off", "Performance": "Off", "Debloat": "Off", "Shell": "Off" },
      "include": ["does.not.exist"],
      "exclude": [],
      "includeDraft": false,
      "scope": "User",
      "options": { "restorePoint": true, "restartExplorer": false, "allowAdvanced": false, "allowBreaking": false },
      "appliesTo": { "editions": [], "minBuild": null, "maxBuild": null, "architectures": [] }
    }
    """;

    private static (RootCommand Root, StringWriter Out, StringWriter Err) Build()
    {
        var doctor = new DoctorService(
            new FakeOperatingSystemInfo(FakeOperatingSystemInfo.Windows11Pro24H2()),
            new FakeElevationContext(isElevated: true));
        return CliTestHost.Host(TestCli.Services(doctor, _ => CatalogLoader.LoadBuiltIn()));
    }

    private static int Invoke(RootCommand root, StringWriter stdout, StringWriter stderr, params string[] args)
        => CliTestHost.Invoke(root, stdout, stderr, args);

    private static string WriteTempProfile(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), "otw-profile-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        return path;
    }

    private static (int Exit, string Out, string Err) RunProfile(params string[] args)
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Build();
        int exit = Invoke(root, stdout, stderr, ["profile", .. args]);
        return (exit, stdout.ToString(), stderr.ToString());
    }

    private static (string Profile, string PrivateKey, string PublicKey) WriteSigningFixture(DirectoryInfo dir, ECDsa signer)
    {
        string profile = Path.Combine(dir.FullName, "profile.json");
        string privateKey = Path.Combine(dir.FullName, "key.pem");
        string publicKey = Path.Combine(dir.FullName, "key.pub.pem");
        File.WriteAllText(profile, ValidFileProfile);
        File.WriteAllText(privateKey, signer.ExportECPrivateKeyPem());
        File.WriteAllText(publicKey, signer.ExportSubjectPublicKeyInfoPem());
        return (profile, privateKey, publicKey);
    }

    [Fact]
    public void List_prints_all_seven_built_ins()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Build();

        int exit = Invoke(root, stdout, stderr, "profile", "list");

        Assert.Equal(ExitCodes.Success, exit);
        string text = stdout.ToString();
        Assert.Contains("7 profiles.", text, StringComparison.Ordinal);
        Assert.Contains("enterprise-workstation", text, StringComparison.Ordinal);
        Assert.Empty(stderr.ToString());
    }

    [Fact]
    public void List_json_emits_an_array_of_profiles()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Build();

        int exit = Invoke(root, stdout, stderr, "profile", "list", "--json");

        Assert.Equal(ExitCodes.Success, exit);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(7, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void Show_prints_a_built_in_profile()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Build();

        int exit = Invoke(root, stdout, stderr, "profile", "show", "home");

        Assert.Equal(ExitCodes.Success, exit);
        string text = stdout.ToString();
        Assert.Contains("Levels:", text, StringComparison.Ordinal);
        Assert.Contains("Privacy=Balanced", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Show_json_emits_the_profile_object()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Build();

        int exit = Invoke(root, stdout, stderr, "profile", "show", "privacy-max", "--json");

        Assert.Equal(ExitCodes.Success, exit);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("privacy-max", doc.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public void Show_reports_an_unknown_profile()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Build();

        int exit = Invoke(root, stdout, stderr, "profile", "show", "no-such-profile");

        Assert.Equal(ExitCodes.InvalidInput, exit);
        Assert.Contains("No built-in profile", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_accepts_a_built_in_profile()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Build();

        int exit = Invoke(root, stdout, stderr, "profile", "validate", "enterprise-workstation");

        Assert.Equal(ExitCodes.Success, exit);
        Assert.Contains("valid", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Show_and_validate_accept_a_profile_file()
    {
        string path = WriteTempProfile(ValidFileProfile);
        try
        {
            (RootCommand root, StringWriter stdout, StringWriter stderr) = Build();
            Assert.Equal(ExitCodes.Success, Invoke(root, stdout, stderr, "profile", "show", path));

            (RootCommand root2, StringWriter out2, StringWriter err2) = Build();
            Assert.Equal(ExitCodes.Success, Invoke(root2, out2, err2, "profile", "validate", path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Validate_rejects_a_profile_with_an_unknown_include()
    {
        string path = WriteTempProfile(UnknownIncludeProfile);
        try
        {
            (RootCommand root, StringWriter stdout, StringWriter stderr) = Build();

            int exit = Invoke(root, stdout, stderr, "profile", "validate", path);

            Assert.Equal(ExitCodes.InvalidInput, exit);
            Assert.Contains("include-unknown", stdout.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Validate_json_emits_the_issue_array()
    {
        string path = WriteTempProfile(UnknownIncludeProfile);
        try
        {
            (RootCommand root, StringWriter stdout, StringWriter stderr) = Build();

            int exit = Invoke(root, stdout, stderr, "profile", "validate", path, "--json");

            Assert.Equal(ExitCodes.InvalidInput, exit);
            using var doc = JsonDocument.Parse(stdout.ToString());
            Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
            Assert.True(doc.RootElement.GetArrayLength() >= 1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Sign_then_verify_round_trips_with_the_public_key()
    {
        var dir = Directory.CreateTempSubdirectory("otw-sign-");
        try
        {
            using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            (string profile, string privateKey, string publicKey) = WriteSigningFixture(dir, signer);

            Assert.Equal(ExitCodes.Success, RunProfile("sign", profile, "--key", privateKey).Exit);
            Assert.True(File.Exists(profile + ".sig"));

            (int exit, string output, _) = RunProfile("verify", profile, "--key", publicKey);
            Assert.Equal(ExitCodes.Success, exit);
            Assert.Contains("valid", output, StringComparison.Ordinal);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Verify_fails_when_the_profile_is_tampered_after_signing()
    {
        var dir = Directory.CreateTempSubdirectory("otw-sign-");
        try
        {
            using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            (string profile, string privateKey, string publicKey) = WriteSigningFixture(dir, signer);
            RunProfile("sign", profile, "--key", privateKey);

            // Change the content (still schema-valid) so the signature no longer matches.
            File.WriteAllText(profile, ValidFileProfile.Replace("\"Privacy\": \"Basic\"", "\"Privacy\": \"Strict\"", StringComparison.Ordinal));

            Assert.Equal(ExitCodes.InvalidInput, RunProfile("verify", profile, "--key", publicKey).Exit);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Verify_fails_with_a_wrong_public_key()
    {
        var dir = Directory.CreateTempSubdirectory("otw-sign-");
        try
        {
            using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            (string profile, string privateKey, _) = WriteSigningFixture(dir, signer);
            string strangerPem = Path.Combine(dir.FullName, "stranger.pub.pem");
            File.WriteAllText(strangerPem, stranger.ExportSubjectPublicKeyInfoPem());
            RunProfile("sign", profile, "--key", privateKey);

            Assert.Equal(ExitCodes.InvalidInput, RunProfile("verify", profile, "--key", strangerPem).Exit);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Verify_reports_a_missing_signature_file()
    {
        string path = WriteTempProfile(ValidFileProfile);
        try
        {
            (int exit, _, string error) = RunProfile("verify", path);

            Assert.Equal(ExitCodes.InvalidInput, exit);
            Assert.Contains("No signature file", error, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Verify_reports_a_missing_key_file()
    {
        var dir = Directory.CreateTempSubdirectory("otw-sign-");
        try
        {
            using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            (string profile, string privateKey, _) = WriteSigningFixture(dir, signer);
            RunProfile("sign", profile, "--key", privateKey);

            (int exit, _, string error) = RunProfile("verify", profile, "--key", Path.Combine(dir.FullName, "absent.pem"));

            Assert.Equal(ExitCodes.InvalidInput, exit);
            Assert.Contains("does not exist", error, StringComparison.Ordinal);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Verify_reports_a_malformed_signature_file()
    {
        var dir = Directory.CreateTempSubdirectory("otw-sign-");
        try
        {
            string profile = Path.Combine(dir.FullName, "profile.json");
            File.WriteAllText(profile, ValidFileProfile);
            File.WriteAllText(profile + ".sig", "{ not json ");

            (int exit, _, string error) = RunProfile("verify", profile, "--key", Path.Combine(dir.FullName, "any.pem"));

            Assert.Equal(ExitCodes.InvalidInput, exit);
            Assert.Contains("not valid JSON", error, StringComparison.Ordinal);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Sign_rejects_a_public_only_key()
    {
        var dir = Directory.CreateTempSubdirectory("otw-sign-");
        try
        {
            using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            string profile = Path.Combine(dir.FullName, "profile.json");
            string publicPem = Path.Combine(dir.FullName, "key.pub.pem");
            File.WriteAllText(profile, ValidFileProfile);
            File.WriteAllText(publicPem, signer.ExportSubjectPublicKeyInfoPem());

            (int exit, _, _) = RunProfile("sign", profile, "--key", publicPem);

            Assert.Equal(ExitCodes.InvalidInput, exit);
            Assert.False(File.Exists(profile + ".sig"));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
