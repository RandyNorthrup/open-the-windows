using System.CommandLine;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Cli.Tests;

public sealed class DoctorCommandTests
{
    private static (RootCommand Root, StringWriter Out, StringWriter Err) Build(bool win11, bool elevated)
    {
        var doctor = new DoctorService(
            new FakeOperatingSystemInfo(win11 ? FakeOperatingSystemInfo.Windows11Pro24H2() : FakeOperatingSystemInfo.Windows10Pro22H2()),
            new FakeElevationContext(elevated));
        return CliTestHost.Host(TestCli.Services(doctor, _ => CatalogLoader.LoadBuiltIn()));
    }

    private static int Invoke(RootCommand root, StringWriter stdout, StringWriter stderr, params string[] args)
        => CliTestHost.Invoke(root, stdout, stderr, args);

    [Fact]
    public void Doctor_supported_platform_exits_zero_and_prints_summary()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Build(win11: true, elevated: true);

        int exit = Invoke(root, stdout, stderr, "doctor");

        Assert.Equal(ExitCodes.Success, exit);
        string text = stdout.ToString();
        Assert.Contains(AppInfo.Title, text, StringComparison.Ordinal);
        Assert.Contains("Windows 11 Pro 24H2 (build 26100.4351, x64)", text, StringComparison.Ordinal);
        Assert.Contains("Can apply: yes", text, StringComparison.Ordinal);
        Assert.Empty(stderr.ToString());
    }

    [Fact]
    public void Doctor_unsupported_platform_exits_three()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Build(win11: false, elevated: true);

        int exit = Invoke(root, stdout, stderr, "doctor");

        Assert.Equal(ExitCodes.UnsupportedPlatform, exit);
        Assert.Contains("UnsupportedWindowsVersion", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Doctor_json_emits_stable_camel_case_schema()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Build(win11: true, elevated: false);

        int exit = Invoke(root, stdout, stderr, "doctor", "--json");

        Assert.Equal(ExitCodes.Success, exit);
        using var doc = JsonDocument.Parse(stdout.ToString());
        JsonElement rootEl = doc.RootElement;
        Assert.Equal("Supported", rootEl.GetProperty("status").GetString());
        Assert.False(rootEl.GetProperty("isElevated").GetBoolean());
        Assert.False(rootEl.GetProperty("canApply").GetBoolean());
        Assert.Equal(26100, rootEl.GetProperty("operatingSystem").GetProperty("build").GetInt32());
        Assert.Equal("26100.4351", rootEl.GetProperty("operatingSystem").GetProperty("fullBuild").GetString());
        Assert.Equal(1, rootEl.GetProperty("notes").GetArrayLength());
    }

    [Fact]
    public void Root_help_mentions_doctor()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Build(win11: true, elevated: true);

        int exit = Invoke(root, stdout, stderr, "--help");

        Assert.Equal(ExitCodes.Success, exit);
        Assert.Contains("doctor", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_command_is_an_error()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Build(win11: true, elevated: true);

        int exit = Invoke(root, stdout, stderr, "frobnicate");

        Assert.NotEqual(ExitCodes.Success, exit);
    }
}
