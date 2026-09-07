using System.CommandLine;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.Core.Releases;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Cli.Tests;

public sealed class CheckUpdateCommandTests
{
    [Fact]
    public void Prints_the_checker_message()
    {
        FakeAppReleaseChecker checker = Checker(new AppReleaseCheckResult(true, true, "0.1.0", "0.2.0", new Uri("https://example.invalid/r"), "A newer release is available."));
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Host(checker);

        int code = CliTestHost.Invoke(root, stdout, stderr, "check-update");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Equal(1, checker.CallCount);
        Assert.Contains("newer", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Failed_check_exits_2()
    {
        FakeAppReleaseChecker checker = Checker(new AppReleaseCheckResult(false, false, "0.1.0", null, null, "Could not reach GitHub."));
        (RootCommand root, StringWriter stdout, StringWriter stderr) = Host(checker);

        int code = CliTestHost.Invoke(root, stdout, stderr, "check-update");

        Assert.Equal(ExitCodes.Error, code);
        Assert.Contains("Could not reach GitHub", stdout.ToString(), StringComparison.Ordinal);
    }

    private static FakeAppReleaseChecker Checker(AppReleaseCheckResult result)
        => new() { Result = result };

    private static (RootCommand Root, StringWriter Out, StringWriter Err) Host(FakeAppReleaseChecker checker)
    {
        var doctor = new DoctorService(
            new FakeOperatingSystemInfo(FakeOperatingSystemInfo.Windows11Pro24H2()),
            new FakeElevationContext(isElevated: true));
        return CliTestHost.Host(TestCli.Services(doctor, _ => CatalogLoader.LoadBuiltIn(), createReleaseChecker: () => checker));
    }
}
