using System.CommandLine;
using System.Globalization;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Model;
using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// Shared command preconditions and option factories, so the read (scan) and
/// write (apply/revert) commands do not repeat the platform check, profile
/// resolution or the common option declarations.
/// </summary>
internal static class CommandSupport
{
    /// <summary>
    /// Runs the shared preamble of a profile command: supported platform and a
    /// loadable catalogue. Also yields the machine facts so the caller can resolve
    /// a profile. On failure writes the reason and yields the exit code.
    /// </summary>
    public static bool TryResolveProfileCatalog(
        CliServices services,
        ParseResult parseResult,
        Option<string?> catalogDirOption,
        TextWriter stderr,
        out TweakCatalog catalog,
        out OperatingSystemFacts facts,
        out int exitCode)
    {
        catalog = null!;

        if (!IsSupported(services.Doctor, stderr, out exitCode, out facts))
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
    /// Selects the entries a scan/apply run operates on and the run/report label.
    /// <c>--only &lt;id&gt;</c> wins and picks exactly that entry (regardless of level
    /// or status). Otherwise <paramref name="profileValue"/> is resolved as, in
    /// order: a built-in level name (<c>basic</c>..<c>paranoid</c>, applied uniformly
    /// with <paramref name="includeDraft"/>); a built-in profile id or a path to a
    /// profile <c>.json</c> file (resolved through <see cref="ProfileResolver"/>,
    /// which honours the profile's own dials, include/exclude, risk gate and draft
    /// setting). On an unknown or unloadable value it writes the reason and yields
    /// <paramref name="unknownProfileExit"/>.
    /// When <paramref name="profileValue"/> is a file and <paramref name="policy"/>
    /// requires signed profiles, the file must carry a valid signature from a key in
    /// <paramref name="trustStoreDirectory"/> or the selection is refused. Built-in
    /// ids and level names are never subject to that check.
    /// </summary>
    public static bool TrySelectEntries(
        TweakCatalog catalog,
        OperatingSystemFacts facts,
        string? profileValue,
        string? onlyId,
        bool includeDraft,
        ProfilePolicy policy,
        string trustStoreDirectory,
        int unknownProfileExit,
        TextWriter stderr,
        out IReadOnlyList<TweakDefinition> entries,
        out string label,
        out int exitCode,
        out Profile? profile)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(catalog);
        entries = [];
        label = string.Empty;
        exitCode = ExitCodes.Success;
        profile = null;

        if (!string.IsNullOrEmpty(onlyId))
        {
            TweakDefinition? entry = catalog.Entries.FirstOrDefault(e => string.Equals(e.Id.Value, onlyId, StringComparison.Ordinal));
            if (entry is null)
            {
                stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"No catalogue entry with id '{onlyId}'."));
                exitCode = unknownProfileExit;
                return false;
            }

