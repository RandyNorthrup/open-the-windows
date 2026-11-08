using System.Globalization;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Updates;

/// <summary>
/// Holds the device on a chosen Windows 11 feature-update version (for example 24H2)
/// through the Windows Update for Business target-version policy, journaled and
/// reversible like every other change: <see cref="Target"/> writes the three policy
/// values, <see cref="Clear"/> reverts every active target. Monthly security updates for
/// the held version keep installing; only the move to a newer feature update waits.
/// The version must be one the end-of-servicing calendar knows for this edition, and
/// Windows Home is refused because it ignores the policy.
/// </summary>
public sealed class UpdateTargetService
{
    /// <summary>The profile name target runs are journaled under.</summary>
    public const string TargetProfileName = "updates-target";

    /// <summary>The entry id of a target run.</summary>
    public const string TargetEntryId = "updates.target.version";

    private const string ProductName = "Windows 11";
    private const int PolicyEnabled = 1;
    private static readonly IReadOnlyList<Uri> Sources =
        [new("https://learn.microsoft.com/windows/client-management/mdm/policy-csp-update#targetreleaseversion")];

    private readonly ApplyEngine _engine;
    private readonly IOperatingSystemInfo _os;
    private readonly EndOfServicingCalendar _calendar;

    /// <summary>Creates the service over the apply engine, the OS facts and the servicing calendar.</summary>
    public UpdateTargetService(ApplyEngine engine, IOperatingSystemInfo os, EndOfServicingCalendar calendar)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _os = os ?? throw new ArgumentNullException(nameof(os));
        _calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
    }

    /// <summary>The versions this device can be held on (the calendar's rows for its servicing track).</summary>
    public IReadOnlyList<string> KnownVersions() => _calendar.Versions(Family());

    /// <summary>Holds feature updates at <paramref name="version"/>.</summary>
    /// <exception cref="InvalidOperationException">The edition is Windows Home, which ignores the policy.</exception>
    /// <exception cref="ArgumentException">The version is not one the calendar knows for this edition.</exception>
    public ApplyResult Target(string version, bool whatIf)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        if (WindowsEditions.FromEditionId(_os.GetFacts().EditionId) == WindowsEdition.Home)
        {
            throw new InvalidOperationException(
                "Holding a feature-update version needs Windows Update for Business (Pro, Enterprise or Education); Windows Home ignores it.");
        }

        string normalized = version.Trim().ToUpperInvariant();
        if (_calendar.Find(normalized, Family()) is null)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture,
                $"'{version}' is not a Windows 11 version this edition can be held on. Known: {string.Join(", ", KnownVersions())}."), nameof(version));
        }

        var options = new ApplyOptions(whatIf, CreateRestorePoint: false, BreakGlass: false, AllowAdvanced: false, AllowBreaking: false, TargetProfileName, RestartExplorer: false);
        return _engine.Apply([BuildEntry(normalized)], options);
    }

    /// <summary>Reverts every active target (newest first), so feature updates are offered again.</summary>
    public IReadOnlyList<RevertResult> Clear(bool whatIf)
        => [.. ActiveRuns.For(_engine.History(), TargetProfileName)
            .Select(run => _engine.Revert(run.RunId, new RevertOptions(whatIf, RestartExplorer: false)))];

    private EndOfServicingEditionFamily Family()
        => WindowsEditions.FromEditionId(_os.GetFacts().EditionId) is { } edition
            ? EndOfServicingCalendar.FamilyFor(edition)
            : EndOfServicingEditionFamily.Consumer;

    private static TweakDefinition BuildEntry(string version)
    {
        static RegistryAction Policy(string name, RegistryValueType type, System.Text.Json.JsonElement value)
            => new(RegistryHive.LocalMachine, WindowsUpdateSettings.PolicyPath, name, type, value, UpdateJson.Null, RegistryValueKind.Policy);

        return new TweakDefinition(
            new TweakId(TargetEntryId),
            string.Create(CultureInfo.InvariantCulture, $"Hold feature updates at Windows 11 {version}"),
            string.Create(CultureInfo.InvariantCulture, $"Keeps this device on Windows 11 {version}; security updates for it keep installing."),
            "The Windows Update for Business target-version policy is the supported way to stay on a feature update until it nears end of servicing.",
            Category.Updates,
            Level.Basic,
            RiskTier.Caution,
            Scope.Machine,
            TweakStatus.Verified,
            Applicability.Any,
            [
                Policy(WindowsUpdateSettings.TargetReleaseVersion, RegistryValueType.Dword, UpdateJson.Number(PolicyEnabled)),
                Policy(WindowsUpdateSettings.ProductVersion, RegistryValueType.Sz, UpdateJson.String(ProductName)),
                Policy(WindowsUpdateSettings.TargetReleaseVersionInfo, RegistryValueType.Sz, UpdateJson.String(version)),
            ],
            RestartRequirement.None,
            [],
            [],
            null,
            ["When the held version reaches end of servicing the device stops getting security updates; clear the hold before then."],
            Sources,
            [],
            ["windows-update", "target-version"]);
    }
}
