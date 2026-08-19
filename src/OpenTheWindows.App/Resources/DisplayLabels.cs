using System.Globalization;
using System.Text;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.App.Resources;

/// <summary>
/// Maps the domain enums to their localised display strings, so every view
/// model formats a risk tier, level, restart requirement or status the same way
/// (single source; keeps the resource keys in one place).
/// </summary>
internal static class DisplayLabels
{
    /// <summary>The localised label for a risk tier.</summary>
    public static string For(RiskTier risk) => risk switch
    {
        RiskTier.Safe => Strings.Risk_Safe,
        RiskTier.Caution => Strings.Risk_Caution,
        RiskTier.Advanced => Strings.Risk_Advanced,
        RiskTier.Breaking => Strings.Risk_Breaking,
        _ => risk.ToString(),
    };

    /// <summary>The localised label for a level dial position.</summary>
    public static string For(Level level) => level switch
    {
        Level.Off => Strings.Level_Off,
        Level.Basic => Strings.Level_Basic,
        Level.Balanced => Strings.Level_Balanced,
        Level.Strict => Strings.Level_Strict,
        Level.Paranoid => Strings.Level_Paranoid,
        _ => level.ToString(),
    };

    /// <summary>The localised label for a restart requirement.</summary>
    public static string For(RestartRequirement requirement) => requirement switch
    {
        RestartRequirement.None => Strings.Restart_None,
        RestartRequirement.ExplorerRestart => Strings.Restart_ExplorerRestart,
        RestartRequirement.SignOut => Strings.Restart_SignOut,
        RestartRequirement.Reboot => Strings.Restart_Reboot,
        _ => requirement.ToString(),
    };

    /// <summary>The localised label for a catalogue entry status.</summary>
    public static string For(TweakStatus status) => status switch
    {
        TweakStatus.Draft => Strings.Status_Draft,
        TweakStatus.Verified => Strings.Status_Verified,
        TweakStatus.Deprecated => Strings.Status_Deprecated,
        _ => status.ToString(),
    };

    /// <summary>A human-readable label for a plan disposition (what the run would do to an entry).</summary>
    public static string For(PlanDisposition disposition) => disposition switch
    {
        PlanDisposition.Apply => "Will apply",
        PlanDisposition.AlreadyCompliant => "Already set",
        PlanDisposition.Managed => "Managed by policy",
        PlanDisposition.ManagedBreakGlass => "Override managed (break-glass)",
        PlanDisposition.NotApplicable => "Not applicable",
        PlanDisposition.Blocked => "Blocked",
        PlanDisposition.Refused => "Refused",
        PlanDisposition.Conflict => "Conflicts with another entry",
        _ => disposition.ToString(),
    };

    /// <summary>A human-readable label for the outcome of an applied (or reverted) entry.</summary>
    public static string For(ApplyState state) => state switch
    {
        ApplyState.Pending => "Pending",
        ApplyState.Applied => "Applied",
        ApplyState.Failed => "Failed",
        ApplyState.RolledBack => "Rolled back",
        ApplyState.Skipped => "Skipped",
        ApplyState.Managed => "Managed",
        ApplyState.Refused => "Refused",
        ApplyState.NotApplicable => "Not applicable",
        _ => state.ToString(),
    };

    /// <summary>A human-readable label for the state of a whole run.</summary>
    public static string For(RunState state) => state switch
    {
        RunState.InProgress => "In progress",
        RunState.Completed => "Completed",
        RunState.RolledBack => "Rolled back",
        RunState.Failed => "Failed",
        _ => state.ToString(),
    };

    /// <summary>A human-readable label for an apply scope.</summary>
    public static string For(Scope scope) => scope switch
    {
        Scope.Machine => "Machine",
        Scope.User => "Current user",
        Scope.AllUsers => "All users",
        _ => scope.ToString(),
    };

    /// <summary>A human-readable label for a profile's signing state.</summary>
    public static string For(ProfileSignatureStatus status) => status switch
    {
        ProfileSignatureStatus.BuiltIn => "Built-in",
        ProfileSignatureStatus.Unsigned => "Unsigned",
        ProfileSignatureStatus.Untrusted => "Signed (untrusted key)",
        ProfileSignatureStatus.Trusted => "Signed (trusted)",
        ProfileSignatureStatus.Invalid => "Invalid signature",
        _ => status.ToString(),
    };

    /// <summary>The "n of m selected" summary for a category page.</summary>
    public static string SelectedCount(int selected, int total) => string.Format(CultureInfo.CurrentCulture, SelectedCountFormat, selected, total);

    // en-US is the only culture shipped in M6, so the format is parsed once.
    private static readonly CompositeFormat SelectedCountFormat = CompositeFormat.Parse(Strings.Category_SelectedCount);
}
