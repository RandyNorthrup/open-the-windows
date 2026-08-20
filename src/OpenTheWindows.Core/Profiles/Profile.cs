using System.Diagnostics.CodeAnalysis;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Profiles;

/// <summary>
/// A named, data-defined profile: a level dial per <see cref="Category"/> plus
/// explicit include/exclude overrides, the application <see cref="Model.Scope"/>,
/// apply options and machine applicability. Immutable; produced by
/// <see cref="ProfileLoader"/> and resolved to a concrete entry set by
/// <see cref="ProfileResolver"/>.
/// </summary>
/// <param name="SchemaVersion">Profile schema version; the loader accepts version 1.</param>
/// <param name="Id">Stable kebab-case identifier, e.g. <c>enterprise-workstation</c>.</param>
/// <param name="Name">Short display name.</param>
/// <param name="Description">One-paragraph explanation of who the profile is for.</param>
/// <param name="Levels">The level dial for every category (all six are required).</param>
/// <param name="Include">Entry ids forced in regardless of level or risk (Deprecated ids are ignored).</param>
/// <param name="Exclude">Entry ids removed after level selection and includes; exclude always wins.</param>
/// <param name="IncludeDraft">Whether level dials may select entries whose status is still Draft.</param>
/// <param name="Scope">Whether per-user entries reach only the interactive user or every profile.</param>
/// <param name="Options">Apply options and the Advanced/Breaking risk gate.</param>
/// <param name="AppliesTo">Machines this whole profile is intended for (edition/build/architecture).</param>
[SuppressMessage(
    "Naming",
    "CA1724:Type names should not match namespaces",
    Justification = "The domain concept is a profile and the M5 spec names the type Profile; the only clash is " +
        "the legacy ASP.NET System.Web.Profile namespace, which this net10.0 assembly never references.")]
public sealed record Profile(
    int SchemaVersion,
    string Id,
    string Name,
    string Description,
    IReadOnlyDictionary<Category, Level> Levels,
    IReadOnlyList<TweakId> Include,
    IReadOnlyList<TweakId> Exclude,
    bool IncludeDraft,
    Scope Scope,
    ProfileOptions Options,
    Applicability AppliesTo)
{
    /// <summary>The schema version this loader understands.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// The level dial for <paramref name="category"/>, or <see cref="Level.Off"/>
    /// when the profile omits it (a valid loaded profile always defines all six).
    /// </summary>
    public Level LevelFor(Category category)
        => Levels.TryGetValue(category, out Level level) ? level : Level.Off;

    /// <summary>
    /// Returns <see langword="true"/> when this profile is intended for the given
    /// machine. Profile-level applicability gates the whole run; each entry is
    /// gated again by its own <see cref="TweakDefinition.AppliesTo"/> in the resolver.
    /// </summary>
    public bool Applies(OperatingSystemFacts facts) => AppliesTo.Matches(facts);
}
