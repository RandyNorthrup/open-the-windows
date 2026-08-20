using System.Globalization;
using OpenTheWindows.App.Resources;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Model;
using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// One profile on the Profiles page: its identity, scope, signing state, the
/// entries it resolves to on this machine, and any validation errors.
/// </summary>
internal sealed class ProfileItemViewModel
{
    /// <summary>Projects a profile, resolving the entries it selects on this machine when it is valid.</summary>
    public ProfileItemViewModel(
        Profile profile,
        TweakCatalog catalog,
        OperatingSystemFacts facts,
        ProfileSignatureStatus signature,
        bool isBuiltIn,
        IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(errors);
        Profile = profile;
        Name = profile.Name;
        Description = profile.Description;
        ScopeLabel = DisplayLabels.For(profile.Scope);
        Signature = signature;
        SignatureLabel = DisplayLabels.For(signature);
        IsBuiltIn = isBuiltIn;
        Errors = errors;
        IsValid = errors.Count == 0;
        ResolvedEntries = IsValid ? ProfileResolver.Resolve(profile, catalog, facts) : [];
        EntryCount = ResolvedEntries.Count;
        CategoryLines = BuildCategoryLines(profile, ResolvedEntries);
        OptionLines = BuildOptionLines(profile);
        EntryCountLabel = IsValid
            ? string.Create(CultureInfo.CurrentCulture, $"{EntryCount} change(s) on this PC")
            : "Cannot be applied (see errors)";
    }

    // The per-category dial positions the profile sets, in display order, skipping
    // the categories it leaves at Off — a plain-language "what this changes".
    private static readonly Category[] CategoryOrder =
        [Category.Privacy, Category.Security, Category.Performance, Category.Debloat, Category.Shell, Category.Updates];

    private static List<string> BuildCategoryLines(Profile profile, IReadOnlyList<TweakDefinition> resolved)
    {
        List<string> lines = [];
        foreach (Category category in CategoryOrder)
        {
            Level level = profile.LevelFor(category);
            if (level == Level.Off)
            {
                continue;
            }

            int count = resolved.Count(e => e.Category == category);
            lines.Add(string.Create(CultureInfo.CurrentCulture,
                $"{category} — {DisplayLabels.For(level)} · {count} change(s)"));
        }

        if (lines.Count == 0)
        {
            lines.Add("No category changes");
        }

        return lines;
    }

    private static List<string> BuildOptionLines(Profile profile)
    {
        ProfileOptions options = profile.Options;
        List<string> lines = [];
        if (options.AllowBreaking)
        {
            lines.Add("Includes Breaking changes that can disrupt features");
        }
        else if (options.AllowAdvanced)
        {
            lines.Add("Includes Advanced changes; Breaking ones are excluded");
        }
        else
        {
            lines.Add("Safe and Caution changes only");
        }

        if (profile.IncludeDraft)
        {
            lines.Add("Includes unverified (Draft) changes");
        }

        if (options.RestorePoint)
        {
            lines.Add("Creates a System Restore point before applying");
        }

        if (options.RestartExplorer)
        {
            lines.Add("Restarts Explorer for shell changes that need it");
        }

        if (profile.Include.Count > 0)
        {
            lines.Add(string.Create(CultureInfo.CurrentCulture, $"{profile.Include.Count} specific change(s) always included"));
        }

        if (profile.Exclude.Count > 0)
        {
            lines.Add(string.Create(CultureInfo.CurrentCulture, $"{profile.Exclude.Count} change(s) explicitly excluded"));
        }

        return lines;
    }

    /// <summary>The underlying profile.</summary>
    public Profile Profile { get; }

    /// <summary>The profile name.</summary>
    public string Name { get; }

    /// <summary>What the profile is for.</summary>
    public string Description { get; }

    /// <summary>The profile's apply scope.</summary>
    public string ScopeLabel { get; }

    /// <summary>The profile's signing state.</summary>
    public ProfileSignatureStatus Signature { get; }

    /// <summary>The localised signing-state label.</summary>
    public string SignatureLabel { get; }

    /// <summary>True for a built-in profile.</summary>
    public bool IsBuiltIn { get; }

    /// <summary>Validation errors, if any.</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>The joined error text, or null when valid.</summary>
    public string? ErrorsText => Errors.Count == 0 ? null : string.Join("; ", Errors);

    /// <summary>Whether the profile is valid and can be applied.</summary>
    public bool IsValid { get; }

    /// <summary>The entries the profile resolves to on this machine.</summary>
    public IReadOnlyList<TweakDefinition> ResolvedEntries { get; }

    /// <summary>How many entries the profile would apply.</summary>
    public int EntryCount { get; }

    /// <summary>A plain-language "N change(s) on this PC" (or the cannot-apply reason).</summary>
    public string EntryCountLabel { get; }

    /// <summary>Per-category "what this changes" lines: dial position and resolved count for each category the profile touches.</summary>
    public IReadOnlyList<string> CategoryLines { get; }

    /// <summary>Plain-language lines for how the profile applies (risk ceiling, Draft, restore point, includes/excludes).</summary>
    public IReadOnlyList<string> OptionLines { get; }
}
