using System.CommandLine;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Cli.Tests;

/// <summary>
/// Shared harness for CLI command tests: builds the command tree with injected
/// services and captured output, and invokes it with argument arrays.
/// </summary>
internal static class CliTestHost
{
    /// <summary>Builds the root command from injected services plus fresh output writers.</summary>
    public static (RootCommand Root, StringWriter Out, StringWriter Err) Host(CliServices services)
        => (CommandLineBuilder.Build(services), new StringWriter(), new StringWriter());

    /// <summary>The command tree wired with the built-in catalogue, an elevated Windows 11 doctor and captured output.</summary>
    public static (RootCommand Root, StringWriter Out, StringWriter Err) DefaultHost()
    {
        var doctor = new DoctorService(
            new FakeOperatingSystemInfo(FakeOperatingSystemInfo.Windows11Pro24H2()),
            new FakeElevationContext(isElevated: true));
        return Host(TestCli.Services(doctor, _ => CatalogLoader.LoadBuiltIn()));
    }

    /// <summary>Parses and invokes the command tree with captured stdout/stderr.</summary>
    public static int Invoke(RootCommand root, StringWriter stdout, StringWriter stderr, params string[] args)
    {
        var parse = root.Parse(args);
        parse.InvocationConfiguration.Output = stdout;
        parse.InvocationConfiguration.Error = stderr;
        return parse.Invoke();
    }
}
