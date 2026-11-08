using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw profile capture --out &lt;file&gt; [--id &lt;id&gt;] [--name &lt;name&gt;] [--include-draft]</c>:
/// scans this machine (read-only, no elevation) and writes a profile naming every catalogue
/// setting already in place, so the same configuration can be applied to other PCs. The
/// written file is re-loaded and schema-validated before the command reports success.
/// Exit codes: 0 written, 2 error, 3 unsupported OS, 4 invalid input.
/// </summary>
internal static class ProfileCaptureCommand
{
    public static Command Create(CliServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var output = new Option<string>("--out") { Description = "Where to write the profile .json.", Required = true };
        var id = new Option<string?>("--id") { Description = "The profile id (lower-case letters, digits and hyphens; default captured-<date>)." };
        var name = new Option<string?>("--name") { Description = "The profile's display name (default: Captured from <computer>)." };
        var includeDraft = new Option<bool>("--include-draft") { Description = "Also consider Draft entries (Verified only by default)." };
        var command = new Command("capture", "Write a profile of the catalogue settings this machine already has, to apply them elsewhere.");
        command.Options.Add(output);
        command.Options.Add(id);
        command.Options.Add(name);
        command.Options.Add(includeDraft);
        command.SetAction(parseResult => Execute(parseResult, services, new CaptureOptions(output, id, name, includeDraft)));
        return command;
    }

    private static int Execute(ParseResult parseResult, CliServices services, CaptureOptions options)
    {
        TextWriter stdout = parseResult.InvocationConfiguration.Output;
        TextWriter stderr = parseResult.InvocationConfiguration.Error;
        if (!CommandSupport.IsSupported(services.Doctor, stderr, out int exitCode, out OperatingSystemFacts facts))
        {
            return exitCode;
        }

        string? requestedId = parseResult.GetValue(options.Id);
        if (requestedId is not null && (requestedId.Length == 0 || !requestedId.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')))
        {
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Profile id '{requestedId}' must use lower-case letters, digits and hyphens only."));
            return ExitCodes.InvalidInput;
        }

        TweakCatalog catalog = services.LoadCatalog(null).Catalog
            ?? throw new InvalidOperationException("The built-in catalogue failed validation.");
        bool includeDraft = parseResult.GetValue(options.IncludeDraft);
        List<TweakDefinition> candidates = [.. catalog.Entries.Where(e =>
            e.Status != TweakStatus.Deprecated && (includeDraft || e.Status == TweakStatus.Verified) && e.AppliesTo.Matches(facts))];

        ScanReport report = services.CreateScanEngine().Scan(candidates, requestedId ?? "capture");
        string id = requestedId ?? string.Create(CultureInfo.InvariantCulture, $"captured-{report.At.UtcDateTime:yyyyMMdd}");
        string displayName = parseResult.GetValue(options.Name) ?? string.Create(CultureInfo.InvariantCulture, $"Captured from {Environment.MachineName}");
        Profile profile = ProfileCapture.FromScan(report, catalog, id, displayName);

        string path = parseResult.GetValue(options.Output)!;
        string json = JsonSerializer.Serialize(profile, ProfileJsonContext.Default.Profile);
        ProfileLoadResult check = ProfileLoader.LoadText(path, json);
        if (check.Profile is null)
        {
            foreach (CatalogIssue issue in check.Errors)
            {
                stderr.WriteLine(issue.ToString());
            }

            return ExitCodes.Error;
        }

        File.WriteAllText(path, json);
        stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Wrote profile '{id}' to {path}: {profile.Include.Count} setting(s) of {candidates.Count} scanned were already in place."));
        return ExitCodes.Success;
    }

    private sealed record CaptureOptions(Option<string> Output, Option<string?> Id, Option<string?> Name, Option<bool> IncludeDraft);
}
