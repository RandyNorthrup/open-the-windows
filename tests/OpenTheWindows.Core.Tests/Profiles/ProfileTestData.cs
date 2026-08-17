using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Model;
using OpenTheWindows.Core.Profiles;
using OpenTheWindows.Core.Tests.Catalog;

namespace OpenTheWindows.Core.Tests.Profiles;

/// <summary>
/// Builders for profile unit tests: a small catalogue that spans levels, risk
/// tiers, statuses and applicability, plus a <see cref="Profile"/> factory whose
/// defaults turn every dial Off so a test can isolate one dimension.
/// </summary>
internal static class ProfileTestData
{
    /// <summary>A Windows 11 Pro machine (matches everything but Enterprise-only entries).</summary>
    public static OperatingSystemFacts ProFacts { get; } =
        new("Windows 11 Pro", "Professional", "25H2", 26200, 9168, "x64", "Client");

    /// <summary>Level Basic, Safe, Verified, Privacy.</summary>
    public static TweakDefinition BasicSafe { get; } = CatalogTestData.ValidEntry("privacy.a.basic-safe")
        with
    { Level = Level.Basic, Risk = RiskTier.Safe, Status = TweakStatus.Verified };

    /// <summary>Level Balanced, Safe, Verified, Privacy.</summary>
    public static TweakDefinition BalancedSafe { get; } = CatalogTestData.ValidEntry("privacy.b.balanced-safe")
        with
    { Level = Level.Balanced, Risk = RiskTier.Safe, Status = TweakStatus.Verified };

    /// <summary>Level Strict, Advanced, Verified, Privacy.</summary>
    public static TweakDefinition StrictAdvanced { get; } = CatalogTestData.ValidEntry("privacy.c.strict-advanced")
        with
    { Level = Level.Strict, Risk = RiskTier.Advanced, Status = TweakStatus.Verified };

    /// <summary>Level Strict, Breaking, Verified, Privacy.</summary>
    public static TweakDefinition StrictBreaking { get; } = CatalogTestData.ValidEntry("privacy.d.strict-breaking")
        with
    { Level = Level.Strict, Risk = RiskTier.Breaking, Status = TweakStatus.Verified };

    /// <summary>Level Basic, Safe, Draft, Privacy.</summary>
    public static TweakDefinition BasicDraft { get; } = CatalogTestData.ValidEntry("privacy.e.basic-draft")
        with
    { Level = Level.Basic, Risk = RiskTier.Safe, Status = TweakStatus.Draft };

    /// <summary>Level Basic, Safe, Verified, Security.</summary>
    public static TweakDefinition SecurityBasicSafe { get; } = CatalogTestData.ValidEntry("security.f.basic-safe")
        with
    { Category = Category.Security, Level = Level.Basic, Risk = RiskTier.Safe, Status = TweakStatus.Verified };

    /// <summary>Level Basic, Safe, Deprecated, Privacy.</summary>
    public static TweakDefinition BasicDeprecated { get; } = CatalogTestData.ValidEntry("privacy.g.basic-deprecated")
        with
    { Level = Level.Basic, Risk = RiskTier.Safe, Status = TweakStatus.Deprecated };

    /// <summary>Level Basic, Safe, Verified, Privacy, but Enterprise-only (never matches Pro).</summary>
    public static TweakDefinition EnterpriseOnly { get; } = CatalogTestData.ValidEntry("privacy.h.enterprise-only")
        with
    {
        Level = Level.Basic,
        Risk = RiskTier.Safe,
        Status = TweakStatus.Verified,
        AppliesTo = new Applicability([WindowsEdition.Enterprise], null, null, []),
    };

    /// <summary>A catalogue spanning every level, risk, status and an edition gate.</summary>
    public static TweakCatalog SampleCatalog() => new(
    [
        BasicSafe,
        BalancedSafe,
        StrictAdvanced,
        StrictBreaking,
        BasicDraft,
        SecurityBasicSafe,
        BasicDeprecated,
        EnterpriseOnly,
    ]);

    /// <summary>A level dial with all six categories, defaulting Off; named categories override.</summary>
    public static IReadOnlyDictionary<Category, Level> Levels(
        Level privacy = Level.Off,
        Level updates = Level.Off,
        Level security = Level.Off,
        Level performance = Level.Off,
        Level debloat = Level.Off,
        Level shell = Level.Off)
        => new Dictionary<Category, Level>
        {
            [Category.Privacy] = privacy,
            [Category.Updates] = updates,
            [Category.Security] = security,
            [Category.Performance] = performance,
            [Category.Debloat] = debloat,
            [Category.Shell] = shell,
        };

    /// <summary>Apply options with the risk gate configurable; the rest are false.</summary>
    public static ProfileOptions Options(bool allowAdvanced = false, bool allowBreaking = false)
        => new(RestorePoint: false, RestartExplorer: false, allowAdvanced, allowBreaking);

    /// <summary>A profile whose defaults turn every dial Off and allow neither Advanced nor Breaking.</summary>
    public static Profile Make(
        IReadOnlyDictionary<Category, Level>? levels = null,
        IReadOnlyList<TweakId>? include = null,
        IReadOnlyList<TweakId>? exclude = null,
        bool includeDraft = false,
        ProfileOptions? options = null,
        Scope scope = Scope.User,
        Applicability? appliesTo = null)
        => new(
            Profile.CurrentSchemaVersion,
            "test-profile",
            "Test Profile",
            "A profile used by unit tests.",
            levels ?? Levels(),
            include ?? [],
            exclude ?? [],
            includeDraft,
            scope,
            options ?? Options(),
            appliesTo ?? Applicability.Any);
}
