using System.Globalization;
using System.Text;
using OpenTheWindows.Core.Catalog;
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

    /// <summary>The "n of m selected" summary for a category page.</summary>
    public static string SelectedCount(int selected, int total) => string.Format(CultureInfo.CurrentCulture, SelectedCountFormat, selected, total);

    // en-US is the only culture shipped in M6, so the format is parsed once.
    private static readonly CompositeFormat SelectedCountFormat = CompositeFormat.Parse(Strings.Category_SelectedCount);
}
