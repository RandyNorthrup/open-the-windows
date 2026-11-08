using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Privacy;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw access [--all] [--json]</c>: which apps used the camera, microphone or location
/// (every recorded capability with <c>--all</c>), and when — read-only, from the last-used times
/// Windows keeps per app. Exit codes: 0, 3 unsupported OS.
/// </summary>
internal static class AccessCommand
{
    /// <summary>Builds the <c>access</c> command.</summary>
    public static Command Create(CliServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var all = new Option<bool>("--all") { Description = "Every recorded capability, not only camera, microphone and location." };
        var json = new Option<bool>("--json") { Description = "Emit the list as JSON on stdout (stable schema)." };
        var command = new Command("access", "Show which apps recently used the camera, microphone or location (read-only).");
        command.Options.Add(all);
        command.Options.Add(json);
        command.SetAction(parseResult =>
        {
            TextWriter stdout = parseResult.InvocationConfiguration.Output;
            if (!CommandSupport.IsSupported(services.Doctor, parseResult.InvocationConfiguration.Error, out int exitCode))
            {
                return exitCode;
            }

            IReadOnlyList<CapabilityUse> uses = services.CreateCapabilityAccess()
                .Recent(parseResult.GetValue(all) ? null : CapabilityAccessService.DefaultCapabilities);
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonSerializer.Serialize(uses, CliJsonContext.Default.IReadOnlyListCapabilityUse));
                return ExitCodes.Success;
            }

            if (uses.Count == 0)
            {
                stdout.WriteLine("No recorded use.");
            }

            foreach (CapabilityUse use in uses)
            {
                string when = use.InUse ? "in use now      " : string.Create(CultureInfo.InvariantCulture, $"{use.LastStarted!.Value.ToLocalTime():yyyy-MM-dd HH:mm}");
                stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{use.Capability,-12}  {when}  {use.App}"));
            }

            return ExitCodes.Success;
        });
        return command;
    }
}
