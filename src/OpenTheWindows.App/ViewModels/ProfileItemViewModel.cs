using OpenTheWindows.App.Resources;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
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
}
