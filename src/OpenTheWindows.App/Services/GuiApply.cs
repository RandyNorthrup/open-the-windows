using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.App.Services;

/// <summary>
/// Builds the standard GUI apply options and opens the review-and-apply flow.
/// Shared by the category and profile pages so the option set (restore point on,
/// advanced/breaking allowed subject to the flow's typed confirmation, no
/// automatic Explorer restart) is defined in exactly one place.
/// </summary>
internal static class GuiApply
{
    /// <summary>Launches the review-and-apply flow for <paramref name="entries"/> under the standard GUI options.</summary>
    public static void Launch(
        IApplyFlowLauncher launcher,
        string title,
        IReadOnlyList<TweakDefinition> entries,
        string profileName,
        Scope scope)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(entries);
        ApplyOptions options = new(
            WhatIf: false,
            CreateRestorePoint: true,
            BreakGlass: false,
            AllowAdvanced: true,
            AllowBreaking: true,
            ProfileName: profileName,
            RestartExplorer: false,
            Scope: scope);
        launcher.Launch(title, entries, options);
    }
}
