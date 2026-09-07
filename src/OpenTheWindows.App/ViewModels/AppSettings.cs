using CommunityToolkit.Mvvm.ComponentModel;
using OpenTheWindows.App.Services;

namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// The user's GUI preferences, shared as a singleton so the pages that act on
/// them (apply flow, category and profile pages) read the current values.
/// When constructed with an <see cref="IGuiSettingsStore"/>, changes are written
/// to disk; the parameterless constructor is for tests and does not persist.
/// </summary>
internal sealed partial class AppSettings : ObservableObject
{
    private readonly IGuiSettingsStore? _store;
    private readonly bool _persist;

    /// <summary>Session-only settings (tests).</summary>
    public AppSettings()
    {
    }

    /// <summary>Loads from <paramref name="store"/> and persists later edits.</summary>
    public AppSettings(IGuiSettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        GuiSettingsSnapshot snapshot = store.Load();
        _createRestorePoint = snapshot.CreateRestorePoint;
        _restartExplorerAutomatically = snapshot.RestartExplorerAutomatically;
        _enterpriseMode = snapshot.EnterpriseMode;
        _theme = snapshot.Theme;
        _persist = true;
    }

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

    partial void OnCreateRestorePointChanged(bool value) => Persist();

    partial void OnRestartExplorerAutomaticallyChanged(bool value) => Persist();

    partial void OnEnterpriseModeChanged(bool value) => Persist();

    partial void OnThemeChanged(AppTheme value) => Persist();

    private void Persist()
    {
        if (!_persist || _store is null)
        {
            return;
        }

        _store.Save(new GuiSettingsSnapshot(
            GuiSettingsSnapshot.CurrentSchemaVersion,
            CreateRestorePoint,
            RestartExplorerAutomatically,
            EnterpriseMode,
            Theme));
    }
}
