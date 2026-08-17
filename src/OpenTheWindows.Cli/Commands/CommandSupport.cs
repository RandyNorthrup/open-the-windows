using System.CommandLine;
using System.Globalization;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// Shared command preconditions and option factories, so the read (scan) and
/// write (apply/revert) commands do not repeat the platform check, profile
/// resolution or the common option declarations.
/// </summary>
internal static class CommandSupport
{
    /// <summary>
    /// Runs the shared preamble of a profile command: supported platform, resolvable
    /// profile and a loadable catalogue. On failure writes the reason and yields the
    /// exit code (unknown profile uses <paramref name="unknownProfileExit"/>).
    /// </summary>
    public static bool TryResolveProfileCatalog(
        CliServices services,
        ParseResult parseResult,
        Option<string> profileOption,
        Option<string?> onlyOption,
        Option<string?> catalogDirOption,
        int unknownProfileExit,
        TextWriter stderr,
        out Level level,
        out TweakCatalog catalog,
        out string profileLabel,
        out int exitCode)
    {
        level = default;
        catalog = null!;
        profileLabel = string.Empty;

        if (!IsSupported(services.Doctor, stderr, out exitCode))
        {
            return false;
        }

        if (!TryResolveLevelAndLabel(
                parseResult.GetValue(onlyOption), parseResult.GetValue(profileOption) ?? string.Empty,
                unknownProfileExit, stderr, out level, out profileLabel, out exitCode))
        {
            return false;
        }

        CatalogLoadResult loaded = services.LoadCatalog(parseResult.GetValue(catalogDirOption));
        if (loaded.Catalog is null)
        {
            foreach (CatalogIssue issue in loaded.Errors)
            {
                stderr.WriteLine(issue.ToString());
            }

            stderr.WriteLine("Catalogue failed validation; run 'otw catalog validate' for details.");
            exitCode = ExitCodes.Error;
            return false;
        }

        catalog = loaded.Catalog;
        exitCode = ExitCodes.Success;
        return true;
    }

    /// <summary>
    /// Resolves the level and the run/report label for a scan or apply. In profile
    /// mode (<paramref name="onlyId"/> empty) a named <paramref name="profileName"/>
    /// is required and selects the level. With <c>--only</c> the profile is ignored:
    /// an explicit profile still sets the level label, otherwise the level is a
    /// placeholder (<see cref="TrySelectEntries"/> ignores it) and the run is
    /// labelled <c>only:&lt;id&gt;</c> so history reads clearly.
    /// </summary>
    private static bool TryResolveLevelAndLabel(
        string? onlyId, string profileName, int unknownProfileExit, TextWriter stderr,
        out Level level, out string profileLabel, out int exitCode)
    {
        level = Level.Basic;
        profileLabel = string.Empty;
        exitCode = ExitCodes.Success;

        if (string.IsNullOrEmpty(onlyId))
        {
            if (string.IsNullOrEmpty(profileName))
            {
                stderr.WriteLine("Option '--profile' is required unless '--only <id>' is given.");
                exitCode = unknownProfileExit;
                return false;
            }
        }
        else if (string.IsNullOrEmpty(profileName))
        {
            profileLabel = string.Create(CultureInfo.InvariantCulture, $"only:{onlyId}");
            return true;
        }

        if (!TryResolveProfile(profileName, stderr, out level))
        {
            exitCode = unknownProfileExit;
            return false;
        }

        profileLabel = profileName;
        return true;
    }

    /// <summary>Returns whether the platform is supported; on failure writes the reason and yields the exit code.</summary>
    public static bool IsSupported(DoctorService doctor, TextWriter stderr, out int exitCode)
    {
        SystemReport support = doctor.Run();
        if (support.Status != SupportStatus.Supported)
        {
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Unsupported platform ({support.Status}); see 'otw doctor'."));
            exitCode = ExitCodes.UnsupportedPlatform;
            return false;
        }

        exitCode = ExitCodes.Success;
        return true;
    }

    /// <summary>Resolves a named level profile; on failure writes the reason and returns <see langword="false"/>.</summary>
    public static bool TryResolveProfile(string? name, TextWriter stderr, out Level level)
    {
        if (!NamedProfile.TryResolve(name ?? string.Empty, out level))
        {
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Unknown profile '{name}'. Use one of: {string.Join(", ", NamedProfile.Names)}."));
            return false;
        }

        return true;
    }

    /// <summary>The shared <c>--profile</c> option (required unless <c>--only</c> is given).</summary>
    public static Option<string> ProfileOption() => new("--profile")
    {
        Description = "Built-in level profile: " + string.Join(", ", NamedProfile.Names) + " (required unless --only is given).",
    };

    /// <summary>The shared <c>--catalog-dir</c> option.</summary>
    public static Option<string?> CatalogDirOption() => new("--catalog-dir")
    {
        Description = "Directory of additional catalogue JSON files; entries override built-in ones by id.",
    };

    /// <summary>The shared <c>--include-draft</c> option.</summary>
    public static Option<bool> IncludeDraftOption() => new("--include-draft")
    {
        Description = "Include Draft entries (verified-only by default).",
    };

    /// <summary>The shared <c>--only</c> option: restrict the run to a single entry id.</summary>
    public static Option<string?> OnlyOption() => new("--only")
    {
        Description = "Operate on only the entry with this exact id (ignores the profile selection; includes Draft). Used for per-entry verification.",
    };

    /// <summary>
    /// Selects the entries a scan/apply run operates on. With <paramref name="onlyId"/>
    /// set it picks exactly that entry from the catalogue (regardless of level or
    /// status); otherwise it uses the named level profile. On an unknown
    /// <paramref name="onlyId"/> it writes the reason and returns <see langword="false"/>.
    /// </summary>
    public static bool TrySelectEntries(
        TweakCatalog catalog,
        Level level,
        bool includeDraft,
        string? onlyId,
        TextWriter stderr,
        out IReadOnlyList<TweakDefinition> entries)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (!string.IsNullOrEmpty(onlyId))
        {
            TweakDefinition? entry = catalog.Entries.FirstOrDefault(e => string.Equals(e.Id.Value, onlyId, StringComparison.Ordinal));
            if (entry is null)
            {
                stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"No catalogue entry with id '{onlyId}'."));
                entries = [];
                return false;
            }

            entries = [entry];
            return true;
        }

        entries = NamedProfile.Select(catalog, level, includeDraft);
        return true;
    }
}
