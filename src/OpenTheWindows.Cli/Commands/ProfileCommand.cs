using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Model;
using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw profile list|show|validate</c>: inspect and validate profiles (the
/// built-in ones or an operator file). Read-only; never touches the machine.
/// Exit codes: 0 ok; 4 invalid input (unknown profile, unreadable file, a
/// profile that fails schema or structural validation).
/// </summary>
internal static class ProfileCommand
{
    public static Command Create(Func<string?, CatalogLoadResult> loadCatalog)
    {
        ArgumentNullException.ThrowIfNull(loadCatalog);

        var jsonOption = new Option<bool>("--json")
        {
            Description = "Emit JSON on stdout (stable schema).",
            Recursive = true,
        };

        var command = new Command("profile", "Inspect and validate profiles (read-only).");
        command.Options.Add(jsonOption);
        command.Subcommands.Add(CreateList(jsonOption));
        command.Subcommands.Add(CreateShow(jsonOption));
        command.Subcommands.Add(CreateValidate(loadCatalog, jsonOption));
        return command;
    }

    private static Command CreateList(Option<bool> json)
    {
        var command = new Command("list", "List the built-in profiles.");
        command.SetAction(parseResult =>
        {
            TextWriter stdout = parseResult.InvocationConfiguration.Output;
            List<Profile> profiles = [.. ProfileLoader.LoadBuiltIns()
                .Where(r => r.IsValid)
                .Select(r => r.Profile!)];

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonSerializer.Serialize<IReadOnlyList<Profile>>(profiles, ProfileJsonContext.Default.IReadOnlyListProfile));
                return ExitCodes.Success;
            }

            WriteTable(stdout, profiles);
            return ExitCodes.Success;
        });
        return command;
    }

    private static Command CreateShow(Option<bool> json)
    {
        var profileArgument = new Argument<string>("profile")
        {
            Description = "Built-in profile id (e.g. home) or a path to a profile .json file.",
        };
        var command = new Command("show", "Show one profile: its level dial, include/exclude, scope, options and applicability.");
        command.Arguments.Add(profileArgument);
        command.SetAction(parseResult =>
        {
            TextWriter stdout = parseResult.InvocationConfiguration.Output;
            TextWriter stderr = parseResult.InvocationConfiguration.Error;
            string idOrPath = parseResult.GetValue(profileArgument) ?? string.Empty;

            if (!TryResolve(idOrPath, stderr, out ProfileLoadResult result, out _))
            {
                return ExitCodes.InvalidInput;
            }

            if (result.Profile is null)
            {
                WriteIssues(stderr, result.Errors);
                stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Profile '{idOrPath}' failed to load."));
                return ExitCodes.InvalidInput;
            }

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonSerializer.Serialize(result.Profile, ProfileJsonContext.Default.Profile));
                return ExitCodes.Success;
            }

            WriteProfile(stdout, result.Profile);
            return ExitCodes.Success;
        });
        return command;
    }

    private static Command CreateValidate(Func<string?, CatalogLoadResult> loadCatalog, Option<bool> json)
    {
        var profileArgument = new Argument<string>("profile")
        {
            Description = "Built-in profile id (e.g. home) or a path to a profile .json file.",
        };
        var catalogDirOption = new Option<string?>("--catalog-dir")
        {
            Description = "Directory of additional catalogue JSON files; entries override built-in ones by id.",
        };
        var command = new Command("validate", "Validate a profile against the schema and against the catalogue (id references, no built-in Breaking).");
        command.Arguments.Add(profileArgument);
        command.Options.Add(catalogDirOption);
        command.SetAction(parseResult =>
        {
            TextWriter stdout = parseResult.InvocationConfiguration.Output;
            TextWriter stderr = parseResult.InvocationConfiguration.Error;
            string idOrPath = parseResult.GetValue(profileArgument) ?? string.Empty;

            if (!TryResolve(idOrPath, stderr, out ProfileLoadResult result, out bool isBuiltIn))
            {
                return ExitCodes.InvalidInput;
            }

            List<CatalogIssue> issues = [.. result.Issues];
            if (result.Profile is not null)
            {
                CatalogLoadResult catalog = loadCatalog(parseResult.GetValue(catalogDirOption));
                if (catalog.Catalog is null)
                {
                    WriteIssues(stderr, catalog.Errors);
                    stderr.WriteLine("Catalogue failed validation; run 'otw catalog validate' for details.");
                    return ExitCodes.InvalidInput;
                }

                issues.AddRange(ProfileValidator.Validate(result.Profile, catalog.Catalog, isBuiltIn));
            }

            bool valid = result.Profile is not null && !issues.Any(i => i.Severity == CatalogIssueSeverity.Error);
            int warnings = issues.Count(i => i.Severity == CatalogIssueSeverity.Warning);

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonSerializer.Serialize<IReadOnlyList<CatalogIssue>>(issues, CatalogJsonContext.Default.IReadOnlyListCatalogIssue));
                return valid ? ExitCodes.Success : ExitCodes.InvalidInput;
            }

            foreach (CatalogIssue issue in issues)
            {
                stdout.WriteLine(issue.ToString());
            }

            stdout.WriteLine(valid
                ? string.Create(CultureInfo.InvariantCulture, $"Profile '{idOrPath}' valid: {warnings} warning(s).")
                : string.Create(CultureInfo.InvariantCulture, $"Profile '{idOrPath}' INVALID: {issues.Count(i => i.Severity == CatalogIssueSeverity.Error)} error(s), {warnings} warning(s)."));
            return valid ? ExitCodes.Success : ExitCodes.InvalidInput;
        });
        return command;
    }

    /// <summary>
    /// Resolves the argument to a profile source: an existing file first (explicit
    /// path), otherwise a built-in profile id. Reports the not-found case on stderr.
    /// </summary>
    private static bool TryResolve(string idOrPath, TextWriter stderr, out ProfileLoadResult result, out bool isBuiltIn)
    {
        if (File.Exists(idOrPath))
        {
            result = ProfileLoader.LoadFile(idOrPath);
            isBuiltIn = false;
            return true;
        }

        ProfileLoadResult? builtIn = ProfileLoader.TryLoadBuiltIn(idOrPath);
        if (builtIn is not null)
        {
            result = builtIn;
            isBuiltIn = true;
            return true;
        }

        stderr.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"No built-in profile or profile file '{idOrPath}'. Run 'otw profile list' for the built-in ids."));
        result = null!;
        isBuiltIn = false;
        return false;
    }

    private static void WriteIssues(TextWriter writer, IEnumerable<CatalogIssue> issues)
    {
        foreach (CatalogIssue issue in issues)
        {
            writer.WriteLine(issue.ToString());
        }
    }

    private static void WriteTable(TextWriter writer, List<Profile> profiles)
    {
        int idWidth = Math.Max("ID".Length, profiles.Count == 0 ? 0 : profiles.Max(p => p.Id.Length));
        int nameWidth = Math.Max("NAME".Length, profiles.Count == 0 ? 0 : profiles.Max(p => p.Name.Length));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{"ID".PadRight(idWidth)}  {"NAME".PadRight(nameWidth)}  {"SCOPE",-9} DESCRIPTION"));
        foreach (Profile p in profiles)
        {
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{p.Id.PadRight(idWidth)}  {p.Name.PadRight(nameWidth)}  {p.Scope,-9} {p.Description}"));
        }

        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{profiles.Count} profiles."));
    }

    private static void WriteProfile(TextWriter writer, Profile p)
    {
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{p.Id}  [{p.Name} / scope {p.Scope}]"));
        writer.WriteLine(p.Description);
        writer.WriteLine();

        writer.Write("Levels:");
        foreach (Category category in Enum.GetValues<Category>())
        {
            writer.Write(string.Create(CultureInfo.InvariantCulture, $" {category}={p.LevelFor(category)}"));
        }

        writer.WriteLine();
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Include: {(p.Include.Count == 0 ? "-" : string.Join(", ", p.Include.Select(i => i.Value)))}"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Exclude: {(p.Exclude.Count == 0 ? "-" : string.Join(", ", p.Exclude.Select(e => e.Value)))}"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Include draft: {p.IncludeDraft}"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Options: restorePoint={p.Options.RestorePoint}, restartExplorer={p.Options.RestartExplorer}, allowAdvanced={p.Options.AllowAdvanced}, allowBreaking={p.Options.AllowBreaking}"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Applies to: editions [{string.Join(", ", p.AppliesTo.Editions)}], builds {p.AppliesTo.MinBuild?.ToString(CultureInfo.InvariantCulture) ?? "*"}..{p.AppliesTo.MaxBuild?.ToString(CultureInfo.InvariantCulture) ?? "*"}, arch [{string.Join(", ", p.AppliesTo.Architectures)}]"));
    }
}
