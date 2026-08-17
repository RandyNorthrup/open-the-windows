using System.CommandLine;

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

    /// <summary>Parses and invokes the command tree with captured stdout/stderr.</summary>
    public static int Invoke(RootCommand root, StringWriter stdout, StringWriter stderr, params string[] args)
    {
        var parse = root.Parse(args);
        parse.InvocationConfiguration.Output = stdout;
        parse.InvocationConfiguration.Error = stderr;
        return parse.Invoke();
    }
}
