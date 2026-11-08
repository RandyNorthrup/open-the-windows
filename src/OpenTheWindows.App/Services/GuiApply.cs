using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.App.Services;

/// <summary>
/// Builds the standard GUI apply options and opens the review-and-apply flow.
/// Shared by the category and profile pages so the option set (restore point per
/// settings, advanced/breaking allowed subject to the flow's typed confirmation,
/// Explorer restart offered in the flow) is defined in exactly one place.
/// </summary>
internal static class GuiApply
{
    /// <summary>Launches the review-and-apply flow for <paramref name="entries"/> under the standard GUI options.</summary>
    public static void Launch(
        IApplyFlowLauncher launcher,
        string title,
        IReadOnlyList<TweakDefinition> entries,
        string profileName,
        Scope scope,
        bool createRestorePoint,
        bool restartExplorer = false)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(entries);
        ApplyOptions options = new(
            WhatIf: false,
            CreateRestorePoint: createRestorePoint,
            BreakGlass: false,
            AllowAdvanced: true,
            AllowBreaking: true,
            ProfileName: profileName,
            RestartExplorer: restartExplorer,
            Scope: scope);
        launcher.Launch(title, entries, options);
    }
}
