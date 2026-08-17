using System.CommandLine;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Cli.Tests;

public sealed class ProfileCommandTests
{
    private const string ValidFileProfile = """
    {
      "schemaVersion": 1,
      "id": "custom-file",
      "name": "Custom File",
      "description": "A profile loaded from a file by a CLI test.",
      "levels": { "Privacy": "Basic", "Updates": "Basic", "Security": "Basic", "Performance": "Basic", "Debloat": "Basic", "Shell": "Basic" },
      "include": [],
      "exclude": [],
      "includeDraft": false,
      "scope": "User",
      "options": { "restorePoint": true, "restartExplorer": true, "allowAdvanced": false, "allowBreaking": false },
      "appliesTo": { "editions": [], "minBuild": null, "maxBuild": null, "architectures": [] }
    }
    """;

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
}
