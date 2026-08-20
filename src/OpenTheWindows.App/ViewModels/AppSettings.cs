using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// The user's GUI preferences, shared as a singleton so the pages that act on
/// them (apply flow, category and profile pages) read the current values. These
/// are session settings in M6; persistence is a follow-up.
/// </summary>
internal sealed partial class AppSettings : ObservableObject
{
    /// <summary>Whether an apply creates a System Restore point first.</summary>
    [ObservableProperty]
    private bool _createRestorePoint = true;

    /// <summary>Whether an apply restarts Explorer automatically when a change needs it.</summary>
    [ObservableProperty]
    private bool _restartExplorerAutomatically;

    /// <summary>Whether enterprise mode is on (hides the consumer-only built-in profiles).</summary>
    [ObservableProperty]
    private bool _enterpriseMode;

    /// <summary>The chosen theme.</summary>
    [ObservableProperty]
    private AppTheme _theme = AppTheme.System;
}
