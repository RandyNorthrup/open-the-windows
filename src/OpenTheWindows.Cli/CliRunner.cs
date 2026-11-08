using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using OpenTheWindows.Core;

namespace OpenTheWindows.Cli;

/// <summary>
/// Invokes a parsed command line with the product's exit-code contract instead of the
/// parser's defaults: a usage error exits with <see cref="ExitCodes.InvalidInput"/> and an
/// unexpected failure with <see cref="ExitCodes.Error"/>. The parser would return 1 for
/// both, which the contract reserves for <see cref="ExitCodes.Drift"/> — so an Intune
/// detection script would read a typo or a crash as "drift" and trigger remediation.
/// </summary>
internal static class CliRunner
{
    /// <summary>Runs <paramref name="parse"/> and returns the process exit code.</summary>
    public static int Invoke(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        TextWriter stderr = parse.InvocationConfiguration.Error;
        if (parse.Errors.Count > 0)
        {
            foreach (ParseError error in parse.Errors)
            {
                stderr.WriteLine(error.Message);
            }

            stderr.WriteLine("Run 'otw --help', or 'otw <command> --help', for usage.");
            return ExitCodes.InvalidInput;
        }

        parse.InvocationConfiguration.EnableDefaultExceptionHandler = false;
        try
        {
            return parse.Invoke();
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
        {
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Unexpected error ({ex.GetType().Name}): {ex.Message}"));
            return ExitCodes.Error;
        }
    }
}