            entries = [entry];
            label = string.Create(CultureInfo.InvariantCulture, $"only:{onlyId}");
            return true;
        }

        string value = profileValue ?? string.Empty;
        if (string.IsNullOrEmpty(value))
        {
            stderr.WriteLine("Option '--profile' is required unless '--only <id>' is given.");
            exitCode = unknownProfileExit;
            return false;
        }

        if (NamedProfile.TryResolve(value, out Level level))
        {
            entries = NamedProfile.Select(catalog, level, includeDraft);
            label = value;
            return true;
        }

        return TryResolveProfileEntries(catalog, facts, value, policy, trustStoreDirectory, unknownProfileExit, stderr, out entries, out label, out exitCode, out profile);
    }

    /// <summary>Resolves a built-in profile id or a profile file through the profile engine, enforcing the signing policy for files.</summary>
    private static bool TryResolveProfileEntries(
        TweakCatalog catalog,
        OperatingSystemFacts facts,
        string value,
        ProfilePolicy policy,
        string trustStoreDirectory,
        int unknownProfileExit,
        TextWriter stderr,
        out IReadOnlyList<TweakDefinition> entries,
        out string label,
        out int exitCode,
        out Profile? profile)
    {
        entries = [];
        label = string.Empty;
        exitCode = ExitCodes.Success;
        profile = null;

        bool isFile = File.Exists(value);
        if (isFile && !EnforceSigningPolicy(value, policy, trustStoreDirectory, unknownProfileExit, stderr, out exitCode))
        {
            return false;
        }

        ProfileLoadResult? loaded = isFile ? ProfileLoader.LoadFile(value) : ProfileLoader.TryLoadBuiltIn(value);
        if (loaded is null)
        {
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Unknown profile '{value}'. Use a level ({string.Join(", ", NamedProfile.Names)}), a built-in profile id " +
                $"({string.Join(", ", ProfileLoader.BuiltInIds)}), or a path to a profile .json file."));
            exitCode = unknownProfileExit;
            return false;
        }

        if (loaded.Profile is null)
        {
            foreach (CatalogIssue issue in loaded.Errors)
            {
                stderr.WriteLine(issue.ToString());
            }

            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Profile '{value}' failed to load."));
            exitCode = unknownProfileExit;
            return false;
        }

        profile = loaded.Profile;
        entries = ProfileResolver.Resolve(loaded.Profile, catalog, facts);
        label = loaded.Profile.Id;
        return true;
    }

    /// <summary>
    /// Applies <see cref="ProfilePolicy.RequireSignedProfiles"/> to a profile file:
    /// returns true (and <paramref name="exitCode"/> Success) when the file may be
    /// used, or false (writing the reason and yielding
    /// <paramref name="unknownProfileExit"/>) when the policy is on and the file is
    /// unsigned or not signed by a trusted key.
    /// </summary>
    private static bool EnforceSigningPolicy(
        string profilePath, ProfilePolicy policy, string trustStoreDirectory, int unknownProfileExit, TextWriter stderr, out int exitCode)
    {
        exitCode = ExitCodes.Success;
        IReadOnlyList<CatalogIssue> issues = ProfileTrust.Enforce(profilePath, policy, trustStoreDirectory);
        if (issues.Count == 0)
        {
            return true;
        }

        foreach (CatalogIssue issue in issues)
        {
            stderr.WriteLine(issue.ToString());
        }

        stderr.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Refusing profile file '{profilePath}': policy {ProfilePolicy.RequireSignedProfilesValueName} requires a trusted signature."));
        exitCode = unknownProfileExit;
        return false;
    }

    /// <summary>Returns whether the platform is supported; on failure writes the reason and yields the exit code.</summary>
    public static bool IsSupported(DoctorService doctor, TextWriter stderr, out int exitCode)
        => IsSupported(doctor, stderr, out exitCode, out _);

    /// <summary>As <see cref="IsSupported(DoctorService, TextWriter, out int)"/>, also yielding the machine facts.</summary>
    public static bool IsSupported(DoctorService doctor, TextWriter stderr, out int exitCode, out OperatingSystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(doctor);
        SystemReport support = doctor.Run();
        facts = support.OperatingSystem;
        if (support.Status != SupportStatus.Supported)
        {
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Unsupported platform ({support.Status}); see 'otw doctor'."));
            exitCode = ExitCodes.UnsupportedPlatform;
            return false;
        }

        exitCode = ExitCodes.Success;
        return true;
    }

    /// <summary>The shared <c>--profile</c> option (required unless <c>--only</c> is given).</summary>
    public static Option<string> ProfileOption() => new("--profile")
    {
        Description = "A level (" + string.Join("|", NamedProfile.Names)
            + "), a built-in profile id (see 'otw profile list'), or a path to a profile .json file "
            + "(required unless --only is given).",
    };

    /// <summary>The shared <c>--catalog-dir</c> option.</summary>
    public static Option<string?> CatalogDirOption() => new("--catalog-dir")
    {
        Description = "Directory of additional catalogue JSON files; entries override built-in ones by id.",
    };

    /// <summary>The shared <c>--include-draft</c> option.</summary>
    public static Option<bool> IncludeDraftOption() => new("--include-draft")
    {
        Description = "Include Draft entries (verified-only by default). Applies to level profiles; a named profile carries its own draft setting.",
    };

    /// <summary>The shared <c>--only</c> option: restrict the run to a single entry id.</summary>
    public static Option<string?> OnlyOption() => new("--only")
    {
        Description = "Operate on only the entry with this exact id (ignores the profile selection; includes Draft). Used for per-entry verification.",
    };
}
