using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Catalog;

/// <summary>
/// One catalogue entry: everything the engine, the UI and an auditor need to
/// know about a tweak. Immutable; produced by <see cref="CatalogLoader"/>.
/// </summary>
/// <param name="Id">Stable identifier.</param>
/// <param name="Title">Short imperative title, e.g. "Disable advertising ID".</param>
/// <param name="Description">What the tweak changes, in user terms.</param>
/// <param name="Rationale">Why someone would want it, and what it costs.</param>
/// <param name="Category">Top-level grouping.</param>
/// <param name="Level">Lowest profile level that applies this entry by default.</param>
/// <param name="Risk">Breakage risk tier.</param>
/// <param name="Scope">Machine, user or all users.</param>
/// <param name="Status">Draft / Verified / Deprecated.</param>
/// <param name="AppliesTo">Edition/build/architecture applicability.</param>
/// <param name="Actions">Ordered state changes.</param>
/// <param name="Requires">Restart requirement after apply.</param>
/// <param name="DependsOn">Entries that must be applied first.</param>
/// <param name="ConflictsWith">Entries that must not be applied together.</param>
/// <param name="ManagedBy">Where an organisation manages the same setting, if anywhere.</param>
/// <param name="SideEffects">Known functional losses, one per item.</param>
/// <param name="Sources">Documentation URLs (at least one).</param>
/// <param name="VerifiedOn">Verification records per build.</param>
/// <param name="Tags">Free-form tags for search and profiles (e.g. "gamer", "developer").</param>
public sealed record TweakDefinition(
    TweakId Id,
    string Title,
    string Description,
    string Rationale,
    Category Category,
    Level Level,
    RiskTier Risk,
    Scope Scope,
    TweakStatus Status,
    Applicability AppliesTo,
    IReadOnlyList<ITweakAction> Actions,
    RestartRequirement Requires,
    IReadOnlyList<TweakId> DependsOn,
    IReadOnlyList<TweakId> ConflictsWith,
    ManagedBy? ManagedBy,
    IReadOnlyList<string> SideEffects,
    IReadOnlyList<Uri> Sources,
    IReadOnlyList<Verification> VerifiedOn,
    IReadOnlyList<string> Tags);
