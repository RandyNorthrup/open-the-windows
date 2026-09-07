using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Releases;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw check-update</c>: one HTTPS GET to GitHub Releases. Not run unless invoked.
/// Exit 0 when the check succeeds (whether or not a newer release exists); 2 on
/// network or parse failure; 3 on unsupported OS is not used — this is product
/// metadata, not an OS change.
/// </summary>
internal static class CheckUpdateCommand
{
    public static Command Create(Func<IAppReleaseChecker> createChecker)
    {
        ArgumentNullException.ThrowIfNull(createChecker);

        var json = new Option<bool>("--json") { Description = "Emit the check result as JSON." };
        var command = new Command("check-update", "Opt-in: query GitHub Releases for a newer version (one HTTPS request).");
        command.Options.Add(json);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            TextWriter stdout = parseResult.InvocationConfiguration.Output;
            AppReleaseCheckResult result = await createChecker().CheckAsync(cancellationToken).ConfigureAwait(false);
            if (parseResult.GetValue(json))
            {
                await stdout.WriteLineAsync(JsonSerializer.Serialize(result, CliJsonContext.Default.AppReleaseCheckResult)).ConfigureAwait(false);
            }
            else
            {
                await stdout.WriteLineAsync(result.Message).ConfigureAwait(false);
                if (result.ReleaseUri is { } uri && result.UpdateAvailable)
                {
                    await stdout.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"  {uri}")).ConfigureAwait(false);
                }
            }

            return result.Succeeded ? ExitCodes.Success : ExitCodes.Error;
        });
        return command;
    }
}
