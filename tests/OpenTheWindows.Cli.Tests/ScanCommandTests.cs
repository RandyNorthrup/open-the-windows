using System.CommandLine;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.Core.Profiles;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Cli.Tests;

public sealed class ScanCommandTests
{
    private static (RootCommand Root, StringWriter Out, StringWriter Err) Build(bool supported = true)
        => Build(new FakeReaders(), supported);

    private static (RootCommand Root, StringWriter Out, StringWriter Err) Build(FakeReaders readers, bool supported = true)
    {
        OperatingSystemFacts os = supported
            ? FakeOperatingSystemInfo.Windows11Pro24H2()
            : FakeOperatingSystemInfo.WindowsServer2025();
        var doctor = new DoctorService(new FakeOperatingSystemInfo(os), new FakeElevationContext(isElevated: false));
        return CliTestHost.Host(TestCli.Services(doctor, _ => CatalogLoader.LoadBuiltIn(), readers));
    }

    [Fact]
    public void Unsupported_platform_exits_3()
    {
        var (root, stdout, stderr) = Build(supported: false);

        int code = CliTestHost.Invoke(root, stdout, stderr, "scan", "--profile", "balanced");

        Assert.Equal(ExitCodes.UnsupportedPlatform, code);
        Assert.Contains("Unsupported", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_profile_exits_2()
    {
        var (root, stdout, stderr) = Build();

        int code = CliTestHost.Invoke(root, stdout, stderr, "scan", "--profile", "nope");

        Assert.Equal(ExitCodes.Error, code);
        Assert.Contains("Unknown profile", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Drift_exits_1_and_lists_ids()
    {
        var (root, stdout, stderr) = Build();

        int code = CliTestHost.Invoke(root, stdout, stderr, "scan", "--profile", "balanced", "--include-draft");

        Assert.Equal(ExitCodes.Drift, code);
        Assert.Contains("DRIFT", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Two_formats_exits_2()
    {
        var (root, stdout, stderr) = Build();

        int code = CliTestHost.Invoke(root, stdout, stderr, "scan", "--profile", "basic", "--json", "--csv");

        Assert.Equal(ExitCodes.Error, code);
    }

    [Fact]
    public void Json_format_writes_report_to_stdout()
    {
        var (root, stdout, stderr) = Build();

        int code = CliTestHost.Invoke(root, stdout, stderr, "scan", "--profile", "balanced", "--include-draft", "--json");

        Assert.Equal(ExitCodes.Drift, code);
        using var document = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("balanced", document.RootElement.GetProperty("profile").GetString());
    }

    [Fact]
    public void Only_without_profile_scans_just_that_entry()
    {
        string id = CatalogLoader.LoadBuiltIn().Catalog!.Entries[0].Id.Value;
        var (root, stdout, stderr) = Build();

        int code = CliTestHost.Invoke(root, stdout, stderr, "scan", "--only", id, "--json");

        Assert.True(code is ExitCodes.Success or ExitCodes.Drift, "a single-entry scan should succeed or report drift, not error");
        using var document = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("only:" + id, document.RootElement.GetProperty("profile").GetString());
    }

    [Fact]
    public void Neither_profile_nor_only_reports_profile_required()
    {
        var (root, stdout, stderr) = Build();

        int code = CliTestHost.Invoke(root, stdout, stderr, "scan");

        Assert.Equal(ExitCodes.Error, code);
        Assert.Contains("--profile' is required", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Built_in_profile_id_resolves_via_the_profile_engine()
    {
        var (root, stdout, stderr) = Build();

        int code = CliTestHost.Invoke(root, stdout, stderr, "scan", "--profile", "home", "--json");

        Assert.True(code is ExitCodes.Success or ExitCodes.Drift, "a real-profile scan should succeed or report drift, not error");
        using var document = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("home", document.RootElement.GetProperty("profile").GetString());
    }

    [Fact]
    public void Profile_file_path_is_resolved()
    {
        string path = Path.Combine(Path.GetTempPath(), "otw-scan-profile-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, ProfileFixtures.ValidProfile);
        try
        {
            var (root, stdout, stderr) = Build();

            int code = CliTestHost.Invoke(root, stdout, stderr, "scan", "--profile", path, "--json");

            Assert.True(code is ExitCodes.Success or ExitCodes.Drift, "a profile-file scan should succeed or report drift, not error");
            using var document = JsonDocument.Parse(stdout.ToString());
            Assert.Equal("custom-file", document.RootElement.GetProperty("profile").GetString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Out_file_writes_csv_report()
    {
        var (root, stdout, stderr) = Build();
        string path = Path.Combine(Path.GetTempPath(), "otw-scan-" + Guid.NewGuid().ToString("N") + ".csv");
        try
        {
            int code = CliTestHost.Invoke(
                root, stdout, stderr, "scan", "--profile", "balanced", "--include-draft", "--csv", "--out", path);

            Assert.Equal(ExitCodes.Drift, code);
            Assert.True(File.Exists(path));
            Assert.Contains("id,state,risk", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.Contains("Wrote csv report", stdout.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Policy_on_rejects_an_unsigned_profile_file()
    {
        string path = Path.Combine(Path.GetTempPath(), "otw-unsigned-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, ProfileFixtures.ValidProfile);
        try
        {
            var (root, stdout, stderr) = Build(ProfileFixtures.PolicyOn());

            int code = CliTestHost.Invoke(root, stdout, stderr, "scan", "--profile", path, "--json");

            Assert.Equal(ExitCodes.Error, code);
            Assert.Contains(ProfilePolicy.RequireSignedProfilesValueName, stderr.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Policy_on_does_not_restrict_a_built_in_profile()
    {
        var (root, stdout, stderr) = Build(ProfileFixtures.PolicyOn());

        int code = CliTestHost.Invoke(root, stdout, stderr, "scan", "--profile", "home", "--json");

        Assert.True(code is ExitCodes.Success or ExitCodes.Drift, "built-in ids are exempt from the signing policy");
    }
}
