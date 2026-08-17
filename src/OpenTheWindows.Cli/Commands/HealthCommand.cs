using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw health [--json]</c>: run the read-only machine health checks
/// (Secure Boot, TPM, Defender, BitLocker, firewall, …). Never changes the
/// machine. Exit code 0 unless the command itself errors.
/// </summary>
internal static class HealthCommand
{
    public static Command Create(IMachineHealthProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        var jsonOption = new Option<bool>("--json")
        {
            Description = "Emit the checks as JSON on stdout (stable schema).",
        };

        var command = new Command("health", "Run read-only machine health checks (Secure Boot, TPM, Defender, BitLocker, …).");
        command.Options.Add(jsonOption);
        command.SetAction(parseResult =>
        {
            IReadOnlyList<HealthCheckResult> results = probe.Run();
            TextWriter stdout = parseResult.InvocationConfiguration.Output;

            if (parseResult.GetValue(jsonOption))
            {
                stdout.WriteLine(JsonSerializer.Serialize(results, CliJsonContext.Default.IReadOnlyListHealthCheckResult));
            }
            else
            {
                foreach (HealthCheckResult result in results)
                {
                    stdout.WriteLine(string.Create(
                        CultureInfo.InvariantCulture, $"[{result.Status,-13}] {result.Title}: {result.Detail}"));
                }
            }

            return ExitCodes.Success;
        });

        return command;
    }
}
